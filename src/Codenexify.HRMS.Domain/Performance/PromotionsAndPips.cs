using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== promotions

/// <summary>Proposed → Approved (applied on the effective date) → Applied; or Rejected / Withdrawn.</summary>
public enum PromotionStatus { Proposed, Approved, Applied, Rejected, Withdrawn }

/// <summary>A proposal to move an employee to a new job title and/or salary.</summary>
public class PromotionCase : TenantEntity
{
    public int EmployeeId { get; set; }
    /// <summary>The manager who proposed it (null when HR proposed).</summary>
    public int? ProposedByEmployeeId { get; set; }
    public string ProposedByName { get; set; } = string.Empty;
    public int? FromDesignationId { get; set; }
    public int? ToDesignationId { get; set; }
    /// <summary>Annual gross when proposed (null if no salary was set).</summary>
    public decimal? CurrentAnnualGross { get; set; }
    /// <summary>New annual gross (null = no salary change).</summary>
    public decimal? ProposedAnnualGross { get; set; }
    public DateOnly EffectiveOn { get; set; }
    public string Justification { get; set; } = string.Empty;
    /// <summary>The review the proposal is based on, if any.</summary>
    public int? PerformanceReviewId { get; set; }
    public PromotionStatus Status { get; set; } = PromotionStatus.Proposed;
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    /// <summary>Why applying failed (e.g. payroll month already finalized); HR can retry.</summary>
    public string? ApplyError { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

// ====================================================================== performance improvement plans

/// <summary>Draft → Active (employee told; acknowledges) → Passed / Not passed; or Extended (new end date) / Cancelled.</summary>
public enum PipStatus { Draft, Active, Passed, NotPassed, Cancelled }
public enum PipObjectiveStatus { Open, Met, NotMet }
public enum PipProgress { OnTrack, SomeConcerns, OffTrack }

public class ImprovementPlan : TenantEntity
{
    public int EmployeeId { get; set; }
    /// <summary>The manager running the plan.</summary>
    public int? ManagerEmployeeId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    /// <summary>The performance concerns, described factually.</summary>
    public string Concerns { get; set; } = string.Empty;
    /// <summary>Training, coaching or resources the company will provide.</summary>
    public string? Support { get; set; }
    public PipStatus Status { get; set; } = PipStatus.Draft;
    public int Extensions { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? EmployeeComment { get; set; }
    public string? OutcomeNote { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public ICollection<PipObjective> Objectives { get; set; } = new List<PipObjective>();
    public ICollection<PipCheckIn> CheckIns { get; set; } = new List<PipCheckIn>();
    public byte[] RowVersion { get; set; } = [];
}

public class PipObjective : TenantEntity
{
    public int ImprovementPlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>How success will be measured.</summary>
    public string Measure { get; set; } = string.Empty;
    public PipObjectiveStatus Status { get; set; } = PipObjectiveStatus.Open;
    public int SortOrder { get; set; }
}

public class PipCheckIn : TenantEntity
{
    public int ImprovementPlanId { get; set; }
    public DateOnly OnDate { get; set; }
    public PipProgress Progress { get; set; }
    public string Note { get; set; } = string.Empty;
    public string ByName { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}
