using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.CoreHr;

namespace Codenexify.HRMS.Domain.Payroll;

public enum ComponentType { Earning, Deduction }
public enum ComponentCalculation { FixedAmount, PercentOfGross, PercentOfBasic, Balancing }
public enum TaxRegime { New, Old }
public enum PayrollRunStatus { Draft, Processed, Finalized, Paid }
public enum PayrollLineKind { Earning, Deduction, EmployerContribution }
public enum LoanType { Loan, SalaryAdvance }
public enum LoanStatus { Requested, Active, Rejected, Closed }
public enum DeclarationStatus { Draft, Submitted, Approved }
public enum SettlementStatus { Draft, Approved, Paid }

// ------------------------------------------------------------------ platform-wide statutory rules
// Maintained by the platform owner (Super Admin). Effective-dated; never hard-coded in calculation code.

/// <summary>PF, ESI and gratuity parameters effective from a date (platform-wide).</summary>
public class StatutoryRateSet : BaseEntity, IAuditable
{
    public DateOnly EffectiveFrom { get; set; }
    public decimal PfEmployeeRate { get; set; }
    public decimal PfEmployerRate { get; set; }
    public decimal EpsRate { get; set; }
    public decimal PfWageCeiling { get; set; }
    public decimal EdliRate { get; set; }
    public decimal PfAdminChargesRate { get; set; }
    public decimal EsiEmployeeRate { get; set; }
    public decimal EsiEmployerRate { get; set; }
    public decimal EsiWageLimit { get; set; }
    public decimal GratuityMinServiceYears { get; set; }
    public decimal GratuityDaysPerYear { get; set; }
    public decimal GratuityWageDivisor { get; set; }
    public decimal GratuityCeiling { get; set; }
    /// <summary>Set once someone has checked the values against current regulations.</summary>
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
}

