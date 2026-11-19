using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.CoreHr;

namespace Codenexify.HRMS.Domain.Benefits;

public enum BenefitCategory { HealthInsurance, LifeInsurance, AccidentInsurance, Allowance, Meal, Transport, Wellness, Education, Other }

/// <summary>Automatic: every eligible employee is enrolled by the company. OptIn: eligible employees choose to join.</summary>
public enum BenefitEnrollmentMode { Automatic, OptIn }

public enum BenefitEnrollmentStatus { Active, Ended }

public enum DependentRelation { Spouse, Child, Father, Mother, FatherInLaw, MotherInLaw, Other }

/// <summary>
/// A perk or benefit the company offers: an insurance policy, a monthly allowance, a meal or wellness benefit.
/// Amounts are monthly. Employee contributions are deducted in payroll; allowances are paid in payroll.
/// </summary>
public class BenefitPlan : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public BenefitCategory Category { get; set; }
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public string? PolicyNumber { get; set; }
    /// <summary>Sum insured / cover per employee (information only).</summary>
    public decimal? CoverageAmount { get; set; }
    public DateOnly? PolicyRenewsOn { get; set; }

    /// <summary>Monthly cost the company pays per enrolled employee (premium share). Shown in cost reports, not on payslips.</summary>
    public decimal EmployerCostPerMonth { get; set; }
    /// <summary>Monthly amount deducted from the employee's salary.</summary>
    public decimal EmployeeContributionPerMonth { get; set; }
    /// <summary>Extra monthly deduction for each covered dependent.</summary>
    public decimal DependentContributionPerMonth { get; set; }
    /// <summary>Monthly amount paid to the employee with salary (allowance-type benefits).</summary>
    public decimal AllowancePerMonth { get; set; }
    /// <summary>Whether the allowance counts as taxable income for TDS.</summary>
    public bool AllowanceTaxable { get; set; } = true;

    public BenefitEnrollmentMode EnrollmentMode { get; set; } = BenefitEnrollmentMode.Automatic;
    public bool CoversDependents { get; set; }
    public int MaxDependents { get; set; }
    /// <summary>Opt-in window (both null = any time).</summary>
    public DateOnly? EnrollmentOpensOn { get; set; }
    public DateOnly? EnrollmentClosesOn { get; set; }

    // Eligibility (all conditions must hold; empty = no restriction)
    /// <summary>Comma separated <see cref="EmploymentType"/> names.</summary>
    public string? EligibleEmploymentTypes { get; set; }
    public int MinServiceDays { get; set; }
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public bool ExcludeProbation { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional policy document employees can download (PDF)
    public string? DocumentStorageKey { get; set; }
    public string? DocumentFileName { get; set; }

    public ICollection<BenefitEnrollment> Enrollments { get; set; } = new List<BenefitEnrollment>();
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>An employee's membership of a benefit plan for a period.</summary>
public class BenefitEnrollment : TenantEntity
{
    public int BenefitPlanId { get; set; }
    public BenefitPlan Plan { get; set; } = null!;
    public int EmployeeId { get; set; }
    public BenefitEnrollmentStatus Status { get; set; } = BenefitEnrollmentStatus.Active;
    public DateOnly StartsOn { get; set; }
    /// <summary>Last covered day (inclusive); null while ongoing.</summary>
    public DateOnly? EndsOn { get; set; }
    public string? EndReason { get; set; }
    /// <summary>Who enrolled: "Automatic", "Employee" or the HR user's name.</summary>
    public string EnrolledBy { get; set; } = string.Empty;
    public ICollection<BenefitDependent> Dependents { get; set; } = new List<BenefitDependent>();
}

/// <summary>A family member covered under an insurance enrollment (kept minimal: name, relation, birth date).</summary>
public class BenefitDependent : TenantEntity
{
    public int BenefitEnrollmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DependentRelation Relation { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
