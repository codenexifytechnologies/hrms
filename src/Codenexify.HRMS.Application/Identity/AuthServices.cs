using System.Net;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Codenexify.HRMS.Application.Identity;

public sealed class SecurityOptions
{
    public const string Section = "Security";
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public string TotpIssuer { get; set; } = "Codenexify HRMS";
}

public enum AuthStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    Disabled,
    IpNotAllowed,
    RequiresTwoFactor,
    InvalidTwoFactorCode,
    OrganizationUnavailable
}

public sealed record AuthenticatedPrincipal(
    PrincipalType Type, int UserId, string Email, string FullName, string SecurityStamp,
    int? OrganizationId, string? OrganizationCode, string? OrganizationName, string? Role,
    IReadOnlyCollection<string> Permissions, bool MustChangePassword);

public sealed record AuthResult(AuthStatus Status, AuthenticatedPrincipal? Principal = null, string? Message = null, int? PendingUserId = null)
{
    public bool Succeeded => Status == AuthStatus.Success;
}

public interface IPlatformAuthService
{
    /// <summary>Validates password; returns RequiresTwoFactor when a TOTP code is needed but not supplied.</summary>
    Task<AuthResult> SignInAsync(string email, string password, string? totpCode, string? ip, string? userAgent, CancellationToken ct = default);
    /// <summary>Second step after a password was verified (<see cref="AuthResult.PendingUserId"/>).</summary>
    Task<AuthResult> CompleteTwoFactorAsync(int userId, string code, string? ip, string? userAgent, CancellationToken ct = default);
    /// <summary>Rebuilds the principal for cookie validation; null if the account was disabled or its stamp changed.</summary>
    Task<AuthenticatedPrincipal?> RefreshAsync(int userId, string securityStamp, CancellationToken ct = default);
}

public interface ITenantAuthService
{
    Task<AuthResult> SignInAsync(string organizationCode, string email, string password, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthenticatedPrincipal?> RefreshAsync(int organizationId, int userId, string securityStamp, CancellationToken ct = default);
}

public static class PlatformPermissionResolver
{
    public static IReadOnlyCollection<string> Effective(PlatformUser user) =>
        PlatformPermissions.ForRole(user.Role).Union(user.Permissions.Select(p => p.Permission)).ToArray();

    public static bool IsIpAllowed(string? allowList, string? ip)
    {
        if (string.IsNullOrWhiteSpace(allowList)) return true;
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var addr)) return false;
        if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
        return allowList.Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => IPAddress.TryParse(entry, out var allowed) && allowed.Equals(addr));
    }
}

