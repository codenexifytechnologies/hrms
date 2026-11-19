using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Support;

public enum TicketCategory { General, Technical, Billing, Payroll, Account, FeatureRequest }
public enum TicketPriority { Low, Normal, High, Urgent }
public enum TicketStatus { Open, InProgress, WaitingOnCustomer, Resolved, Closed }
public enum TicketAuthor { Customer, Support }
public enum AnnouncementSeverity { Info, Warning, Critical }

/// <summary>A customer's support request. Owned by the organization; worked on by Codenexify staff.</summary>
public class SupportTicket : TenantEntity
{
    public string Subject { get; set; } = string.Empty;
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public int? RaisedByUserId { get; set; }
    public string RaisedByName { get; set; } = string.Empty;
    public string? RaisedByEmail { get; set; }
    public int? AssignedToPlatformUserId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime? FirstResponseAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

public class TicketMessage : TenantEntity
{
    public int TicketId { get; set; }
    public TicketAuthor Author { get; set; }
    public int? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Staff-only note: never shown to the organization.</summary>
    public bool IsInternal { get; set; }
}

/// <summary>Consent given by an organization admin for Codenexify staff to view its data read-only.</summary>
public class SupportAccessGrant : TenantEntity
{
    public int GrantedByUserId { get; set; }
    public string GrantedByName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? TicketId { get; set; }
    public bool IncludePayroll { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedBy { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}

/// <summary>One read-only visit by a staff member under a grant. Visible to the organization.</summary>
public class SupportSession : TenantEntity
{
    public int GrantId { get; set; }
    public SupportAccessGrant Grant { get; set; } = null!;
    public int PlatformUserId { get; set; }
    public string PlatformUserName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int PageViews { get; set; }
}

/// <summary>Platform-wide banner shown in organization portals.</summary>
public class PlatformAnnouncement : BaseEntity, IAuditable
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementSeverity Severity { get; set; }
    /// <summary>Null = every organization; otherwise only organizations currently on this plan.</summary>
    public int? PlanId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public bool Dismissible { get; set; } = true;
}
