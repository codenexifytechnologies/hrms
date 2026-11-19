using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.CoreHr;

namespace Codenexify.HRMS.Domain.Planning;

// ====================================================================== headcount planning

public enum HeadcountPlanStatus { Draft, Approved, Closed }
/// <summary>A new role grows the team; a replacement backfills someone who left (projected headcount doesn't change).</summary>
public enum HeadcountPositionKind { NewRole, Replacement }
public enum HeadcountPositionStatus { Open, OnHold, Cancelled }
public enum HeadcountPriority { Low, Medium, High }

/// <summary>A hiring plan for a period (e.g. FY 2027-28): which positions each department will add and what they cost.</summary>
public class HeadcountPlan : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public HeadcountPlanStatus Status { get; set; } = HeadcountPlanStatus.Draft;
    public string? Notes { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public ICollection<HeadcountPosition> Positions { get; set; } = new List<HeadcountPosition>();
}

/// <summary>One line of the plan: N openings for a role in a department, with a target start and a budget per hire.</summary>
public class HeadcountPosition : TenantEntity
{
    public int PlanId { get; set; }
    public HeadcountPlan Plan { get; set; } = null!;
    public int DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public int? DesignationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Openings { get; set; } = 1;
    public HeadcountPositionKind Kind { get; set; }
    public DateOnly TargetStartOn { get; set; }
    /// <summary>Budgeted annual cost (CTC) for each hire.</summary>
    public decimal AnnualBudgetPerHire { get; set; }
    public HeadcountPriority Priority { get; set; } = HeadcountPriority.Medium;
    public HeadcountPositionStatus Status { get; set; } = HeadcountPositionStatus.Open;
    public string? Notes { get; set; }
    public ICollection<HeadcountHire> Hires { get; set; } = new List<HeadcountHire>();
}

/// <summary>An employee who filled one opening of a position.</summary>
public class HeadcountHire : TenantEntity
{
    public int PositionId { get; set; }
    public HeadcountPosition Position { get; set; } = null!;
    /// <summary>Copied from the position so an employee can fill only one opening per plan (unique index).</summary>
    public int PlanId { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}

// ====================================================================== pre-boarding

public enum PreboardingItemKind { Document, Acknowledgement }

/// <summary>The organization's welcome content for new hires (one row per organization).</summary>
public class PreboardingSettings : TenantEntity
{
    public string? WelcomeMessage { get; set; }
    public string? FirstDayInfo { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool AskBankDetails { get; set; } = true;
    public bool AskPan { get; set; } = true;
    /// <summary>The link keeps working this many days after the joining date.</summary>
    public int LinkDaysAfterJoining { get; set; } = 3;
}

/// <summary>Something every new hire is asked to do before day one: upload a document or read and accept something.</summary>
public class PreboardingItem : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PreboardingItemKind Kind { get; set; }
    public DocumentCategory DocumentCategory { get; set; } = DocumentCategory.Identity;
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>A secure link sent to a new hire. Only a hash of the token is stored.</summary>
public class PreboardingInvite : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int? SentByUserId { get; set; }
    public DateTime? FirstOpenedAtUtc { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }
    public DateTime? PersonalSavedAtUtc { get; set; }
    public DateTime? BankSavedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public ICollection<PreboardingResponse> Responses { get; set; } = new List<PreboardingResponse>();
}

/// <summary>A new hire's answer to one item: an uploaded document or an acknowledgement.</summary>
public class PreboardingResponse : TenantEntity
{
    public int InviteId { get; set; }
    public int ItemId { get; set; }
    public PreboardingItem Item { get; set; } = null!;
    public int? EmployeeDocumentId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