public sealed class PlatformAuthService(
    IAppDbContext db, IPasswordService passwords, ITotpService totp, ISecretProtector protector,
    IClock clock, IOptions<SecurityOptions> options) : IPlatformAuthService
{
    public async Task<AuthResult> SignInAsync(string email, string password, string? totpCode, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await db.PlatformUsers.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Email == normalized, ct);
        var now = clock.UtcNow;

        AuthResult Fail(AuthStatus status, string reason, string message)
        {
            db.LoginAudits.Add(new LoginAudit { TimestampUtc = now, PrincipalType = PrincipalType.Platform, UserId = user?.Id, Email = normalized, Succeeded = false, FailureReason = reason, IpAddress = ip, UserAgent = Trim(userAgent) });
            return new AuthResult(status, Message: message);
        }

        AuthResult result;
        if (user is null)
        {
            passwords.Verify(DummyHash, password ?? string.Empty); // equalise timing for unknown accounts
            result = Fail(AuthStatus.InvalidCredentials, "Unknown account", "Invalid email or password.");
        }
        else if (user.LockoutEndUtc > now)
            result = Fail(AuthStatus.LockedOut, "Locked out", $"Account locked until {user.LockoutEndUtc:HH:mm} UTC after repeated failures.");
        else if (!passwords.Verify(user.PasswordHash, password ?? string.Empty).Verified)
        {
            RegisterFailure(user, now);
            result = Fail(AuthStatus.InvalidCredentials, "Wrong password", "Invalid email or password.");
        }
        else if (!user.IsActive)
            result = Fail(AuthStatus.Disabled, "Disabled", "This account is disabled.");
        else if (!PlatformPermissionResolver.IsIpAllowed(user.AllowedIps, ip))
            result = Fail(AuthStatus.IpNotAllowed, $"IP {ip} not in allow-list", "Sign-in is not allowed from this network.");
        else if (user.TwoFactorEnabled && string.IsNullOrWhiteSpace(totpCode))
            result = new AuthResult(AuthStatus.RequiresTwoFactor, PendingUserId: user.Id);
        else if (user.TwoFactorEnabled && !totp.Verify(protector.Unprotect(user.TwoFactorSecret!), totpCode!, now))
        {
            RegisterFailure(user, now);
            result = Fail(AuthStatus.InvalidTwoFactorCode, "Wrong 2FA code", "The authentication code is not valid.");
        }
        else
        {
            var check = passwords.Verify(user.PasswordHash, password!);
            if (check.NeedsRehash) user.PasswordHash = passwords.Hash(password!);
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.LastLoginAtUtc = now;
            user.LastLoginIp = ip;
            db.LoginAudits.Add(new LoginAudit { TimestampUtc = now, PrincipalType = PrincipalType.Platform, UserId = user.Id, Email = normalized, Succeeded = true, IpAddress = ip, UserAgent = Trim(userAgent) });
            result = new AuthResult(AuthStatus.Success, ToPrincipal(user));
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> CompleteTwoFactorAsync(int userId, string code, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == userId, ct);
        var now = clock.UtcNow;
        if (user is null || !user.TwoFactorEnabled || user.TwoFactorSecret is null)
            return new AuthResult(AuthStatus.InvalidCredentials, Message: "Sign-in session is no longer valid. Please sign in again.");

        AuthResult result;
        if (!user.IsActive) result = new AuthResult(AuthStatus.Disabled, Message: "This account is disabled.");
        else if (user.LockoutEndUtc > now) result = new AuthResult(AuthStatus.LockedOut, Message: $"Account locked until {user.LockoutEndUtc:HH:mm} UTC after repeated failures.");
        else if (!PlatformPermissionResolver.IsIpAllowed(user.AllowedIps, ip)) result = new AuthResult(AuthStatus.IpNotAllowed, Message: "Sign-in is not allowed from this network.");
        else if (!totp.Verify(protector.Unprotect(user.TwoFactorSecret), code, now))
        {
            RegisterFailure(user, now);
            result = new AuthResult(AuthStatus.InvalidTwoFactorCode, Message: "The authentication code is not valid.");
        }
        else
        {
            user.FailedLoginCount = 0;
            user.LastLoginAtUtc = now;
            user.LastLoginIp = ip;
            result = new AuthResult(AuthStatus.Success, ToPrincipal(user));
        }

        db.LoginAudits.Add(new LoginAudit
        {
            TimestampUtc = now, PrincipalType = PrincipalType.Platform, UserId = user.Id, Email = user.Email,
            Succeeded = result.Succeeded, FailureReason = result.Succeeded ? null : $"2FA: {result.Status}", IpAddress = ip, UserAgent = Trim(userAgent)
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthenticatedPrincipal?> RefreshAsync(int userId, string securityStamp, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.AsNoTracking().Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive || user.SecurityStamp != securityStamp) return null;
        return ToPrincipal(user);
    }

    private void RegisterFailure(PlatformUser user, DateTime now)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= options.Value.MaxFailedLogins)
        {
            user.LockoutEndUtc = now.AddMinutes(options.Value.LockoutMinutes);
            user.FailedLoginCount = 0;
        }
    }

    private static AuthenticatedPrincipal ToPrincipal(PlatformUser u) => new(
        PrincipalType.Platform, u.Id, u.Email, u.FullName, u.SecurityStamp, null, null, null, u.Role.ToString(),
        PlatformPermissionResolver.Effective(u), u.MustChangePassword);

    internal static string? Trim(string? s) => s is null ? null : s.Length > 500 ? s[..500] : s;

    // Hash of a random value; verifying against it keeps response timing equal for unknown accounts.
    private static string? _dummyHash;
    private string DummyHash => _dummyHash ??= passwords.Hash(Guid.NewGuid().ToString("N"));
}

