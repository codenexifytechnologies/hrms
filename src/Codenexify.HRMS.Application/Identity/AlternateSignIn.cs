using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Codenexify.HRMS.Application.Identity;

/// <summary>Sends one-time codes by SMS (MSG91 in production).</summary>
public interface ISmsSender
{
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);
    /// <param name="mobile">Indian 10-digit mobile number.</param>
    Task<bool> SendOtpAsync(string mobile, string code, CancellationToken ct = default);
}

public static class SsoProviders
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    public static bool IsKnown(string? p) => p is Google or Microsoft;
}

/// <summary>Which alternative sign-in methods an organization offers on its login page.</summary>
public sealed record SignInOptions(bool Google, bool Microsoft, bool MobileOtp)
{
    public static readonly SignInOptions None = new(false, false, false);
    public bool Any => Google || Microsoft || MobileOtp;
}

public sealed class SignInSettingsInput
{
    public bool SsoGoogle { get; set; }
    public bool SsoMicrosoft { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(300)] public string? SsoDomains { get; set; }
    public bool MobileOtpLogin { get; set; }
}

public sealed record SignInSettingsView(SignInSettingsInput Settings, bool SsoInPlan, bool OtpInPlan, bool GoogleReady, bool MicrosoftReady, bool SmsReady, int UsersWithoutMobile);

/// <summary>Organization admin settings for single sign-on and mobile OTP sign-in.</summary>
public interface ISignInSettingsService
{
    Task<SignInSettingsView> GetAsync(CancellationToken ct = default);
    Task<Result> SaveAsync(SignInSettingsInput input, CancellationToken ct = default);
}

