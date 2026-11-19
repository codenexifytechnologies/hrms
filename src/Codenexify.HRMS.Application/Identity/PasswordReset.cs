using System.Net;
using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Application.Identity;

/// <summary>What the "choose a new password" page may show about a valid link.</summary>
public sealed record PasswordResetLink(PrincipalType Type, string MaskedEmail, string? OrganizationName, string? OrganizationCode);

public interface IPasswordResetService
{
    /// <summary>Emails a reset link when the company code + email match an active login. Never reveals whether they do.</summary>
    Task RequestForCompanyUserAsync(string organizationCode, string email, string baseUrl, string? ip, CancellationToken ct = default);

    /// <summary>Emails a reset link when the email matches an active Control Center user. Never reveals whether it does.</summary>
    Task RequestForPlatformUserAsync(string email, string baseUrl, string? ip, CancellationToken ct = default);

    /// <summary>Null when the link is unknown, used, replaced or expired.</summary>
    Task<PasswordResetLink?> CheckAsync(string token, CancellationToken ct = default);

    /// <summary>Sets the new password, ends every signed-in session, clears a lockout and uses up the link.</summary>
    Task<Result<PasswordResetLink>> ResetAsync(string token, string newPassword, string? ip = null, CancellationToken ct = default);
}

public sealed class PasswordResetService(
    IAppDbContext db, ICurrentContext context, IPasswordService passwords, IClock clock, IEmailSender email,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    public const int LinkMinutes = 30;
    public const int MaxRequestsPerHour = 3;
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 100;

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("pwreset:" + token.Trim()))).ToLowerInvariant();

    private static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string MaskEmail(string address)
    {
        var at = address.IndexOf('@');
        if (at <= 0) return "•••";
        var name = address[..at];
        return (name.Length <= 2 ? name[..1] : name[..2]) + new string('•', Math.Max(3, name.Length - 2)) + address[at..];
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public async Task RequestForCompanyUserAsync(string organizationCode, string address, string baseUrl, string? ip, CancellationToken ct = default)
    {
        var code = (organizationCode ?? string.Empty).Trim().ToLowerInvariant();
        var normalized = (address ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0 || normalized.Length == 0) return;
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Code == code, ct);
        if (org is null) return;

        using var scope = context.BeginOrganizationScope(org.Id);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !user.IsActive) return;
        var token = await IssueAsync(PrincipalType.Tenant, org.Id, user.Id, ip, ct);
        if (token is null) return;

        var link = $"{baseUrl.TrimEnd('/')}/reset-password?token={token}";
        await email.SendAsync(new EmailMessage(user.Email, $"Reset your {org.Name} HRMS password",
            $"<p>Hello {E(user.FullName)},</p>" +
            $"<p>We received a request to reset your password for <strong>{E(org.Name)}</strong> (company code <strong>{E(org.Code)}</strong>).</p>" +
            $"<p><a href=\"{E(link)}\">Choose a new password</a></p>" +
            $"<p>This link works once and expires in {LinkMinutes} minutes. If you didn't ask for it, ignore this email: your password stays the same.</p>",
            "PasswordReset", org.Id), ct);
    }

    public async Task RequestForPlatformUserAsync(string address, string baseUrl, string? ip, CancellationToken ct = default)
    {
        var normalized = (address ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0) return;
        var user = await db.PlatformUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !user.IsActive || !PlatformPermissionResolver.IsIpAllowed(user.AllowedIps, ip)) return;
        var token = await IssueAsync(PrincipalType.Platform, null, user.Id, ip, ct);
        if (token is null) return;

        var link = $"{baseUrl.TrimEnd('/')}/superadmin/account/reset-password?token={token}";
        await email.SendAsync(new EmailMessage(user.Email, "Reset your Control Center password",
            $"<p>Hello {E(user.FullName)},</p>" +
            "<p>We received a request to reset your Codenexify HRMS Control Center password.</p>" +
            $"<p><a href=\"{E(link)}\">Choose a new password</a></p>" +
            $"<p>This link works once and expires in {LinkMinutes} minutes. Two-step sign-in, if you use it, still applies. " +
            "If you didn't ask for this, ignore this email and tell the other Super Admins.</p>",
            "PasswordReset"), ct);
    }

    /// <summary>Creates a link unless the hourly limit is reached; older unused links stop working.</summary>
    private async Task<string?> IssueAsync(PrincipalType type, int? orgId, int userId, string? ip, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hourAgo = now.AddHours(-1);
        var mine = db.PasswordResetTokens.Where(t => t.PrincipalType == type && t.UserId == userId && t.OrganizationId == orgId);
        if (await mine.CountAsync(t => t.CreatedAtUtc >= hourAgo, ct) >= MaxRequestsPerHour)
        {
            logger.LogWarning("Password reset limit reached for {Type} user {UserId}", type, userId);
            return null;
        }
        foreach (var old in await mine.Where(t => t.UsedAtUtc == null && t.ExpiresAtUtc > now).ToListAsync(ct)) old.UsedAtUtc = now;

        var token = NewToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            PrincipalType = type, OrganizationId = orgId, UserId = userId, TokenHash = HashToken(token),
            CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(LinkMinutes), RequestIp = ip
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    private async Task<PasswordResetToken?> FindAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = HashToken(token);
        var now = clock.UtcNow;
        return await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAtUtc == null && t.ExpiresAtUtc > now, ct);
    }

    public async Task<PasswordResetLink?> CheckAsync(string token, CancellationToken ct = default)
    {
        var row = await FindAsync(token, ct);
        if (row is null) return null;
        if (row.PrincipalType == PrincipalType.Platform)
        {
            var p = await db.PlatformUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == row.UserId, ct);
            return p is { IsActive: true } ? new PasswordResetLink(PrincipalType.Platform, MaskEmail(p.Email), null, null) : null;
        }
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == row.OrganizationId, ct);
        if (org is null) return null;
        using var scope = context.BeginOrganizationScope(org.Id);
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.UserId, ct);
        return u is { IsActive: true } ? new PasswordResetLink(PrincipalType.Tenant, MaskEmail(u.Email), org.Name, org.Code) : null;
    }

    public async Task<Result<PasswordResetLink>> ResetAsync(string token, string newPassword, string? ip = null, CancellationToken ct = default)
    {
        const string expired = "This link has expired or was already used. Ask for a new one.";
        newPassword ??= string.Empty;
        if (newPassword.Length < MinPasswordLength) return Result<PasswordResetLink>.Fail($"The new password must be at least {MinPasswordLength} characters.");
        if (newPassword.Length > MaxPasswordLength) return Result<PasswordResetLink>.Fail($"The new password can be at most {MaxPasswordLength} characters.");

        var link = await CheckAsync(token, ct);
        var row = await FindAsync(token, ct);
        if (link is null || row is null) return Result<PasswordResetLink>.Fail(expired);
        var now = clock.UtcNow;

        string to, name;
        if (row.PrincipalType == PrincipalType.Platform)
        {
            var p = await db.PlatformUsers.FirstAsync(u => u.Id == row.UserId, ct);
            if (!PlatformPermissionResolver.IsIpAllowed(p.AllowedIps, ip)) return Result<PasswordResetLink>.Fail("Password changes are not allowed from this network.");
            if (passwords.Verify(p.PasswordHash, newPassword).Verified) return Result<PasswordResetLink>.Fail("Choose a password different from your current one.");
            p.PasswordHash = passwords.Hash(newPassword);
            p.MustChangePassword = false;
            p.SecurityStamp = Guid.NewGuid().ToString("N");
            p.FailedLoginCount = 0;
            p.LockoutEndUtc = null;
            (to, name) = (p.Email, p.FullName);
            Finish(row, now, p.Email, ip, null);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            using var scope = context.BeginOrganizationScope(row.OrganizationId!.Value);
            var u = await db.Users.FirstAsync(x => x.Id == row.UserId, ct);
            if (passwords.Verify(u.PasswordHash, newPassword).Verified) return Result<PasswordResetLink>.Fail("Choose a password different from your current one.");
            u.PasswordHash = passwords.Hash(newPassword);
            u.MustChangePassword = false;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.FailedLoginCount = 0;
            u.LockoutEndUtc = null;
            (to, name) = (u.Email, u.FullName);
            Finish(row, now, u.Email, ip, row.OrganizationId);
            await db.SaveChangesAsync(ct);
        }

        await email.SendAsync(new EmailMessage(to, "Your password was changed",
            $"<p>Hello {E(name)},</p><p>Your {(link.OrganizationName is { } o ? E(o) + " HRMS" : "Codenexify HRMS Control Center")} password was changed " +
            $"using a reset link on {now:dd MMM yyyy HH:mm} UTC{(ip is null ? "" : " from " + E(ip))}. You were signed out everywhere else.</p>" +
            "<p>If this wasn't you, contact your administrator immediately.</p>", "PasswordReset", row.OrganizationId), ct);
        return Result<PasswordResetLink>.Ok(link);
    }

    /// <summary>Uses up this link and any other open link of the same user, and records the change in the login history.</summary>
    private void Finish(PasswordResetToken row, DateTime now, string address, string? ip, int? orgId)
    {
        row.UsedAtUtc = now;
        foreach (var other in db.PasswordResetTokens.Where(t => t.PrincipalType == row.PrincipalType && t.UserId == row.UserId
                     && t.OrganizationId == row.OrganizationId && t.Id != row.Id && t.UsedAtUtc == null).ToList())
            other.UsedAtUtc = now;
        db.LoginAudits.Add(new LoginAudit
        {
            TimestampUtc = now, PrincipalType = row.PrincipalType, OrganizationId = orgId, UserId = row.UserId, Email = address,
            Succeeded = true, FailureReason = "Password reset by email link", IpAddress = ip
        });
    }
}