public sealed class TenantAuthService(
    IAppDbContext db, ICurrentContext context, IPasswordService passwords, IClock clock,
    IOptions<SecurityOptions> options) : ITenantAuthService
{
    public async Task<AuthResult> SignInAsync(string organizationCode, string email, string password, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var code = (organizationCode ?? string.Empty).Trim().ToLowerInvariant();
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var now = clock.UtcNow;
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Code == code, ct);

        if (org is null)
        {
            db.LoginAudits.Add(new LoginAudit { TimestampUtc = now, PrincipalType = PrincipalType.Tenant, Email = normalized, Succeeded = false, FailureReason = $"Unknown organization '{code}'", IpAddress = ip, UserAgent = PlatformAuthService.Trim(userAgent) });
            await db.SaveChangesAsync(ct);
            return new AuthResult(AuthStatus.InvalidCredentials, Message: "Invalid company code, email or password.");
        }

        // The tenant is identified server-side from the verified organization record, never from client claims.
        using var scope = context.BeginOrganizationScope(org.Id);
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);

        AuthResult Fail(AuthStatus status, string reason, string message)
        {
            db.LoginAudits.Add(new LoginAudit { TimestampUtc = now, PrincipalType = PrincipalType.Tenant, OrganizationId = org.Id, UserId = user?.Id, Email = normalized, Succeeded = false, FailureReason = reason, IpAddress = ip, UserAgent = PlatformAuthService.Trim(userAgent) });
            return new AuthResult(status, Message: message);
        }

        AuthResult result;
        if (user is null)
            result = Fail(AuthStatus.InvalidCredentials, "Unknown account", "Invalid company code, email or password.");
        else if (user.LockoutEndUtc > now)
            result = Fail(AuthStatus.LockedOut, "Locked out", "Account temporarily locked after repeated failures. Try again later.");
        else if (!passwords.Verify(user.PasswordHash, password ?? string.Empty).Verified)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= options.Value.MaxFailedLogins)
            {
                user.LockoutEndUtc = now.AddMinutes(options.Value.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            result = Fail(AuthStatus.InvalidCredentials, "Wrong password", "Invalid company code, email or password.");
        }
        else if (!user.IsActive)
            result = Fail(AuthStatus.Disabled, "Disabled", "Your account is disabled. Contact your HR administrator.");
        else if (!org.IsOperational)
            result = Fail(AuthStatus.OrganizationUnavailable, $"Organization {org.Status}", OrganizationMessage(org.Status));
        else
        {
            if (passwords.Verify(user.PasswordHash, password!).NeedsRehash) user.PasswordHash = passwords.Hash(password!);
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.LastLoginAtUtc = now;
            db.LoginAudits.Add(new LoginAudit { TimestampUtc = now, PrincipalType = PrincipalType.Tenant, OrganizationId = org.Id, UserId = user.Id, Email = normalized, Succeeded = true, IpAddress = ip, UserAgent = PlatformAuthService.Trim(userAgent) });
            result = new AuthResult(AuthStatus.Success, ToPrincipal(user, org));
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthenticatedPrincipal?> RefreshAsync(int organizationId, int userId, string securityStamp, CancellationToken ct = default)
    {
        using var scope = context.BeginOrganizationScope(organizationId);
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null || !org.IsOperational) return null;
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive || user.SecurityStamp != securityStamp) return null;
        return ToPrincipal(user, org);
    }

    internal static string OrganizationMessage(OrganizationStatus status) => status switch
    {
        OrganizationStatus.Expired => "Your organization's subscription has expired. Please contact your administrator to renew.",
        OrganizationStatus.Suspended => "Your organization's account is suspended. Please contact support.",
        OrganizationStatus.PendingVerification => "Your organization is pending verification.",
        _ => "Your organization's account is not active."
    };

    internal static AuthenticatedPrincipal ToPrincipal(Domain.Identity.User u, Organization org)
    {
        var roles = u.UserRoles.Select(r => r.Role).Where(r => !r.IsDeleted).ToList();
        var perms = roles.SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code)).Distinct().ToArray();
        return new AuthenticatedPrincipal(PrincipalType.Tenant, u.Id, u.Email, u.FullName, u.SecurityStamp,
            org.Id, org.Code, org.Name, string.Join(", ", roles.Select(r => r.Name)), perms, u.MustChangePassword);
    }
}
