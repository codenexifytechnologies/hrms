using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed partial class PasswordResetTests : IAsyncLifetime
{
    private const string Base = "https://hr.example.test";
    private const string OldPassword = "Passw0rd!x";
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.CreateAsync();
    public async Task DisposeAsync() => await _host.DisposeAsync();

    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenRegex();

    private string LastToken(string to)
    {
        var mail = _host.Email.Sent.Last(m => m.To == to && m.Subject.Contains("Reset"));
        return TokenRegex().Match(mail.HtmlBody).Groups[1].Value;
    }

    private Task<T> Reset<T>(Func<IPasswordResetService, Task<T>> action)
    {
        _host.Context.AsAnonymous();
        return _host.RunAsync(sp => action(sp.GetRequiredService<IPasswordResetService>()));
    }
    private Task Reset(Func<IPasswordResetService, Task> action) => Reset(async s => { await action(s); return true; });

    private Task<AuthResult> SignInAsync(string code, string email, string password)
    {
        _host.Context.AsAnonymous();
        return _host.RunAsync(sp => sp.GetRequiredService<ITenantAuthService>().SignInAsync(code, email, password, "127.0.0.1", "test"));
    }

    [Fact]
    public async Task Company_user_resets_password_with_emailed_link_once()
    {
        await _host.ProvisionAsync("pwr1");
        var stampBefore = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().Where(u => u.Email == "admin@pwr1.test").Select(u => u.SecurityStamp).SingleAsync());

        await Reset(s => s.RequestForCompanyUserAsync("PWR1", " Admin@pwr1.test ", Base, "10.0.0.1"));
        var token = LastToken("admin@pwr1.test");
        Assert.True(token.Length >= 40);
        Assert.Contains($"{Base}/reset-password?token=", _host.Email.Sent.Last().HtmlBody);

        // Only the hash is stored.
        Assert.False(await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().PasswordResetTokens.AnyAsync(t => t.TokenHash == token)));

        var link = await Reset(s => s.CheckAsync(token));
        Assert.NotNull(link);
        Assert.Equal(PrincipalType.Tenant, link!.Type);
        Assert.Equal("pwr1", link.OrganizationCode);
        Assert.DoesNotContain("admin@", link.MaskedEmail);

        Assert.False((await Reset(s => s.ResetAsync(token, "short"))).Succeeded);
        Assert.Contains("different", (await Reset(s => s.ResetAsync(token, OldPassword))).Error);
        var ok = await Reset(s => s.ResetAsync(token, "BrandNew@Pass1", "10.0.0.1"));
        Assert.True(ok.Succeeded, ok.Error);

        Assert.False((await SignInAsync("pwr1", "admin@pwr1.test", OldPassword)).Succeeded);
        Assert.True((await SignInAsync("pwr1", "admin@pwr1.test", "BrandNew@Pass1")).Succeeded);

        // Other sessions end; the link can't be reused; the user is told by email.
        var stampAfter = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().Where(u => u.Email == "admin@pwr1.test").Select(u => u.SecurityStamp).SingleAsync());
        Assert.NotEqual(stampBefore, stampAfter);
        Assert.Null(await Reset(s => s.CheckAsync(token)));
        Assert.False((await Reset(s => s.ResetAsync(token, "Another@Pass22"))).Succeeded);
        Assert.Contains(_host.Email.Sent, m => m.To == "admin@pwr1.test" && m.Subject == "Your password was changed");
    }

    [Fact]
    public async Task Unknown_wrong_company_or_disabled_accounts_get_no_email()
    {
        await _host.ProvisionAsync("pwr2");
        await _host.ProvisionAsync("pwr3");
        await Reset(s => s.RequestForCompanyUserAsync("pwr2", "nobody@pwr2.test", Base, null));
        await Reset(s => s.RequestForCompanyUserAsync("pwr3", "admin@pwr2.test", Base, null)); // right email, other company
        await Reset(s => s.RequestForCompanyUserAsync("nope", "admin@pwr2.test", Base, null));

        await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var u = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Email == "admin@pwr2.test");
            using (_host.Context.BeginOrganizationScope(u.OrganizationId)) { u.IsActive = false; await db.SaveChangesAsync(); }
        });
        await Reset(s => s.RequestForCompanyUserAsync("pwr2", "admin@pwr2.test", Base, null));

        Assert.DoesNotContain(_host.Email.Sent, m => m.Subject.Contains("Reset"));
        Assert.Equal(0, await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().PasswordResetTokens.CountAsync()));
    }

    [Fact]
    public async Task New_link_replaces_old_one_links_expire_and_requests_are_limited()
    {
        await _host.ProvisionAsync("pwr4");
        await Reset(s => s.RequestForCompanyUserAsync("pwr4", "admin@pwr4.test", Base, null));
        var first = LastToken("admin@pwr4.test");
        await Reset(s => s.RequestForCompanyUserAsync("pwr4", "admin@pwr4.test", Base, null));
        var second = LastToken("admin@pwr4.test");
        Assert.NotEqual(first, second);
        Assert.Null(await Reset(s => s.CheckAsync(first)));
        Assert.NotNull(await Reset(s => s.CheckAsync(second)));

        await Reset(s => s.RequestForCompanyUserAsync("pwr4", "admin@pwr4.test", Base, null));
        var third = LastToken("admin@pwr4.test");
        await Reset(s => s.RequestForCompanyUserAsync("pwr4", "admin@pwr4.test", Base, null)); // 4th within the hour: refused
        Assert.Equal(third, LastToken("admin@pwr4.test"));

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(PasswordResetService.LinkMinutes + 1);
        Assert.Null(await Reset(s => s.CheckAsync(third)));
        Assert.Contains("expired", (await Reset(s => s.ResetAsync(third, "BrandNew@Pass1"))).Error);
        Assert.Null(await Reset(s => s.CheckAsync("not-a-real-token")));
    }

    [Fact]
    public async Task Reset_clears_a_lockout()
    {
        await _host.ProvisionAsync("pwr5");
        for (var i = 0; i < 5; i++) await SignInAsync("pwr5", "admin@pwr5.test", "wrong-password");
        Assert.Equal(AuthStatus.LockedOut, (await SignInAsync("pwr5", "admin@pwr5.test", OldPassword)).Status);

        await Reset(s => s.RequestForCompanyUserAsync("pwr5", "admin@pwr5.test", Base, null));
        Assert.True((await Reset(s => s.ResetAsync(LastToken("admin@pwr5.test"), "BrandNew@Pass1"))).Succeeded);
        Assert.True((await SignInAsync("pwr5", "admin@pwr5.test", "BrandNew@Pass1")).Succeeded);
    }

    [Fact]
    public async Task Control_center_user_resets_password_and_two_step_sign_in_still_applies()
    {
        int id = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var u = new PlatformUser { Email = "staff@codenexify.test", FullName = "Staff One", PasswordHash = sp.GetRequiredService<IPasswordService>().Hash(OldPassword + "123"), Role = PlatformRole.SupportAdmin, MustChangePassword = true };
            var blocked = new PlatformUser { Email = "office@codenexify.test", FullName = "Office Only", PasswordHash = "x", Role = PlatformRole.SupportAdmin, AllowedIps = "10.9.9.9" };
            db.PlatformUsers.AddRange(u, blocked);
            await db.SaveChangesAsync();
            return u.Id;
        });

        await Reset(s => s.RequestForPlatformUserAsync("staff@codenexify.test", Base, "10.0.0.1"));
        var token = LastToken("staff@codenexify.test");
        Assert.Contains($"{Base}/superadmin/account/reset-password?token=", _host.Email.Sent.Last().HtmlBody);
        Assert.Equal(PrincipalType.Platform, (await Reset(s => s.CheckAsync(token)))!.Type);
        Assert.True((await Reset(s => s.ResetAsync(token, "Staff@NewPass9"))).Succeeded);

        _host.Context.AsAnonymous();
        var signIn = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformAuthService>().SignInAsync("staff@codenexify.test", "Staff@NewPass9", null, "10.0.0.1", "test"));
        Assert.True(signIn.Succeeded);
        Assert.False(signIn.Principal!.MustChangePassword);

        // Allow-listed staff can't even request a link from another network.
        await Reset(s => s.RequestForPlatformUserAsync("office@codenexify.test", Base, "10.0.0.1"));
        Assert.DoesNotContain(_host.Email.Sent, m => m.To == "office@codenexify.test");

        // A company link can't be used on a platform account and vice versa: the stored row decides.
        Assert.Equal(1, await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().PasswordResetTokens.CountAsync(t => t.PrincipalType == PrincipalType.Platform && t.UserId == id)));
    }
}
