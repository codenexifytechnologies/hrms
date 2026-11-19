using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Payroll;

public enum AdjustmentCategory { Bonus, Incentive, Arrears, Reimbursement, OtherEarning, Recovery, OtherDeduction }
public enum AdjustmentSource { Manual, SalaryRevision, Expense }

/// <summary>
/// A one-time amount paid (or recovered) in a payroll month: bonus, incentive, arrears, reimbursement, recovery.
/// Settled when the payroll that includes it is finalized; while unsettled it carries forward to the next run.
/// </summary>
public class PayrollAdjustment : TenantEntity
{
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public AdjustmentCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; }
    public string? Notes { get; set; }
    public AdjustmentSource Source { get; set; }
    public int? SourceId { get; set; }
    /// <summary>Salary months an arrears entry covers (back-dated revisions).</summary>
    public DateOnly? CoversFrom { get; set; }
    public DateOnly? CoversTo { get; set; }
    public int? SettledRunId { get; set; }

    public bool IsDeduction => Category is AdjustmentCategory.Recovery or AdjustmentCategory.OtherDeduction;
}

public class ExpenseCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional cap per item; null = no limit.</summary>
    public decimal? MaxPerItem { get; set; }
    public bool ReceiptRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public enum ExpenseStatus { Submitted, ManagerApproved, Approved, Rejected, Paid, Cancelled }
public enum ExpensePayout { Payroll, Direct }

public class ExpenseClaim : TenantEntity
{
    public int EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ExpenseStatus Status { get; set; }
    public decimal Total { get; set; }
    /// <summary>Reporting manager's user at submission (first approval step), if any.</summary>
    public int? ManagerUserId { get; set; }
    public string? ManagerDecisionBy { get; set; }
    public DateTime? ManagerDecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
    public ExpensePayout? Payout { get; set; }
    public int? AdjustmentId { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? PaymentReference { get; set; }
    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
}

public class ExpenseItem : TenantEntity
{
    public int ClaimId { get; set; }
    public int CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptKey { get; set; }
    public string? ReceiptFileName { get; set; }
    public string? ReceiptContentType { get; set; }
}
