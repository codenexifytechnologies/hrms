using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== compensation planning

/// <summary>Draft → Planning (managers enter raises) → Review (HR finalises) → Approved (applied on the date) → Closed.</summary>
public enum CompensationCycleStatus { Draft, Planning, Review, Approved, Closed }

/// <summary>A salary-revision (merit) cycle with a budget and rating-based guidelines.</summary>
public class CompensationCycle : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly EffectiveOn { get; set; }
    /// <summary>Budget as a percentage of the eligible employees' current annual gross.</summary>
    public decimal BudgetPercent { get; set; }
    /// <summary>Ratings come from this review cycle (final rating) when set.</summary>
    public int? ReviewCycleId { get; set; }
    public DateOnly? JoinedOnOrBefore { get; set; }
    public int? DepartmentId { get; set; }
    public DateOnly? PlanningDueOn { get; set; }
    public CompensationCycleStatus Status { get; set; } = CompensationCycleStatus.Draft;
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public ICollection<CompensationGuideline> Guidelines { get; set; } = new List<CompensationGuideline>();
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Recommended raise range for a rating (Rating 0 = employees without a rating).</summary>
public class CompensationGuideline : TenantEntity
{
    public int CompensationCycleId { get; set; }
    public int Rating { get; set; }
    public decimal MinPercent { get; set; }
    public decimal MaxPercent { get; set; }
}

/// <summary>One employee's proposed raise in a cycle.</summary>
public class CompensationLine : TenantEntity
{
    public int CompensationCycleId { get; set; }
    public int EmployeeId { get; set; }
    /// <summary>Who plans this raise: the reporting manager at launch; null = HR.</summary>
    public int? PlannerEmployeeId { get; set; }
    public decimal CurrentAnnualGross { get; set; }
    public int? Rating { get; set; }
    public decimal? ProposedPercent { get; set; }
    public decimal? ProposedAnnualGross { get; set; }
    public string? Note { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public string? ApplyError { get; set; }
}

// ====================================================================== calibration

/// <summary>Target share of each rating in a review cycle, used on the calibration board.</summary>
public class CalibrationTarget : TenantEntity
{
    public int ReviewCycleId { get; set; }
    public int Rating { get; set; }
    public decimal Percent { get; set; }
}