public sealed class SignInSettingsService(IAppDbContext db, ICurrentContext context, IFeatureAccessService features, ISmsSender sms) : ISignInSettingsService
{
    public static IReadOnlyList<string> ParseDomains(string? csv) =>
        (csv ?? "").Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.TrimStart('@').ToLowerInvariant()).Distinct().ToList();

    public async Task<SignInSettingsView> GetAsync(CancellationToken ct = default)
    {
        var f = await features.GetEnabledFeaturesAsync(context.OrgId(), ct);
        var s = await db.OrganizationSettings.AsNoTracking().FirstAsync(ct);
        var p = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var withoutMobile = await db.Users.CountAsync(u => u.IsActive && (u.Phone == null || u.Phone == ""), ct);
        return new SignInSettingsView(new SignInSettingsInput { SsoGoogle = s.SsoGoogle, SsoMicrosoft = s.SsoMicrosoft, SsoDomains = s.SsoDomains, MobileOtpLogin = s.MobileOtpLogin },
            f.Contains(FeatureCodes.SingleSignOn), f.Contains(FeatureCodes.MobileOtp),
            p is { GoogleClientId: not null, GoogleClientSecretProtected: not null }, p is { MicrosoftClientId: not null, MicrosoftClientSecretProtected: not null },
            await sms.IsConfiguredAsync(ct), withoutMobile);
    }

    public async Task<Result> SaveAsync(SignInSettingsInput i, CancellationToken ct = default)
    {
        var view = await GetAsync(ct);
        var domains = ParseDomains(i.SsoDomains);
        if ((i.SsoGoogle || i.SsoMicrosoft) && !view.SsoInPlan) return Result.Fail("Single sign-on is not included in your plan.");
        if (i.MobileOtpLogin && !view.OtpInPlan) return Result.Fail("Mobile OTP sign-in is not included in your plan.");
        if (i.SsoGoogle && !view.GoogleReady) return Result.Fail("Google sign-in isn't set up on the platform yet. Contact Codenexify support.");
        if (i.SsoMicrosoft && !view.MicrosoftReady) return Result.Fail("Microsoft sign-in isn't set up on the platform yet. Contact Codenexify support.");
        if (i.MobileOtpLogin && !view.SmsReady) return Result.Fail("SMS codes aren't set up on the platform yet. Contact Codenexify support.");
        if ((i.SsoGoogle || i.SsoMicrosoft) && domains.Count == 0) return Result.Fail("List your company email domain(s), e.g. acme.com — only those accounts can use single sign-on.");
        if (domains.Any(d => !d.Contains('.') || d.Length > 100 || d.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-'))))
            return Result.Fail("Enter plain domains like acme.com, separated by commas.");
        if (domains.Any(d => d is "gmail.com" or "googlemail.com" or "outlook.com" or "hotmail.com" or "live.com" or "yahoo.com"))
            return Result.Fail("Public email domains can't be used for single sign-on; use your company's own domain.");
        var s = await db.OrganizationSettings.FirstAsync(ct);
        s.SsoGoogle = i.SsoGoogle; s.SsoMicrosoft = i.SsoMicrosoft; s.SsoDomains = domains.Count == 0 ? null : string.Join(",", domains); s.MobileOtpLogin = i.MobileOtpLogin;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

/// <summary>Single sign-on and mobile OTP sign-in for organization users. Accounts are never created here: the user must already exist.</summary>
public interface IAlternateSignInService
{
    Task<SignInOptions> OptionsAsync(string organizationCode, CancellationToken ct = default);
    /// <param name="email">Email from a validated ID token; only accepted when the provider marked it verified.</param>
    Task<AuthResult> SignInExternalAsync(string organizationCode, string provider, string email, bool emailVerified, string? ip, string? userAgent, CancellationToken ct = default);
    /// <summary>Sends a code when the number belongs to exactly one active user; the reply never reveals whether it does.</summary>
    Task RequestOtpAsync(string organizationCode, string mobile, string? ip, CancellationToken ct = default);
    Task<AuthResult> SignInWithOtpAsync(string organizationCode, string mobile, string code, string? ip, string? userAgent, CancellationToken ct = default);
}

public sealed class AlternateSignInService(IAppDbContext db, ICurrentContext context, IClock clock, IFeatureAccessService features, ISmsSender sms,
    IOptions<SecurityOptions> options) : IAlternateSignInService
{
    private const int OtpMinutes = 5;
    private const int OtpAttempts = 5;
    private const int OtpSendsPer15Min = 3;

    public static string? NormalizeMobile(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
        return digits.Length == 10 && digits[0] is >= '6' and <= '9' ? digits : null;
    }

    private static string HashCode(int userId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"login:{userId}:{code.Trim()}"))).ToLowerInvariant();

    private async Task<Organization?> OrgAsync(string code, CancellationToken ct)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        return c.Length == 0 ? null : await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Code == c, ct);
    }

    public async Task<SignInOptions> OptionsAsync(string organizationCode, CancellationToken ct = default)
    {
        var org = await OrgAsync(organizationCode, ct);
        if (org is null || !org.IsOperational) return SignInOptions.None;
        var f = await features.GetEnabledFeaturesAsync(org.Id, ct);
        using var scope = context.BeginOrganizationScope(org.Id);
        var s = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var p = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null) return SignInOptions.None;
        var sso = f.Contains(FeatureCodes.SingleSignOn);
        return new SignInOptions(
            sso && s.SsoGoogle && p is { GoogleClientId: not null, GoogleClientSecretProtected: not null },
            sso && s.SsoMicrosoft && p is { MicrosoftClientId: not null, MicrosoftClientSecretProtected: not null },
            f.Contains(FeatureCodes.MobileOtp) && s.MobileOtpLogin && await sms.IsConfiguredAsync(ct));
    }

    private void Audit(int? orgId, int? userId, string email, bool ok, string? reason, string? ip, string? ua) =>
        db.LoginAudits.Add(new LoginAudit { TimestampUtc = clock.UtcNow, PrincipalType = PrincipalType.Tenant, OrganizationId = orgId, UserId = userId, Email = email, Succeeded = ok, FailureReason = reason, IpAddress = ip, UserAgent = PlatformAuthService.Trim(ua) });

    private async Task<AuthResult> CompleteAsync(Organization org, Domain.Identity.User user, string method, string? ip, string? ua, CancellationToken ct)
    {
        if (!user.IsActive) { Audit(org.Id, user.Id, user.Email, false, $"Disabled ({method})", ip, ua); await db.SaveChangesAsync(ct); return new AuthResult(AuthStatus.Disabled, Message: "Your account is disabled. Contact your HR administrator."); }
        if (user.LockoutEndUtc > clock.UtcNow) { Audit(org.Id, user.Id, user.Email, false, $"Locked out ({method})", ip, ua); await db.SaveChangesAsync(ct); return new AuthResult(AuthStatus.LockedOut, Message: "Account temporarily locked after repeated failures. Try again later."); }
        if (!org.IsOperational) { Audit(org.Id, user.Id, user.Email, false, $"Organization {org.Status}", ip, ua); await db.SaveChangesAsync(ct); return new AuthResult(AuthStatus.OrganizationUnavailable, Message: TenantAuthService.OrganizationMessage(org.Status)); }
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = clock.UtcNow;
        Audit(org.Id, user.Id, user.Email, true, method, ip, ua);
        await db.SaveChangesAsync(ct);
        return new AuthResult(AuthStatus.Success, TenantAuthService.ToPrincipal(user, org));
    }

    private IQueryable<Domain.Identity.User> UsersWithRoles =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission);

    public async Task<AuthResult> SignInExternalAsync(string organizationCode, string provider, string email, bool emailVerified, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var fail = new AuthResult(AuthStatus.InvalidCredentials, Message: "This account can't sign in with single sign-on. Use your password or ask HR.");
        var org = await OrgAsync(organizationCode, ct);
        var options = await OptionsAsync(organizationCode, ct);
        var allowed = provider == SsoProviders.Google ? options.Google : provider == SsoProviders.Microsoft && options.Microsoft;
        if (org is null || !allowed) { Audit(org?.Id, null, normalized, false, $"SSO {provider} not enabled", ip, userAgent); await db.SaveChangesAsync(ct); return fail; }

        using var scope = context.BeginOrganizationScope(org.Id);
        var settings = await db.OrganizationSettings.AsNoTracking().FirstAsync(ct);
        var domain = normalized.Contains('@') ? normalized[(normalized.LastIndexOf('@') + 1)..] : "";
        if (!emailVerified || !SignInSettingsService.ParseDomains(settings.SsoDomains).Contains(domain))
        {
            Audit(org.Id, null, normalized, false, emailVerified ? $"SSO {provider}: domain not allowed" : $"SSO {provider}: email not verified", ip, userAgent);
            await db.SaveChangesAsync(ct);
            return fail;
        }
        var user = await UsersWithRoles.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null) { Audit(org.Id, null, normalized, false, $"SSO {provider}: no such user", ip, userAgent); await db.SaveChangesAsync(ct); return fail; }
        return await CompleteAsync(org, user, $"SSO {provider}", ip, userAgent, ct);
    }

    private async Task<Domain.Identity.User?> UserByMobileAsync(string mobile, CancellationToken ct)
    {
        var candidates = await db.Users.AsNoTracking().Where(u => u.Phone != null && u.Phone != "").Select(u => new { u.Id, u.Phone }).ToListAsync(ct);
        var matches = candidates.Where(c => NormalizeMobile(c.Phone) == mobile).Select(c => c.Id).ToList();
        // A number shared by several logins is ambiguous: refuse rather than guess.
        return matches.Count == 1 ? await UsersWithRoles.FirstAsync(u => u.Id == matches[0], ct) : null;
    }

    public async Task RequestOtpAsync(string organizationCode, string mobile, string? ip, CancellationToken ct = default)
    {
        var number = NormalizeMobile(mobile);
        var org = await OrgAsync(organizationCode, ct);
        if (number is null || org is null || !(await OptionsAsync(organizationCode, ct)).MobileOtp) return;
        using var scope = context.BeginOrganizationScope(org.Id);
        var user = await UserByMobileAsync(number, ct);
        if (user is null || !user.IsActive) return;
        var since = clock.UtcNow.AddMinutes(-15);
        if (await db.LoginOtps.CountAsync(o => o.UserId == user.Id && o.CreatedAtUtc >= since, ct) >= OtpSendsPer15Min) return;

        // Older unused codes stop working when a new one is sent.
        foreach (var old in await db.LoginOtps.Where(o => o.UserId == user.Id && o.ConsumedAtUtc == null && o.ExpiresAtUtc > clock.UtcNow).ToListAsync(ct))
            old.ConsumedAtUtc = clock.UtcNow;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000");
        db.LoginOtps.Add(new LoginOtp { UserId = user.Id, CodeHash = HashCode(user.Id, code), ExpiresAtUtc = clock.UtcNow.AddMinutes(OtpMinutes), IpAddress = ip });
        await db.SaveChangesAsync(ct);
        await sms.SendOtpAsync(number, code, ct);
    }

    public async Task<AuthResult> SignInWithOtpAsync(string organizationCode, string mobile, string code, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var fail = new AuthResult(AuthStatus.InvalidCredentials, Message: "That code is not right or has expired.");
        var number = NormalizeMobile(mobile);
        var org = await OrgAsync(organizationCode, ct);
        if (number is null || org is null || string.IsNullOrWhiteSpace(code) || !(await OptionsAsync(organizationCode, ct)).MobileOtp) return fail;
        using var scope = context.BeginOrganizationScope(org.Id);
        var user = await UserByMobileAsync(number, ct);
        if (user is null) { Audit(org.Id, null, $"mobile:{number[..2]}••••{number[^2..]}", false, "OTP: unknown number", ip, userAgent); await db.SaveChangesAsync(ct); return fail; }
        var otp = await db.LoginOtps.Where(o => o.UserId == user.Id && o.ConsumedAtUtc == null && o.ExpiresAtUtc > clock.UtcNow).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        if (otp is null || user.LockoutEndUtc > clock.UtcNow) { Audit(org.Id, user.Id, user.Email, false, "OTP: no valid code", ip, userAgent); await db.SaveChangesAsync(ct); return fail; }
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashCode(user.Id, code)), Encoding.ASCII.GetBytes(otp.CodeHash));
        if (!ok)
        {
            otp.Attempts++;
            if (otp.Attempts >= OtpAttempts) otp.ConsumedAtUtc = clock.UtcNow;
            var tracked = await db.Users.FirstAsync(u => u.Id == user.Id, ct);
            tracked.FailedLoginCount++;
            if (tracked.FailedLoginCount >= options.Value.MaxFailedLogins) { tracked.LockoutEndUtc = clock.UtcNow.AddMinutes(options.Value.LockoutMinutes); tracked.FailedLoginCount = 0; }
            Audit(org.Id, user.Id, user.Email, false, "OTP: wrong code", ip, userAgent);
            await db.SaveChangesAsync(ct);
            return fail;
        }
        otp.ConsumedAtUtc = clock.UtcNow;
        return await CompleteAsync(org, user, "Mobile OTP", ip, userAgent, ct);
    }
}