public class ProfessionalTaxSlab : BaseEntity, IAuditable
{
    public string State { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    /// <summary>Null = everyone; otherwise the slab applies to that gender only.</summary>
    public Gender? Gender { get; set; }
    public decimal MinMonthlyGross { get; set; }
    public decimal? MaxMonthlyGross { get; set; }
    public decimal MonthlyAmount { get; set; }
    /// <summary>Different amount in a specific month (e.g. Maharashtra levies more in February).</summary>
    public int? SpecialMonth { get; set; }
    public decimal? SpecialMonthAmount { get; set; }
}

public class LabourWelfareFundRule : BaseEntity, IAuditable
{
    public string State { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public decimal EmployeeAmount { get; set; }
    public decimal EmployerAmount { get; set; }
    /// <summary>Comma separated deduction months, e.g. "6,12".</summary>
    public string Months { get; set; } = "6,12";
}

/// <summary>Income-tax parameters for one financial year (starting year, e.g. 2026 = FY 2026-27) and regime.</summary>
public class IncomeTaxRule : BaseEntity, IAuditable
{
    public int FinancialYear { get; set; }
    public TaxRegime Regime { get; set; }
    public decimal StandardDeduction { get; set; }
    public decimal RebateIncomeLimit { get; set; }
    public decimal RebateMaxAmount { get; set; }
    public decimal CessRate { get; set; }
    public decimal Section80CLimit { get; set; }
    public decimal Section80DLimit { get; set; }
    public decimal Section80CCD1BLimit { get; set; }
    public decimal HomeLoanInterestLimit { get; set; }
    public bool IsVerified { get; set; }
    public ICollection<IncomeTaxSlab> Slabs { get; set; } = new List<IncomeTaxSlab>();
    public ICollection<IncomeTaxSurcharge> Surcharges { get; set; } = new List<IncomeTaxSurcharge>();
}

public class IncomeTaxSlab
{
    public int Id { get; set; }
    public int IncomeTaxRuleId { get; set; }
    public decimal FromIncome { get; set; }
    public decimal? ToIncome { get; set; }
    public decimal Rate { get; set; }
}

public class IncomeTaxSurcharge
{
    public int Id { get; set; }
    public int IncomeTaxRuleId { get; set; }
    public decimal AboveIncome { get; set; }
    public decimal Rate { get; set; }
}

// ------------------------------------------------------------------ tenant payroll configuration

public class PayrollSetting : TenantEntity
{
    public bool PfEnabled { get; set; } = true;
    /// <summary>Compute PF on wages capped at the ceiling (common practice) instead of actual PF wages.</summary>
    public bool PfRestrictToCeiling { get; set; } = true;
    public bool EsiEnabled { get; set; } = true;
    public bool PtEnabled { get; set; } = true;
    public bool LwfEnabled { get; set; } = true;
    public bool TdsEnabled { get; set; } = true;
    /// <summary>State used for PT/LWF when the employee's branch has none.</summary>
    public string? DefaultState { get; set; }
    public string? PfEstablishmentCode { get; set; }
    public string? EsiEmployerCode { get; set; }
    public string? Tan { get; set; }
    public decimal OvertimeRateMultiplier { get; set; } = 2;
    /// <summary>Days used to derive the daily rate for overtime and leave encashment.</summary>
    public decimal DailyRateDivisor { get; set; } = 26;
    public int PayDay { get; set; } = 1;
    // Accounting ledger names for the payroll journal
    public string SalaryExpenseAccount { get; set; } = "Salaries & Wages";
    public string EmployerContributionAccount { get; set; } = "Employer Statutory Contributions";
    public string SalaryPayableAccount { get; set; } = "Salary Payable";
    public string PfPayableAccount { get; set; } = "PF Payable";
    public string EsiPayableAccount { get; set; } = "ESI Payable";
    public string PtPayableAccount { get; set; } = "Professional Tax Payable";
    public string LwfPayableAccount { get; set; } = "LWF Payable";
    public string TdsPayableAccount { get; set; } = "TDS Payable";
    public string LoanRecoveryAccount { get; set; } = "Employee Loans & Advances";
}

public class SalaryComponent : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ComponentType Type { get; set; } = ComponentType.Earning;
    public bool IsTaxable { get; set; } = true;
    /// <summary>Counts towards PF wages (Basic and DA).</summary>
    public bool IsPfWage { get; set; }
    public bool IsEsiWage { get; set; } = true;
    public bool IsProrated { get; set; } = true;
    /// <summary>Used for the HRA exemption calculation.</summary>
    public bool IsHra { get; set; }
    /// <summary>Basic pay: the base for percent-of-basic components, gratuity and encashment.</summary>
    public bool IsBasic { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class SalaryStructure : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<SalaryStructureLine> Lines { get; set; } = new List<SalaryStructureLine>();
}

public class SalaryStructureLine : TenantEntity
{
    public int SalaryStructureId { get; set; }
    public int SalaryComponentId { get; set; }
    public SalaryComponent Component { get; set; } = null!;
    public ComponentCalculation Calculation { get; set; }
    /// <summary>Monthly amount for fixed components, or a percentage.</summary>
    public decimal Value { get; set; }
}

/// <summary>Salary assignment; each revision is a new row effective from a date.</summary>
public class EmployeeSalary : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int SalaryStructureId { get; set; }
    public SalaryStructure Structure { get; set; } = null!;
    public decimal AnnualGross { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool PfApplicable { get; set; } = true;
    public string? Remarks { get; set; }
}

public class TaxDeclaration : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int FinancialYear { get; set; }
    public TaxRegime Regime { get; set; } = TaxRegime.New;
    public decimal Section80C { get; set; }
    public decimal Section80D { get; set; }
    public decimal Section80CCD1B { get; set; }
    public decimal HomeLoanInterest { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal MonthlyRentPaid { get; set; }
    public bool MetroCity { get; set; }
    public decimal PreviousEmployerIncome { get; set; }
    public decimal PreviousEmployerTds { get; set; }
    public DeclarationStatus Status { get; set; } = DeclarationStatus.Draft;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}

public class EmployeeLoan : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public LoanType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal InstallmentAmount { get; set; }
    /// <summary>First payroll month for recovery (first day of the month).</summary>
    public DateOnly RecoveryStart { get; set; }
    public decimal Outstanding { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LoanStatus Status { get; set; } = LoanStatus.Requested;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
    public ICollection<LoanRepayment> Repayments { get; set; } = new List<LoanRepayment>();
}

public class LoanRepayment : TenantEntity
{
    public int EmployeeLoanId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public int? PayrollRunId { get; set; }
    public string? Note { get; set; }
}

// ------------------------------------------------------------------ payroll runs

public class PayrollRun : TenantEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public string? FinalizedBy { get; set; }
    public DateOnly? PaidOn { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerCost { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();
}

/// <summary>One employee's pay for a run; becomes the payslip once the run is finalized.</summary>
public class PayrollDetail : TenantEntity
{
    public int PayrollRunId { get; set; }
    public PayrollRun Run { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int DaysInMonth { get; set; }
    public decimal PaidDays { get; set; }
    public decimal LopDays { get; set; }
    /// <summary>HR override of the calculated LOP days (kept across re-processing).</summary>
    public decimal? LopOverride { get; set; }
    public bool OnHold { get; set; }
    public string? HoldReason { get; set; }
    public decimal MonthlyGross { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal EmployerContributions { get; set; }
    public decimal PfWages { get; set; }
    public decimal EsiWages { get; set; }
    public decimal TaxableEarnings { get; set; }
    public decimal ProjectedAnnualTax { get; set; }
    /// <summary>Encashed leave days paid in this run (marked paid on the balance at finalization).</summary>
    public decimal LeaveEncashmentDays { get; set; }
    public TaxRegime Regime { get; set; }
    public string? State { get; set; }
    public string? Warnings { get; set; }
    public ICollection<PayrollLine> Lines { get; set; } = new List<PayrollLine>();
}

public class PayrollLine : TenantEntity
{
    public int PayrollDetailId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollLineKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal? FullAmount { get; set; }
    public bool IsTaxable { get; set; }
    public int SortOrder { get; set; }
}

// ------------------------------------------------------------------ full & final

public class FullAndFinalSettlement : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly LastWorkingDay { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Draft;
    public decimal TotalPayable { get; set; }
    public decimal TotalRecoverable { get; set; }
    public decimal NetAmount { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Notes { get; set; }
    public ICollection<SettlementLine> Lines { get; set; } = new List<SettlementLine>();
}

public class SettlementLine : TenantEntity
{
    public int FullAndFinalSettlementId { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Positive = payable to the employee, negative = recovered.</summary>
    public decimal Amount { get; set; }
    public bool IsManual { get; set; }
    public int SortOrder { get; set; }
}
