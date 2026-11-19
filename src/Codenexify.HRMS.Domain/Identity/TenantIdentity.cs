using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Platform;

namespace Codenexify.HRMS.Domain.Identity;

/// <summary>A login account inside a client organization (HR admin, manager, employee, ...).</summary>
public class User : TenantEntity
{
    public Organization Organization { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsOrganizationOwner { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool MustChangePassword { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>User's choice: in-app notifications only, no email copies.</summary>
    public bool MuteEmailNotifications { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>Organization-specific role. System roles are created for every organization and cannot be deleted.</summary>
public class Role : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>Global catalog of tenant permission codes (e.g. "users.manage").</summary>
public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    /// <summary>Feature that must be enabled for this permission to be usable (null = always).</summary>
    public string? FeatureCode { get; set; }
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
