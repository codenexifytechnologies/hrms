using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Identity;

public sealed class PlatformUserInput
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;
    [Phone, StringLength(20)]
    public string? Phone { get; set; }
    [Required]
    public PlatformRole Role { get; set; } = PlatformRole.SupportAdmin;
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    [StringLength(500), Display(Name = "Allowed IP addresses (comma separated, blank = any)")]
    public string? AllowedIps { get; set; }
    /// <summary>Granted in addition to the role defaults.</summary>
    public List<string> ExtraPermissions { get; set; } = [];
    /// <summary>Required on create, optional on edit (blank keeps the current password).</summary>
    [StringLength(100, MinimumLength = 10), DataType(DataType.Password), Display(Name = "Password")]
    public string? Password { get; set; }
}

public sealed record PlatformUserRow(int Id, string FullName, string Email, PlatformRole Role, bool IsActive, bool TwoFactorEnabled, DateTime? LastLoginAtUtc, DateTime? LockoutEndUtc);

public interface IPlatformUserService
{
    Task<IReadOnlyList<PlatformUserRow>> ListAsync(CancellationToken ct = default);
    Task<PlatformUserInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(PlatformUserInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, PlatformUserInput input, CancellationToken ct = default);
    Task<Result> UnlockAsync(int id, CancellationToken ct = default);
    Task<Result> ResetTwoFactorAsync(int id, CancellationToken ct = default);
    Task<Result> ChangeOwnPasswordAsync(int id, string currentPassword, string newPassword, CancellationToken ct = default);
    /// <summary>Begins 2FA enrolment: returns a fresh secret (not yet saved) and its provisioning URI.</summary>
    Task<(string Secret, string Uri)> BeginTwoFactorSetupAsync(int id, CancellationToken ct = default);
    Task<Result> ConfirmTwoFactorSetupAsync(int id, string secret, string code, CancellationToken ct = default);
    Task<Result> DisableOwnTwoFactorAsync(int id, string password, CancellationToken ct = default);
    Task<PagedResult<LoginAudit>> LoginHistoryAsync(PrincipalType? type, bool? succeeded, string? search, int page, CancellationToken ct = default);
}

