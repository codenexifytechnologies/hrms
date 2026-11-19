using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Infrastructure.Services;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class AuthenticationTests : IAsyncLifetime
{
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.CreateAsync();
    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<int> AddPlatformUserAsync(string email, string password, PlatformRole role = PlatformRole.SupportAdmin, string? allowedIps = null)
    {
        _host.Context.AsPlatform();
        var r = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformUserService>().CreateAsync(new PlatformUserInput
        {
            FullName = "Test Staff", Email = email, Role = role, Password = password, AllowedIps = allowedIps
        }));
        Assert.True(r.Succeeded, r.Error);
        _host.Context.AsAnonymous();
        return r.Value;
    }

    private Task<AuthResult> PlatformSignIn(string email, string password, string? code = null, string ip = "10.0.0.1")
        => _host.RunAsync(sp => sp.GetRequiredService<IPlatformAuthService>().SignInAsync(email, password, code, ip, "tests"));

    [Fact]
    public void Totp_matches_rfc6238_reference_vector()
    {
        // RFC 6238 Appendix B, SHA-1 secret "12345678901234567890", T = 59s → 94287082 (8 digits) → 287082 (6 digits)
        var key = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal("287082", TotpService.Compute(key, 59 / 30));
    }

    [Fact]
    public async Task Platform_sign_in_succeeds_and_returns_role_permissions()
    {
        await AddPlatformUserAsync("support@cnx.test", "Sup3rSecret!");
        var result = await PlatformSignIn("SUPPORT@cnx.test", "Sup3rSecret!");
        Assert.True(result.Succeeded);
        Assert.Equal(PrincipalType.Platform, result.Principal!.Type);
        Assert.Null(result.Principal.OrganizationId);
        Assert.Contains(Application.Catalog.PlatformPermissions.OrganizationsView, result.Principal.Permissions);
        Assert.DoesNotContain(Application.Catalog.PlatformPermissions.PlansManage, result.Principal.Permissions);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_and_are_audited()
    {
        await AddPlatformUserAsync("lock@cnx.test", "Sup3rSecret!");
        for (var i = 0; i < 5; i++) Assert.False((await PlatformSignIn("lock@cnx.test", "wrong")).Succeeded);

        var locked = await PlatformSignIn("lock@cnx.test", "Sup3rSecret!");
        Assert.Equal(AuthStatus.LockedOut, locked.Status);

        _host.Context.AsPlatform();
        var failures = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().LoginAudits.CountAsync(l => l.Email == "lock@cnx.test" && !l.Succeeded));
        Assert.Equal(6, failures);
    }

    [Fact]
    public async Task Ip_allow_list_is_enforced_for_privileged_accounts()
    {
        await AddPlatformUserAsync("ip@cnx.test", "Sup3rSecret!", PlatformRole.SuperAdmin, "203.0.113.10");
        Assert.Equal(AuthStatus.IpNotAllowed, (await PlatformSignIn("ip@cnx.test", "Sup3rSecret!", ip: "10.0.0.1")).Status);
        Assert.True((await PlatformSignIn("ip@cnx.test", "Sup3rSecret!", ip: "203.0.113.10")).Succeeded);
    }

    [Fact]
    public async Task Two_factor_is_required_once_enabled()
    {
        var id = await AddPlatformUserAsync("tfa@cnx.test", "Sup3rSecret!");
        _host.Context.AsPlatform();
        _host.Context.User = id;
        var (secret, _) = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformUserService>().BeginTwoFactorSetupAsync(id));
        string CodeNow() => TotpService.Compute(TotpService.FromBase32(secret), new DateTimeOffset(_host.Clock.UtcNow).ToUnixTimeSeconds() / 30);
        Assert.True((await _host.RunAsync(sp => sp.GetRequiredService<IPlatformUserService>().ConfirmTwoFactorSetupAsync(id, secret, CodeNow()))).Succeeded);
        _host.Context.AsAnonymous();

        var step1 = await PlatformSignIn("tfa@cnx.test", "Sup3rSecret!");
        Assert.Equal(AuthStatus.RequiresTwoFactor, step1.Status);
        Assert.Null(step1.Principal);

        var bad = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformAuthService>().CompleteTwoFactorAsync(step1.PendingUserId!.Value, "000000", "10.0.0.1", "tests"));
        Assert.Equal(AuthStatus.InvalidTwoFactorCode, bad.Status);

        var ok = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformAuthService>().CompleteTwoFactorAsync(step1.PendingUserId!.Value, CodeNow(), "10.0.0.1", "tests"));
        Assert.True(ok.Succeeded);
    }

    [Fact]
    public async Task Tenant_sign_in_is_scoped_to_company_code()
    {
        var org = await _host.ProvisionAsync("acme", password: "Passw0rd!x");
        await _host.ProvisionAsync("globex", password: "Passw0rd!x");
        _host.Context.AsAnonymous();
        var auth = (Func<IServiceProvider, ITenantAuthService>)(sp => sp.GetRequiredService<ITenantAuthService>());

        var ok = await _host.RunAsync(sp => auth(sp).SignInAsync("ACME", "admin@acme.test", "Passw0rd!x", "1.1.1.1", "tests"));
        Assert.True(ok.Succeeded);
        Assert.Equal(org, ok.Principal!.OrganizationId);
        Assert.Equal(PrincipalType.Tenant, ok.Principal.Type);

        // Correct credentials for acme's admin, but presented against another company's code.
        var cross = await _host.RunAsync(sp => auth(sp).SignInAsync("globex", "admin@acme.test", "Passw0rd!x", "1.1.1.1", "tests"));
        Assert.False(cross.Succeeded);
    }

    [Fact]
    public async Task Suspended_or_expired_organization_cannot_sign_in()
    {
        var org = await _host.ProvisionAsync("frozen", password: "Passw0rd!x");
        _host.Context.AsPlatform();
        Assert.True((await _host.RunAsync(sp => sp.GetRequiredService<IOrganizationService>().ChangeStatusAsync(org, OrganizationStatus.Suspended, "Test"))).Succeeded);
        _host.Context.AsAnonymous();

        var r = await _host.RunAsync(sp => sp.GetRequiredService<ITenantAuthService>().SignInAsync("frozen", "admin@frozen.test", "Passw0rd!x", null, null));
        Assert.Equal(AuthStatus.OrganizationUnavailable, r.Status);
    }

    [Fact]
    public async Task Changed_security_stamp_invalidates_existing_sessions()
    {
        var id = await AddPlatformUserAsync("stamp@cnx.test", "Sup3rSecret!");
        var first = await PlatformSignIn("stamp@cnx.test", "Sup3rSecret!");
        _host.Context.AsPlatform();
        _host.Context.User = 999; // another admin edits this user
        var input = (await _host.RunAsync(sp => sp.GetRequiredService<IPlatformUserService>().GetForEditAsync(id)))!;
        input.IsActive = false;
        Assert.True((await _host.RunAsync(sp => sp.GetRequiredService<IPlatformUserService>().UpdateAsync(id, input))).Succeeded);

        var refreshed = await _host.RunAsync(sp => sp.GetRequiredService<IPlatformAuthService>().RefreshAsync(id, first.Principal!.SecurityStamp));
        Assert.Null(refreshed);
    }

    [Fact]
    public async Task Organization_management_requires_platform_principal()
    {
        var org = await _host.ProvisionAsync("guard");
        _host.Context.AsTenant(org);
        await Assert.ThrowsAsync<Application.Common.TenantIsolationException>(() =>
            _host.RunAsync(sp => sp.GetRequiredService<IOrganizationService>().ListAsync(new OrganizationListQuery())));
    }
}
