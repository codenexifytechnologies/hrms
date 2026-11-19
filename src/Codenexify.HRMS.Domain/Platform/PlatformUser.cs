using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Domain.Platform;

/// <summary>Employee of the platform owner (Codenexify). Never belongs to a client organization.</summary>
public class PlatformUser : BaseEntity, IAuditable
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public PlatformRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    public bool TwoFactorEnabled { get; set; }
    /// <summary>Base32 TOTP secret, protected with ASP.NET Data Protection before storage.</summary>
    public string? TwoFactorSecret { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string? LastLoginIp { get; set; }
    /// <summary>Comma separated IP allow-list. Empty = any IP.</summary>
    public string? AllowedIps { get; set; }
    /// <summary>Changes whenever credentials/permissions change; invalidates existing cookies.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public ICollection<PlatformUserPermission> Permissions { get; set; } = new List<PlatformUserPermission>();
}

public class PlatformUserPermission
{
    public int PlatformUserId { get; set; }
    public PlatformUser PlatformUser { get; set; } = null!;
    public string Permission { get; set; } = string.Empty;
}
