using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Domain.Identity;

/// <summary>
/// A one-time "forgot password" link for a company user or a Control Center user. Only the SHA-256 hash of the
/// random token is stored; the token itself exists only in the email. Not a tenant entity: the link is opened
/// anonymously and the row itself names the organization.
/// </summary>
public class PasswordResetToken
{
    public long Id { get; set; }
    public PrincipalType PrincipalType { get; set; }
    /// <summary>Set for company users; null for Control Center users.</summary>
    public int? OrganizationId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? RequestIp { get; set; }
}
