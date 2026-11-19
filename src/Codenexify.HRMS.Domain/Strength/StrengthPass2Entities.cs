using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Strength;

// ====================================================================== onboarding

/// <summary>Who is responsible for an onboarding task.</summary>
public enum OnboardingAssignee { Hr, Manager, Buddy, Employee }
public enum OnboardingStatus { InProgress, Completed, Cancelled }

/// <summary>Reusable onboarding checklist (e.g. "Engineering joiner").</summary>
public class OnboardingTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<OnboardingTemplateTask> Tasks { get; set; } = new List<OnboardingTemplateTask>();
}

public class OnboardingTemplateTask : TenantEntity
{
    public int TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OnboardingAssignee Assignee { get; set; }
    /// <summary>Due date relative to the joining date (negative = before day one).</summary>
    public int DueOffsetDays { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>An employee's onboarding, copied from a template so later template edits don't change it.</summary>
public class OnboardingPlan : TenantEntity
{
    public int EmployeeId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public DateOnly StartedOn { get; set; }
    public int? BuddyEmployeeId { get; set; }
    public OnboardingStatus Status { get; set; } = OnboardingStatus.InProgress;
    public DateTime? CompletedAtUtc { get; set; }
    public ICollection<OnboardingTask> Tasks { get; set; } = new List<OnboardingTask>();
}

public class OnboardingTask : TenantEntity
{
    public int PlanId { get; set; }
    public OnboardingPlan Plan { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OnboardingAssignee Assignee { get; set; }
    public DateOnly DueOn { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedBy { get; set; }
    public string? Note { get; set; }
}

// ====================================================================== e-signature

public enum SignatureStatus { Pending, Signed, Declined, Cancelled }

/// <summary>An employee's in-app electronic acceptance of an issued letter, confirmed with a one-time code sent by email.</summary>
public class SignatureRequest : TenantEntity
{
    public int GeneratedLetterId { get; set; }
    public int EmployeeId { get; set; }
    public SignatureStatus Status { get; set; } = SignatureStatus.Pending;
    /// <summary>SHA-256 (hex) of the letter subject and text when signing was requested.</summary>
    public string DocumentHash { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }
    public DateTime? OtpSentAtUtc { get; set; }
    public int OtpSends { get; set; }
    public int OtpAttempts { get; set; }
    public string? SignedName { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? SignedIp { get; set; }
    public string? SignedUserAgent { get; set; }
    public string? DeclineReason { get; set; }
    public int? SignedDocumentId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

// ====================================================================== reminder rules & scheduled reports

public enum ReminderEvent { ProbationEnding, DocumentExpiring, WorkAnniversary, Birthday, OnboardingTaskOverdue }

/// <summary>Organization-defined automatic reminder (e.g. "7 days before probation ends, tell the manager and HR").</summary>
public class NotificationRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ReminderEvent Event { get; set; }
    /// <summary>Days before the date (for overdue onboarding tasks: days after the due date).</summary>
    public int Days { get; set; }
    public bool NotifyEmployee { get; set; }
    public bool NotifyManager { get; set; }
    public bool NotifyHr { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Remembers which reminders were sent so each fires once per subject.</summary>
public class NotificationRuleLog : TenantEntity
{
    public int RuleId { get; set; }
    public string SubjectKey { get; set; } = string.Empty;
    public DateOnly SentOn { get; set; }
}

public enum ReportKind { HeadcountSummary, JoinersAndExits }
public enum ReportFrequency { Weekly, Monthly }

/// <summary>A report summary emailed on a schedule, with a link back to the full report in HRMS.</summary>
public class ReportSchedule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ReportKind Kind { get; set; }
    public ReportFrequency Frequency { get; set; }
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
    /// <summary>Comma-separated user ids (users who can view reports).</summary>
    public string RecipientUserIds { get; set; } = string.Empty;
    public DateOnly NextRunOn { get; set; }
    public DateTime? LastSentAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

// ====================================================================== mobile OTP sign-in

/// <summary>A one-time sign-in code sent by SMS (only its hash is stored).</summary>
public class LoginOtp : TenantEntity
{
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? IpAddress { get; set; }
}

// ====================================================================== travel desk

public enum TravelMode { Flight, Train, Bus, Cab, OwnVehicle, Other }
/// <summary>Submitted → Approved (manager) → Booked (travel desk) → Completed; or Rejected / Cancelled.</summary>
public enum TravelStatus { Submitted, Approved, Booked, Completed, Rejected, Cancelled }

public class TravelRequest : TenantEntity
{
    public int EmployeeId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    public DateOnly DepartOn { get; set; }
    public DateOnly? ReturnOn { get; set; }
    public TravelMode Mode { get; set; }
    public bool NeedsAccommodation { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? AdvanceRequested { get; set; }
    public string? Notes { get; set; }
    public TravelStatus Status { get; set; } = TravelStatus.Submitted;
    /// <summary>The manager (user) who must approve; null = the travel desk approves.</summary>
    public int? ApproverUserId { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public string? BookingDetails { get; set; }
    public decimal? BookedCost { get; set; }
    public string? TicketStorageKey { get; set; }
    public string? TicketFileName { get; set; }
    public string? TicketContentType { get; set; }
    public string? BookedBy { get; set; }
    public DateTime? BookedAtUtc { get; set; }
    public decimal? AdvancePaid { get; set; }
    public DateOnly? AdvancePaidOn { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
