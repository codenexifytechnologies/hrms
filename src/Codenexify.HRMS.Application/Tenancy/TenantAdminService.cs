using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Tenancy;

public sealed class TenantUserInput
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;
    [Phone, StringLength(20)]
    public string? Phone { get; set; }
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    [StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Password")]
    public string? Password { get; set; }
    public List<int> RoleIds { get; set; } = [];
}

public sealed class RoleInput
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [StringLength(300)]
    public string? Description { get; set; }
    public List<int> PermissionIds { get; set; } = [];
}

public sealed record TenantUserRow(int Id, string FullName, string Email, bool IsActive, bool IsOwner, string Roles, DateTime? LastLoginAtUtc);
public sealed record RoleRow(int Id, string Name, string? Description, bool IsSystem, int PermissionCount, int UserCount);

public sealed class SubscriptionOverview
{
    public required string PlanName { get; init; }
    public required OrganizationSubscription Subscription { get; init; }
    public required EffectiveLimits Limits { get; init; }
    public required int UserCount { get; init; }
    public required IReadOnlyList<Feature> EnabledFeatures { get; init; }
}

/// <summary>Organization Admin operations. Every query is confined to the signed-in organization.</summary>
public interface ITenantAdminService
{
    Task<Organization> GetOrganizationAsync(CancellationToken ct = default);
    Task<Result> UpdateProfileAsync(OrganizationEditInput input, CancellationToken ct = default);
    Task<SubscriptionOverview?> GetSubscriptionOverviewAsync(CancellationToken ct = default);

    Task<PagedResult<TenantUserRow>> ListUsersAsync(string? search, bool? active, int page, CancellationToken ct = default);
    Task<TenantUserInput?> GetUserForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateUserAsync(TenantUserInput input, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(int id, TenantUserInput input, CancellationToken ct = default);

    Task<IReadOnlyList<RoleRow>> ListRolesAsync(CancellationToken ct = default);
    Task<RoleInput?> GetRoleForEditAsync(int id, CancellationToken ct = default);
    Task<Role?> GetRoleAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetPermissionCatalogAsync(CancellationToken ct = default);
    Task<Result<int>> CreateRoleAsync(RoleInput input, CancellationToken ct = default);
    Task<Result> UpdateRoleAsync(int id, RoleInput input, CancellationToken ct = default);
    Task<Result> DeleteRoleAsync(int id, CancellationToken ct = default);

    Task<PagedResult<AuditLog>> AuditTrailAsync(int page, CancellationToken ct = default);
    Task<TenantDashboard> GetDashboardAsync(CancellationToken ct = default);
    Task<Result> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);
    Task<TenantUserRow?> GetMeAsync(CancellationToken ct = default);
}

public sealed class TenantDashboard
{
    public required int TotalUsers { get; init; }
    public required int ActiveUsers { get; init; }
    public required int RoleCount { get; init; }
    public required int UsersSignedInLast7Days { get; init; }
    public SubscriptionOverview? Subscription { get; init; }
    public required IReadOnlyList<(string Role, int Users)> UsersByRole { get; init; }
    public required IReadOnlyList<AuditLog> RecentActivity { get; init; }
}

