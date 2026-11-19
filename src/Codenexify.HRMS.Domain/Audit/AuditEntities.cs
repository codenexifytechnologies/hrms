using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Domain.Audit;

/// <summary>Immutable record of a data change or privileged action.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int? OrganizationId { get; set; }
    public PrincipalType? ActorType { get; set; }
    public int? ActorId { get; set; }
    public string? ActorName { get; set; }
    /// <summary>Created / Updated / Deleted, or a named business action (e.g. "Subscription.Upgrade").</summary>
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>Every sign-in attempt, successful or not.</summary>
public class LoginAudit
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public PrincipalType PrincipalType { get; set; }
    public int? OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
