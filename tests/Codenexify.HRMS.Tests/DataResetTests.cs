using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Infrastructure.Services;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class DataResetTests : IAsyncLifetime
{
    private const string Password = "Owner@Reset2026";
    private TestHost _host = null!;
    private int _ownerId, _supportId;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _host.Context.AsPlatform();
        (_ownerId, _supportId) = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var hash = sp.GetRequiredService<IPasswordService>().Hash(Password);
            var owner = new PlatformUser { Email = "reset-owner@codenexify.test", FullName = "Reset Owner", PasswordHash = hash, Role = PlatformRole.SuperAdmin };
            var support = new PlatformUser { Email = "reset-support@codenexify.test", FullName = "Reset Support", PasswordHash = hash, Role = PlatformRole.SupportAdmin };
            db.PlatformUsers.AddRange(owner, support);
            await db.SaveChangesAsync();
            return (owner.Id, support.Id);
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static DataResetInput Valid() => new() { Confirmation = DataResetRules.ConfirmationPhrase, Password = Password };

    private Task<T> Run<T>(int platformUserId, bool enabled, Func<IDataResetService, Task<T>> action)
    {
        _host.Context.AsPlatform();
        _host.Context.User = platformUserId;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DataResetRules.EnabledSetting] = enabled ? "true" : "false"
        }).Build();
        return _host.RunAsync(sp => action(ActivatorUtilities.CreateInstance<DataResetService>(sp, (IConfiguration)config)));
    }

    private Task<int> CountAsync(Func<AppDbContext, Task<int>> q) => _host.RunAsync(sp => q(sp.GetRequiredService<AppDbContext>()));
    private Task<int> Organizations() => CountAsync(db => db.Organizations.IgnoreQueryFilters().CountAsync());

    [Fact]
    public async Task Refuses_when_switched_off_in_settings()
    {
        await _host.ProvisionAsync("rst1");
        var result = await Run(_ownerId, enabled: false, s => s.ResetAsync(Valid()));
        Assert.False(result.Succeeded);
        Assert.Contains("switched off", result.Error);
        Assert.Equal(1, await Organizations());
    }

    [Fact]
    public async Task Only_a_super_admin_can_reset()
    {
        await _host.ProvisionAsync("rst2");
        var result = await Run(_supportId, enabled: true, s => s.ResetAsync(Valid()));
        Assert.False(result.Succeeded);
        Assert.Contains("Super Admin", result.Error);

        var status = await Run(_supportId, enabled: true, s => s.StatusAsync());
        Assert.False(status.IsSuperAdmin);
        Assert.Equal(1, await Organizations());
    }

    [Fact]
    public async Task Wrong_phrase_or_password_deletes_nothing()
    {
        await _host.ProvisionAsync("rst3");
        var phrase = await Run(_ownerId, true, s => s.ResetAsync(new DataResetInput { Confirmation = "delete all company data", Password = Password }));
        Assert.False(phrase.Succeeded);
        Assert.Contains("confirmation phrase", phrase.Error);

        var password = await Run(_ownerId, true, s => s.ResetAsync(new DataResetInput { Confirmation = DataResetRules.ConfirmationPhrase, Password = "wrong-password" }));
        Assert.False(password.Succeeded);
        Assert.Contains("password is incorrect", password.Error);
        Assert.Equal(1, await Organizations());
    }

    [Fact]
    public async Task Authenticator_code_is_required_when_two_step_sign_in_is_on()
    {
        await _host.ProvisionAsync("rst4");
        var secret = await _host.RunAsync(async sp =>
        {
            var totp = sp.GetRequiredService<ITotpService>();
            var db = sp.GetRequiredService<AppDbContext>();
            var owner = await db.PlatformUsers.SingleAsync(u => u.Id == _ownerId);
            var s = totp.GenerateSecret();
            owner.TwoFactorSecret = sp.GetRequiredService<ISecretProtector>().Protect(s);
            owner.TwoFactorEnabled = true;
            await db.SaveChangesAsync();
            return s;
        });

        Assert.True((await Run(_ownerId, true, s => s.StatusAsync())).TwoFactorRequired);
        var missing = await Run(_ownerId, true, s => s.ResetAsync(Valid()));
        Assert.False(missing.Succeeded);
        Assert.Contains("authenticator code", missing.Error);
        Assert.Equal(1, await Organizations());

        var input = Valid();
        input.TotpCode = TotpService.Compute(TotpService.FromBase32(secret), new DateTimeOffset(_host.Clock.UtcNow).ToUnixTimeSeconds() / 30);
        var ok = await Run(_ownerId, true, s => s.ResetAsync(input));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(0, await Organizations());
    }

    [Fact]
    public async Task Reset_removes_every_company_and_keeps_control_center_setup()
    {
        var a = await _host.ProvisionAsync("rst5");
        await _host.ProvisionAsync("rst6");
        var file = Path.Combine(_host.StorageRoot, a.ToString(), "doc.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "payslip");

        var plansBefore = await CountAsync(db => db.SubscriptionPlans.CountAsync());
        var featuresBefore = await CountAsync(db => db.Features.CountAsync());
        var statutoryBefore = await CountAsync(db => db.StatutoryRateSets.CountAsync());
        var before = await Run(_ownerId, true, s => s.StatusAsync());
        Assert.Equal(2, before.Organizations);
        Assert.True(before.TenantUsers >= 2);
        Assert.Equal(1, before.UploadedFiles);

        var result = await Run(_ownerId, true, s => s.ResetAsync(Valid()));
        Assert.True(result.Succeeded, result.Error);
        Assert.Null(result.Value!.BackupFile); // SQLite test database: no SQL Server backup
        Assert.True(result.Value.TablesEmptied > 50);

        Assert.Equal(0, await Organizations());
        Assert.Equal(0, await CountAsync(db => db.Users.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(0, await CountAsync(db => db.Employees.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(plansBefore, await CountAsync(db => db.SubscriptionPlans.CountAsync()));
        Assert.Equal(featuresBefore, await CountAsync(db => db.Features.CountAsync()));
        Assert.Equal(statutoryBefore, await CountAsync(db => db.StatutoryRateSets.CountAsync()));
        Assert.Equal(1, await CountAsync(db => db.PlatformUsers.CountAsync(u => u.Id == _ownerId && u.IsActive)));

        // Files were moved aside, not deleted; the storage folder is empty and still usable.
        Assert.NotNull(result.Value.UploadsMovedTo);
        Assert.True(File.Exists(Path.Combine(result.Value.UploadsMovedTo!, a.ToString(), "doc.txt")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_host.StorageRoot));
        Directory.Delete(result.Value.UploadsMovedTo!, recursive: true);

        // The emptied audit log records who did it.
        var logged = await CountAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(l => l.Action == "Platform.DataReset" && l.ActorId == _ownerId));
        Assert.Equal(1, logged);

        // A company can register again with a previously used code.
        await _host.ProvisionAsync("rst5");
        Assert.Equal(1, await Organizations());
    }
}