public sealed class PlatformUserService(
    IAppDbContext db, ICurrentContext context, IPasswordService passwords, ITotpService totp,
    ISecretProtector protector, IClock clock, Microsoft.Extensions.Options.IOptions<SecurityOptions> options) : IPlatformUserService
{
    public async Task<IReadOnlyList<PlatformUserRow>> ListAsync(CancellationToken ct = default) =>
        await db.PlatformUsers.AsNoTracking().OrderBy(u => u.FullName)
            .Select(u => new PlatformUserRow(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.TwoFactorEnabled, u.LastLoginAtUtc, u.LockoutEndUtc))
            .ToListAsync(ct);

    public async Task<PlatformUserInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var u = await db.PlatformUsers.AsNoTracking().Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id, ct);
        return u is null ? null : new PlatformUserInput
        {
            FullName = u.FullName, Email = u.Email, Phone = u.Phone, Role = u.Role, IsActive = u.IsActive,
            AllowedIps = u.AllowedIps, ExtraPermissions = u.Permissions.Select(p => p.Permission).ToList()
        };
    }

    public async Task<Result<int>> CreateAsync(PlatformUserInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Password)) return Result<int>.Fail("A password is required for new users.");
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.PlatformUsers.AnyAsync(u => u.Email == email, ct)) return Result<int>.Fail("A platform user with this email already exists.");
        if (InvalidIps(input.AllowedIps) is { } bad) return Result<int>.Fail($"'{bad}' is not a valid IP address.");

        var user = new PlatformUser { Email = email, PasswordHash = passwords.Hash(input.Password), MustChangePassword = true };
        Apply(user, input);
        db.PlatformUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(user.Id);
    }

    public async Task<Result> UpdateAsync(int id, PlatformUserInput input, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.PlatformUsers.AnyAsync(u => u.Email == email && u.Id != id, ct)) return Result.Fail("Another user already uses this email.");
        if (InvalidIps(input.AllowedIps) is { } bad) return Result.Fail($"'{bad}' is not a valid IP address.");

        if (id == context.UserId && (!input.IsActive || input.Role != user.Role))
            return Result.Fail("You cannot disable your own account or change your own role.");
        if (user.Role == PlatformRole.SuperAdmin && user.IsActive && (input.Role != PlatformRole.SuperAdmin || !input.IsActive)
            && !await db.PlatformUsers.AnyAsync(u => u.Id != id && u.Role == PlatformRole.SuperAdmin && u.IsActive, ct))
            return Result.Fail("At least one active Super Admin must remain.");

        user.Email = email;
        Apply(user, input);
        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            user.PasswordHash = passwords.Hash(input.Password);
            user.MustChangePassword = true;
        }
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // forces re-login with new permissions
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private void Apply(PlatformUser user, PlatformUserInput input)
    {
        user.FullName = input.FullName.Trim();
        user.Phone = input.Phone?.Trim();
        user.Role = input.Role;
        user.IsActive = input.IsActive;
        user.AllowedIps = string.IsNullOrWhiteSpace(input.AllowedIps) ? null : input.AllowedIps.Trim();
        var defaults = PlatformPermissions.ForRole(input.Role);
        var wanted = input.ExtraPermissions.Where(p => PlatformPermissions.Descriptions.ContainsKey(p) && !defaults.Contains(p)).ToHashSet();
        foreach (var p in user.Permissions.Where(p => !wanted.Contains(p.Permission)).ToList()) user.Permissions.Remove(p);
        foreach (var p in wanted.Where(w => user.Permissions.All(x => x.Permission != w)))
            user.Permissions.Add(new PlatformUserPermission { Permission = p });
    }

    private static string? InvalidIps(string? list) =>
        string.IsNullOrWhiteSpace(list) ? null
        : list.Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(ip => !System.Net.IPAddress.TryParse(ip, out _));

    public async Task<Result> UnlockAsync(int id, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        user.LockoutEndUtc = null;
        user.FailedLoginCount = 0;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ResetTwoFactorAsync(int id, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ChangeOwnPasswordAsync(int id, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        if (!passwords.Verify(user.PasswordHash, currentPassword).Verified) return Result.Fail("Current password is incorrect.");
        if (newPassword.Length < 10) return Result.Fail("New password must be at least 10 characters.");
        if (newPassword == currentPassword) return Result.Fail("New password must differ from the current one.");
        user.PasswordHash = passwords.Hash(newPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<(string Secret, string Uri)> BeginTwoFactorSetupAsync(int id, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.AsNoTracking().FirstAsync(u => u.Id == id, ct);
        var secret = totp.GenerateSecret();
        return (secret, totp.BuildProvisioningUri(options.Value.TotpIssuer, user.Email, secret));
    }

    public async Task<Result> ConfirmTwoFactorSetupAsync(int id, string secret, string code, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        if (!totp.Verify(secret, code, clock.UtcNow)) return Result.Fail("The code is not valid. Check the time on your device and try again.");
        user.TwoFactorSecret = protector.Protect(secret);
        user.TwoFactorEnabled = true;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DisableOwnTwoFactorAsync(int id, string password, CancellationToken ct = default)
    {
        var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        if (!passwords.Verify(user.PasswordHash, password).Verified) return Result.Fail("Password is incorrect.");
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<PagedResult<LoginAudit>> LoginHistoryAsync(PrincipalType? type, bool? succeeded, string? search, int page, CancellationToken ct = default)
    {
        const int size = 50;
        page = Math.Max(1, page);
        var q = db.LoginAudits.AsNoTracking().AsQueryable();
        if (type is { } t) q = q.Where(l => l.PrincipalType == t);
        if (succeeded is { } s) q = q.Where(l => l.Succeeded == s);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(l => l.Email.Contains(search.Trim()) || (l.IpAddress != null && l.IpAddress.Contains(search.Trim())));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<LoginAudit>(items, total, page, size);
    }
}