public sealed class TenantAdminService(
    IAppDbContext db, ICurrentContext context, IPasswordService passwords,
    ISubscriptionService subscriptions, IFeatureAccessService featureAccess) : ITenantAdminService
{
    private int OrgId => context.OrganizationId ?? throw new TenantIsolationException("No organization in the current context.");

    public async Task<Organization> GetOrganizationAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == OrgId, ct);

    public async Task<Result> UpdateProfileAsync(OrganizationEditInput input, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstAsync(o => o.Id == OrgId, ct);
        OrganizationService.ApplyEditInput(org, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<SubscriptionOverview?> GetSubscriptionOverviewAsync(CancellationToken ct = default) => SubscriptionOverviewAsync(null, ct);

    private async Task<SubscriptionOverview?> SubscriptionOverviewAsync(int? knownUserCount, CancellationToken ct)
    {
        var sub = await db.OrganizationSubscriptions.AsNoTracking().Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == OrgId && s.IsCurrent, ct);
        if (sub is null) return null;
        var enabled = await featureAccess.GetEnabledFeaturesAsync(OrgId, ct);
        return new SubscriptionOverview
        {
            PlanName = sub.Plan.Name,
            Subscription = sub,
            Limits = await subscriptions.GetLimitsAsync(OrgId, ct),
            UserCount = knownUserCount ?? await db.Users.CountAsync(ct),
            EnabledFeatures = await db.Features.AsNoTracking().Where(f => enabled.Contains(f.Code))
                .OrderBy(f => f.Tier).ThenBy(f => f.SortOrder).ToListAsync(ct)
        };
    }

    public async Task<PagedResult<TenantUserRow>> ListUsersAsync(string? search, bool? active, int page, CancellationToken ct = default)
    {
        const int size = 20;
        page = Math.Max(1, page);
        var q = db.Users.AsNoTracking().AsQueryable(); // tenant filter applied centrally
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.FullName.Contains(search.Trim()) || u.Email.Contains(search.Trim()));
        if (active is { } a) q = q.Where(u => u.IsActive == a);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.FullName).Skip((page - 1) * size).Take(size)
            .Select(u => new TenantUserRow(u.Id, u.FullName, u.Email, u.IsActive, u.IsOrganizationOwner,
                string.Join(", ", u.UserRoles.Select(r => r.Role.Name)), u.LastLoginAtUtc))
            .ToListAsync(ct);
        return new PagedResult<TenantUserRow>(items, total, page, size);
    }

    public async Task<TenantUserInput?> GetUserForEditAsync(int id, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == id, ct);
        return u is null ? null : new TenantUserInput
        {
            FullName = u.FullName, Email = u.Email, Phone = u.Phone, IsActive = u.IsActive,
            RoleIds = u.UserRoles.Select(r => r.RoleId).ToList()
        };
    }

    public async Task<Result<int>> CreateUserAsync(TenantUserInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Password)) return Result<int>.Fail("A password is required for new users.");
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return Result<int>.Fail("A user with this email already exists in your organization.");

        var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
        if (await db.Users.CountAsync(ct) >= limits.MaxEmployees)
            return Result<int>.Fail($"Your plan allows {limits.MaxEmployees} users. Please upgrade your subscription to add more.");

        var roleIds = await ValidRoleIds(input.RoleIds, ct);
        if (roleIds.Count == 0) return Result<int>.Fail("Assign at least one role.");
        if (await RoleAssignmentErrorAsync(roleIds, ct) is { } escalation) return Result<int>.Fail(escalation);

        var user = new User
        {
            Email = email, FullName = input.FullName.Trim(), Phone = input.Phone?.Trim(), IsActive = input.IsActive,
            PasswordHash = passwords.Hash(input.Password), MustChangePassword = true,
            UserRoles = roleIds.Select(r => new UserRole { RoleId = r }).ToList()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(user.Id);
    }

    public async Task<Result> UpdateUserAsync(int id, TenantUserInput input, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result.Fail("User not found.");
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct)) return Result.Fail("Another user already uses this email.");

        var roleIds = await ValidRoleIds(input.RoleIds, ct);
        if (roleIds.Count == 0) return Result.Fail("Assign at least one role.");
        if (await RoleAssignmentErrorAsync(roleIds.Where(r => user.UserRoles.All(ur => ur.RoleId != r)), ct) is { } escalation) return Result.Fail(escalation);
        // Taking roles away from an Organization Admin (or the owner) needs an Organization Admin too.
        if (await ActorPermissionIdsAsync(ct) is not null && id != context.UserId
            && await db.UserRoles.AnyAsync(ur => ur.UserId == id && ur.Role.IsSystem && ur.Role.Name == TenantPermissions.OrgAdminRole, ct))
            return Result.Fail("Only an Organization Admin can change another Organization Admin's account.");
        if (user.IsOrganizationOwner)
        {
            if (!input.IsActive) return Result.Fail("The organization owner cannot be disabled.");
            var adminRoleId = await db.Roles.Where(r => r.Name == TenantPermissions.OrgAdminRole && r.IsSystem).Select(r => r.Id).FirstAsync(ct);
            if (!roleIds.Contains(adminRoleId)) return Result.Fail("The organization owner must keep the Organization Admin role.");
        }
        if (id == context.UserId && !input.IsActive) return Result.Fail("You cannot disable your own account.");

        user.Email = email;
        user.FullName = input.FullName.Trim();
        user.Phone = input.Phone?.Trim();
        user.IsActive = input.IsActive;
        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            user.PasswordHash = passwords.Hash(input.Password);
            user.MustChangePassword = true;
        }
        foreach (var ur in user.UserRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList()) user.UserRoles.Remove(ur);
        foreach (var rid in roleIds.Where(r => user.UserRoles.All(ur => ur.RoleId != r))) user.UserRoles.Add(new UserRole { RoleId = rid });
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>Only roles visible through the tenant filter (i.e. this organization's) are accepted.</summary>
    private async Task<HashSet<int>> ValidRoleIds(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return (await db.Roles.Where(r => list.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task<IReadOnlyList<RoleRow>> ListRolesAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking().OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new RoleRow(r.Id, r.Name, r.Description, r.IsSystem, r.RolePermissions.Count, r.UserRoles.Count))
            .ToListAsync(ct);

    public Task<Role?> GetRoleAsync(int id, CancellationToken ct = default) =>
        db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<RoleInput?> GetRoleForEditAsync(int id, CancellationToken ct = default)
    {
        var r = await db.Roles.AsNoTracking().Include(x => x.RolePermissions).FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : new RoleInput { Name = r.Name, Description = r.Description, PermissionIds = r.RolePermissions.Select(p => p.PermissionId).ToList() };
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionCatalogAsync(CancellationToken ct = default) =>
        await db.Permissions.AsNoTracking().OrderBy(p => p.Module).ThenBy(p => p.Name).ToListAsync(ct);

    public async Task<Result<int>> CreateRoleAsync(RoleInput input, CancellationToken ct = default)
    {
        if (!await featureAccess.IsEnabledAsync(OrgId, FeatureCodes.CustomRoles, ct))
            return Result<int>.Fail("Custom roles are available on the Strength plan and above.");
        var name = input.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name, ct)) return Result<int>.Fail("A role with this name already exists.");
        var permissionIds = await ValidPermissionIds(input.PermissionIds, ct);
        if (await EscalationErrorAsync(permissionIds, ct) is { } escalation) return Result<int>.Fail(escalation);
        var role = new Role { Name = name, Description = input.Description?.Trim() };
        role.RolePermissions = permissionIds.Select(p => new RolePermission { PermissionId = p }).ToList();
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(role.Id);
    }

    public async Task<Result> UpdateRoleAsync(int id, RoleInput input, CancellationToken ct = default)
    {
        var role = await db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Result.Fail("Role not found.");
        if (role.IsSystem && role.Name == TenantPermissions.OrgAdminRole) return Result.Fail("The Organization Admin role always has every permission.");
        if (!role.IsSystem)
        {
            var name = input.Name.Trim();
            if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != id, ct)) return Result.Fail("A role with this name already exists.");
            role.Name = name;
        }
        role.Description = input.Description?.Trim();
        var wanted = await ValidPermissionIds(input.PermissionIds, ct);
        if (await EscalationErrorAsync(wanted.Where(p => role.RolePermissions.All(rp => rp.PermissionId != p)), ct) is { } escalation) return Result.Fail(escalation);
        foreach (var rp in role.RolePermissions.Where(rp => !wanted.Contains(rp.PermissionId)).ToList()) role.RolePermissions.Remove(rp);
        foreach (var pid in wanted.Where(p => role.RolePermissions.All(rp => rp.PermissionId != p))) role.RolePermissions.Add(new RolePermission { PermissionId = pid });

        // Members must sign in again to pick up the new permission set.
        var members = await db.Users.Where(u => u.UserRoles.Any(ur => ur.RoleId == id)).ToListAsync(ct);
        foreach (var m in members) m.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteRoleAsync(int id, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Result.Fail("Role not found.");
        if (role.IsSystem) return Result.Fail("System roles cannot be deleted.");
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct)) return Result.Fail("Remove this role from all users before deleting it.");
        role.IsDeleted = true; // soft delete via interceptor stamps
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>
    /// Permission ids the signed-in user holds through their roles. Organization Admins hold everything.
    /// Used to stop privilege escalation: nobody can grant (through a role or a role assignment) more than they have.
    /// </summary>
    private async Task<HashSet<int>?> ActorPermissionIdsAsync(CancellationToken ct)
    {
        var me = context.UserId;
        if (me is null) return [];
        var roles = await db.UserRoles.Where(ur => ur.UserId == me).Select(ur => ur.Role.Name + "|" + ur.Role.IsSystem).ToListAsync(ct);
        if (roles.Contains($"{TenantPermissions.OrgAdminRole}|True")) return null; // unrestricted
        return (await db.UserRoles.Where(ur => ur.UserId == me).SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.PermissionId)).Distinct().ToListAsync(ct)).ToHashSet();
    }

    private async Task<string?> EscalationErrorAsync(IEnumerable<int> addedPermissionIds, CancellationToken ct)
    {
        var mine = await ActorPermissionIdsAsync(ct);
        if (mine is null) return null;
        var extra = addedPermissionIds.Where(p => !mine.Contains(p)).ToList();
        if (extra.Count == 0) return null;
        var names = await db.Permissions.Where(p => extra.Contains(p.Id)).OrderBy(p => p.Name).Select(p => p.Name).Take(3).ToListAsync(ct);
        return $"You can only grant permissions you hold yourself. Not allowed: {string.Join("; ", names)}{(extra.Count > 3 ? $" and {extra.Count - 3} more" : "")}.";
    }

    /// <summary>Roles newly given to a user must not carry permissions the actor lacks (the Organization Admin role only by Organization Admins).</summary>
    private async Task<string?> RoleAssignmentErrorAsync(IEnumerable<int> addedRoleIds, CancellationToken ct)
    {
        var added = addedRoleIds.ToList();
        if (added.Count == 0) return null;
        var mine = await ActorPermissionIdsAsync(ct);
        if (mine is null) return null;
        if (await db.Roles.AnyAsync(r => added.Contains(r.Id) && r.IsSystem && r.Name == TenantPermissions.OrgAdminRole, ct))
            return "Only an Organization Admin can give someone the Organization Admin role.";
        var needed = await db.RolePermissions.Where(rp => added.Contains(rp.RoleId)).Select(rp => rp.PermissionId).Distinct().ToListAsync(ct);
        return needed.All(mine.Contains) ? null : "You can't assign a role that has permissions you don't hold yourself.";
    }

    private async Task<HashSet<int>> ValidPermissionIds(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return (await db.Permissions.Where(p => list.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task<PagedResult<AuditLog>> AuditTrailAsync(int page, CancellationToken ct = default)
    {
        const int size = 50;
        page = Math.Max(1, page);
        var q = db.AuditLogs.AsNoTracking().Where(a => a.OrganizationId == OrgId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, page, size);
    }

    public async Task<TenantDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var byRole = await db.Roles.AsNoTracking()
            .Select(r => new { r.Name, Users = r.UserRoles.Count })
            .Where(r => r.Users > 0)
            .OrderByDescending(r => r.Users)
            .ToListAsync(ct);
        // All four counts in one round trip (the organization row is just an anchor for the sub-queries).
        var counts = await db.Organizations.AsNoTracking().Where(o => o.Id == OrgId).Select(o => new
        {
            Total = db.Users.Count(),
            Active = db.Users.Count(u => u.IsActive),
            Roles = db.Roles.Count(),
            Recent = db.Users.Count(u => u.LastLoginAtUtc >= since)
        }).FirstAsync(ct);
        return new TenantDashboard
        {
            TotalUsers = counts.Total,
            ActiveUsers = counts.Active,
            RoleCount = counts.Roles,
            UsersSignedInLast7Days = counts.Recent,
            Subscription = await SubscriptionOverviewAsync(counts.Total, ct),
            UsersByRole = byRole.Select(r => (r.Name, r.Users)).ToList(),
            RecentActivity = await db.AuditLogs.AsNoTracking().Where(a => a.OrganizationId == OrgId)
                .OrderByDescending(a => a.Id).Take(8).ToListAsync(ct)
        };
    }

    public async Task<Result> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var id = context.UserId ?? throw new TenantIsolationException("No signed-in user.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
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

    public async Task<TenantUserRow?> GetMeAsync(CancellationToken ct = default)
    {
        var id = context.UserId;
        return await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new TenantUserRow(u.Id, u.FullName, u.Email, u.IsActive, u.IsOrganizationOwner,
                string.Join(", ", u.UserRoles.Select(r => r.Role.Name)), u.LastLoginAtUtc))
            .FirstOrDefaultAsync(ct);
    }
}
