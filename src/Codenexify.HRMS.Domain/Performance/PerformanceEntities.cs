using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== goals & OKRs

public enum GoalLevel { Company, Department, Individual }
public enum GoalStatus { NotStarted, OnTrack, AtRisk, OffTrack, Completed, Cancelled }
public enum KeyResultType { Number, Percentage, Currency, Milestone }

/// <summary>An objective for a period. Company and department goals are visible to everyone; individual goals to the owner, their managers and HR.</summary>
public class Goal : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GoalLevel Level { get; set; }
    /// <summary>Set for department goals.</summary>
    public int? DepartmentId { get; set; }
    /// <summary>Set for individual goals.</summary>
    public int? OwnerEmployeeId { get; set; }
    /// <summary>The higher-level goal this one supports (alignment).</summary>
    public int? ParentGoalId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly DueOn { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.NotStarted;
    /// <summary>0–100. Average of key results when there are any; otherwise set at check-in.</summary>
    public decimal Progress { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public ICollection<KeyResult> KeyResults { get; set; } = new List<KeyResult>();
    public ICollection<GoalCheckIn> CheckIns { get; set; } = new List<GoalCheckIn>();
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>A measurable result of a goal: from a start value to a target (a milestone is 0 → 1).</summary>
public class KeyResult : TenantEntity
{
    public int GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public KeyResultType Type { get; set; }
    public decimal StartValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A progress update: new values, status and a short note.</summary>
public class GoalCheckIn : TenantEntity
{
    public int GoalId { get; set; }
    public decimal Progress { get; set; }
    public GoalStatus Status { get; set; }
    public string? Note { get; set; }
    public string ByName { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}

// ====================================================================== performance reviews

/// <summary>Draft → Active (self & manager reviews) → Released (employees see results) → Closed.</summary>
public enum ReviewCycleStatus { Draft, Active, Released, Closed }
public enum ReviewStatus { SelfReview, ManagerReview, Submitted, Released, Acknowledged }
public enum ReviewQuestionType { Rating, Text }
public enum ReviewAudience { Both, SelfOnly, ManagerOnly }
public enum ReviewRole { Self, Manager }

public class ReviewCycle : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly SelfReviewDue { get; set; }
    public DateOnly ManagerReviewDue { get; set; }
    public ReviewCycleStatus Status { get; set; } = ReviewCycleStatus.Draft;
    /// <summary>Five labels for ratings 1–5, separated by '|'.</summary>
    public string RatingLabels { get; set; } = "Needs improvement|Below expectations|Meets expectations|Exceeds expectations|Outstanding";
    /// <summary>Show the employee's goals for the period in the review form.</summary>
    public bool IncludeGoals { get; set; } = true;
    /// <summary>Employees who joined after this date are left out (null = everyone current at launch).</summary>
    public DateOnly? JoinedOnOrBefore { get; set; }
    public int? DepartmentId { get; set; }
    public bool SkipSelfReview { get; set; }
    public DateTime? LaunchedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public ICollection<ReviewQuestion> Questions { get; set; } = new List<ReviewQuestion>();
    public byte[] RowVersion { get; set; } = [];
}

public class ReviewQuestion : TenantEntity
{
    public int ReviewCycleId { get; set; }
    public string Text { get; set; } = string.Empty;
    public ReviewQuestionType Type { get; set; }
    public ReviewAudience Audience { get; set; }
    public bool Required { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>One employee's review in a cycle.</summary>
public class PerformanceReview : TenantEntity
{
    public int ReviewCycleId { get; set; }
    public ReviewCycle Cycle { get; set; } = null!;
    public int EmployeeId { get; set; }
    /// <summary>The reviewer (the reporting manager at launch); null = HR reviews.</summary>
    public int? ReviewerEmployeeId { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.SelfReview;
    public DateTime? SelfSubmittedAtUtc { get; set; }
    public DateTime? ManagerSubmittedAtUtc { get; set; }
    public string? ReviewedByName { get; set; }
    /// <summary>The reviewer's overall rating (1–5).</summary>
    public int? OverallRating { get; set; }
    /// <summary>Rating after HR adjustment (calibration); null = same as the overall rating.</summary>
    public int? FinalRating { get; set; }
    public string? AdjustmentNote { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? EmployeeComment { get; set; }
    public ICollection<ReviewAnswer> Answers { get; set; } = new List<ReviewAnswer>();
    public byte[] RowVersion { get; set; } = [];
}

public class ReviewAnswer : TenantEntity
{
    public int PerformanceReviewId { get; set; }
    public int ReviewQuestionId { get; set; }
    public ReviewRole Role { get; set; }
    public int? Rating { get; set; }
    public string? Text { get; set; }
}
