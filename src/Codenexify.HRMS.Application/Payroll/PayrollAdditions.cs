using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

// ====================================================================== arrears

public sealed record PaidMonth(int Year, int Month, decimal MonthlyGrossPaidOn, decimal PaidDays, int DaysInMonth);

/// <summary>Pure arithmetic: arrears owed when a salary revision is back-dated into months already paid.</summary>
public static class ArrearsCalculator
{
    /// <returns>Positive = arrears to pay, negative = excess to recover. Whole rupees.</returns>
    public static decimal Compute(decimal newAnnualGross, DateOnly effectiveFrom, IReadOnlyList<PaidMonth> paidMonths)
    {
        var newMonthly = PayrollEngine.R(newAnnualGross / 12m);
        decimal total = 0;
        foreach (var m in paidMonths)
        {
            if (m.DaysInMonth <= 0) continue;
            var monthStart = new DateOnly(m.Year, m.Month, 1);
            if (monthStart.AddMonths(1) <= effectiveFrom) continue;
            // In the month the revision starts, only the days from the effective date onwards change.
            var share = effectiveFrom > monthStart ? (m.DaysInMonth - effectiveFrom.Day + 1) / (decimal)m.DaysInMonth : 1m;
            total += (newMonthly - m.MonthlyGrossPaidOn) * (m.PaidDays / m.DaysInMonth) * share;
        }
        return PayrollEngine.R(total);
    }
}

// ====================================================================== one-time items

public sealed class AdjustmentInput
{
    [Required, Display(Name = "Employee")] public int EmployeeId { get; set; }
    public AdjustmentCategory Category { get; set; } = AdjustmentCategory.Bonus;
    [Required, StringLength(100), Display(Name = "Description on payslip")] public string Name { get; set; } = string.Empty;
    [Range(1, 100_000_000), Display(Name = "Amount (₹)")] public decimal Amount { get; set; }
    [Display(Name = "Taxable")] public bool IsTaxable { get; set; } = true;
    [Range(2000, 2100)] public int Year { get; set; }
    [Range(1, 12)] public int Month { get; set; }
    [StringLength(300)] public string? Notes { get; set; }
}

public sealed record AdjustmentRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, int Year, int Month, AdjustmentCategory Category,
    string Name, decimal Amount, bool IsDeduction, bool IsTaxable, AdjustmentSource Source, string? Notes, int? SettledRunId);

public interface IPayrollAdjustmentService
{
    Task<IReadOnlyList<AdjustmentRow>> ListAsync(bool pendingOnly, CancellationToken ct = default);
    Task<Result> AddAsync(AdjustmentInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>First payroll month that is not finalized yet (where new one-time items land).</summary>
    Task<DateOnly> NextOpenMonthAsync(CancellationToken ct = default);
}

public sealed class PayrollAdjustmentService(IAppDbContext db, ICurrentContext context, IClock clock, IAuditWriter audit) : IPayrollAdjustmentService
{
    public async Task<IReadOnlyList<AdjustmentRow>> ListAsync(bool pendingOnly, CancellationToken ct = default)
    {
        var q = db.PayrollAdjustments.AsNoTracking();
        if (pendingOnly) q = q.Where(a => a.SettledRunId == null);
        var rows = await q.OrderByDescending(a => a.Year).ThenByDescending(a => a.Month).ThenByDescending(a => a.Id).Take(300)
            .Join(db.Employees, a => a.EmployeeId, e => e.Id, (a, e) => new { a, e.FirstName, e.LastName, e.EmployeeCode }).ToListAsync(ct);
        return rows.Select(x => new AdjustmentRow(x.a.Id, x.a.EmployeeId, $"{x.FirstName} {x.LastName}", x.EmployeeCode, x.a.Year, x.a.Month, x.a.Category,
            x.a.Name, x.a.Amount, x.a.IsDeduction, x.a.IsTaxable, x.a.Source, x.a.Notes, x.a.SettledRunId)).ToList();
    }

    public async Task<DateOnly> NextOpenMonthAsync(CancellationToken ct = default)
    {
        var lastFinal = await db.PayrollRuns.Where(r => r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid)
            .Select(r => (int?)(r.Year * 100 + r.Month)).MaxAsync(ct);
        if (lastFinal is int lf) return new DateOnly(lf / 100, lf % 100, 1).AddMonths(1);
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        return new DateOnly(today.Year, today.Month, 1);
    }

    public async Task<Result> AddAsync(AdjustmentInput i, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == i.EmployeeId, ct)) return Result.Fail("Employee not found.");
        // Any month whose payroll is not finalized yet is fine (including a past month not yet run).
        var lastFinal = await db.PayrollRuns.Where(r => r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid)
            .Select(r => (int?)(r.Year * 100 + r.Month)).MaxAsync(ct);
        if (lastFinal is int lf && i.Year * 100 + i.Month <= lf)
        {
            var next = new DateOnly(lf / 100, lf % 100, 1).AddMonths(1);
            return Result.Fail($"Payroll is finalized up to {next.AddMonths(-1):MMMM yyyy}. Choose {next:MMMM yyyy} or later.");
        }
        var deduction = i.Category is AdjustmentCategory.Recovery or AdjustmentCategory.OtherDeduction;
        db.PayrollAdjustments.Add(new PayrollAdjustment
        {
            EmployeeId = i.EmployeeId, Year = i.Year, Month = i.Month, Category = i.Category, Name = i.Name.Trim(), Amount = i.Amount,
            // Reimbursements of actual expenses are not income; deductions are never "taxable".
            IsTaxable = !deduction && i.Category != AdjustmentCategory.Reimbursement && i.IsTaxable,
            Notes = i.Notes?.Trim(), Source = AdjustmentSource.Manual
        });
        audit.Add("Payroll.Adjustment", nameof(PayrollAdjustment), null, $"{i.Category} ₹{i.Amount:N0} for employee {i.EmployeeId} in {i.Month:00}/{i.Year}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var a = await db.PayrollAdjustments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Fail("Item not found.");
        if (a.SettledRunId is not null) return Result.Fail("This item was paid in a finalized payroll and cannot be removed.");
        if (a.Source == AdjustmentSource.Expense) return Result.Fail("This comes from an approved expense claim. Reject or re-route the claim instead.");
        db.PayrollAdjustments.Remove(a);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ====================================================================== expense claims

public sealed class ExpenseItemInput
{
    public int CategoryId { get; set; }
    public DateOnly? Date { get; set; }
    [StringLength(200)] public string? Description { get; set; }
    public decimal? Amount { get; set; }
}

public sealed class ExpenseClaimInput
{
    [Required, StringLength(150), Display(Name = "What is this claim for?")] public string Title { get; set; } = string.Empty;
    public List<ExpenseItemInput> Items { get; set; } = [];
}

public sealed record ExpenseCategoryRow(int Id, string Name, decimal? MaxPerItem, bool ReceiptRequired, bool IsActive);
public sealed record ExpenseClaimRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string Title, DateTime SubmittedAtUtc, decimal Total,
    ExpenseStatus Status, string? ManagerDecisionBy, string? DecidedBy, string? DecisionNotes, ExpensePayout? Payout, DateOnly? PaidOn, string? PaymentReference, int ItemCount);
public sealed record ExpenseItemView(int Id, string Category, DateOnly Date, string Description, decimal Amount, bool HasReceipt, string? ReceiptFileName);
public sealed record ExpenseClaimView(ExpenseClaimRow Claim, IReadOnlyList<ExpenseItemView> Items, int? ManagerUserId);
public sealed record ReceiptFile(Stream Content, string ContentType, string FileName);

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseCategoryRow>> CategoriesAsync(bool activeOnly, CancellationToken ct = default);
    Task<Result> SaveCategoryAsync(int? id, string name, decimal? maxPerItem, bool receiptRequired, bool isActive, CancellationToken ct = default);
    Task<Result<int>> SubmitAsync(int employeeId, ExpenseClaimInput input, IReadOnlyDictionary<int, UploadedFile> receipts, CancellationToken ct = default);
    Task<Result> CancelAsync(int claimId, int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseClaimRow>> MineAsync(int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseClaimRow>> AwaitingManagerAsync(int managerUserId, CancellationToken ct = default);
    Task<PagedResult<ExpenseClaimRow>> ListAsync(IReadOnlyCollection<ExpenseStatus>? statuses, int page, CancellationToken ct = default);
    /// <param name="viewerEmployeeId">Self-service: only the claimant. Null with <paramref name="managerUserId"/>: the approving manager. Both null: finance.</param>
    Task<ExpenseClaimView?> GetAsync(int claimId, int? viewerEmployeeId, int? managerUserId, CancellationToken ct = default);
    Task<Result> ManagerDecideAsync(int claimId, int managerUserId, bool approve, string? notes, CancellationToken ct = default);
    Task<Result> FinanceApproveAsync(int claimId, ExpensePayout payout, string? notes, CancellationToken ct = default);
    Task<Result> FinanceRejectAsync(int claimId, string notes, CancellationToken ct = default);
    Task<Result> MarkPaidAsync(int claimId, DateOnly paidOn, string reference, CancellationToken ct = default);
    Task<ReceiptFile?> ReceiptAsync(int itemId, int? viewerEmployeeId, int? managerUserId, CancellationToken ct = default);
}

public sealed class ExpenseService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage,
    INotificationService notifications, IPayrollAdjustmentService adjustments, IAuditWriter audit) : IExpenseService
{
    private const long MaxReceiptBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, (string Type, byte[] Sig)> Receipts = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [0x25, 0x50, 0x44, 0x46]), [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]), [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
    };

    private int OrgId => context.OrgId();

    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public async Task<IReadOnlyList<ExpenseCategoryRow>> CategoriesAsync(bool activeOnly, CancellationToken ct = default) =>
        await db.ExpenseCategories.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryRow(c.Id, c.Name, c.MaxPerItem, c.ReceiptRequired, c.IsActive)).ToListAsync(ct);

    public async Task<Result> SaveCategoryAsync(int? id, string name, decimal? maxPerItem, bool receiptRequired, bool isActive, CancellationToken ct = default)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 80) return Result.Fail("Give the category a name (up to 80 characters).");
        if (maxPerItem is <= 0) return Result.Fail("The limit must be more than zero, or left blank for no limit.");
        if (await db.ExpenseCategories.AnyAsync(c => c.Name == name && c.Id != id, ct)) return Result.Fail("That category already exists.");
        var c = id is null ? db.ExpenseCategories.Add(new ExpenseCategory()).Entity : await db.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail("Category not found.");
        c.Name = name; c.MaxPerItem = maxPerItem; c.ReceiptRequired = receiptRequired; c.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> SubmitAsync(int employeeId, ExpenseClaimInput input, IReadOnlyDictionary<int, UploadedFile> receipts, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null || !employee.IsCurrent) return Result<int>.Fail("Only current employees can claim expenses.");
        var today = (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        var categories = (await CategoriesAsync(true, ct)).ToDictionary(c => c.Id);
        var rows = input.Items.Select((it, idx) => (it, idx)).Where(x => x.it.Amount is > 0 || !string.IsNullOrWhiteSpace(x.it.Description)).ToList();
        if (rows.Count == 0) return Result<int>.Fail("Add at least one expense.");
        if (rows.Count > 20) return Result<int>.Fail("A claim can have at most 20 expenses.");

        var items = new List<(ExpenseItem Item, UploadedFile? File, string? Ext)>();
        foreach (var (it, idx) in rows)
        {
            var n = idx + 1;
            if (!categories.TryGetValue(it.CategoryId, out var cat)) return Result<int>.Fail($"Expense {n}: choose a category.");
            if (it.Amount is not > 0) return Result<int>.Fail($"Expense {n}: enter the amount.");
            if (it.Date is not { } date || date > today || date < today.AddDays(-180)) return Result<int>.Fail($"Expense {n}: the date must be within the last 180 days.");
            if (string.IsNullOrWhiteSpace(it.Description)) return Result<int>.Fail($"Expense {n}: describe the expense.");
            if (cat.MaxPerItem is { } max && it.Amount > max) return Result<int>.Fail($"Expense {n}: {cat.Name} is limited to ₹{max:N0} per expense.");
            receipts.TryGetValue(idx, out var file);
            if (file is null && cat.ReceiptRequired) return Result<int>.Fail($"Expense {n}: attach the receipt ({cat.Name} needs one).");
            string? ext = null;
            if (file is not null)
            {
                ext = Path.GetExtension(file.FileName);
                if (!Receipts.ContainsKey(ext)) return Result<int>.Fail($"Expense {n}: receipts must be PDF, PNG or JPG.");
                if (file.Length is <= 0 or > MaxReceiptBytes) return Result<int>.Fail($"Expense {n}: receipts can be at most 5 MB.");
            }
            items.Add((new ExpenseItem { CategoryId = cat.Id, Date = date, Description = it.Description!.Trim(), Amount = Math.Round(it.Amount!.Value, 2) }, file, ext));
        }

        var saved = new List<string>();
        try
        {
            foreach (var (item, file, ext) in items.Where(x => x.File is not null))
            {
                using var buffer = new MemoryStream();
                await file!.Content.CopyToAsync(buffer, ct);
                var sig = Receipts[ext!].Sig;
                if (buffer.Length < sig.Length || !buffer.GetBuffer().AsSpan(0, sig.Length).SequenceEqual(sig)) throw new InvalidDataException(file.FileName);
                buffer.Position = 0;
                item.ReceiptKey = await storage.SaveAsync(OrgId, buffer, ext!.ToLowerInvariant(), ct);
                saved.Add(item.ReceiptKey);
                item.ReceiptFileName = Path.GetFileName(file.FileName) is { Length: > 200 } long_ ? long_[^200..] : Path.GetFileName(file.FileName);
                item.ReceiptContentType = Receipts[ext].Type;
            }
            var manager = await db.ApproverForAsync(employeeId, ct);
            var claim = new ExpenseClaim
            {
                EmployeeId = employeeId, Title = input.Title.Trim(), Status = ExpenseStatus.Submitted, Total = items.Sum(x => x.Item.Amount),
                ManagerUserId = manager, Items = items.Select(x => x.Item).ToList()
            };
            db.ExpenseClaims.Add(claim);
            await db.SaveChangesAsync(ct);
            var to = manager is int m ? [m] : await notifications.UsersWithPermissionAsync(TenantPermissions.ExpensesManage, ct);
            notifications.Queue(to.Where(u => u != context.UserId), "Expense claim to review", $"{employee.FullName}: {claim.Title} — ₹{claim.Total:N0}",
                manager is null ? $"/org/expenses/{claim.Id}" : $"/me/expenses/review/{claim.Id}");
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(claim.Id);
        }
        catch (InvalidDataException bad)
        {
            foreach (var key in saved) await storage.DeleteAsync(key, ct);
            return Result<int>.Fail($"The receipt “{bad.Message}” does not match its file type.");
        }
        catch
        {
            foreach (var key in saved) await storage.DeleteAsync(key, ct);
            throw;
        }
    }

    public async Task<Result> CancelAsync(int claimId, int employeeId, CancellationToken ct = default)
    {
        var c = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == claimId && x.EmployeeId == employeeId, ct);
        if (c is null) return Result.Fail("Claim not found.");
        if (c.Status is not (ExpenseStatus.Submitted or ExpenseStatus.ManagerApproved)) return Result.Fail("Only claims still under review can be cancelled.");
        c.Status = ExpenseStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private IQueryable<ExpenseClaimRow> Rows(IQueryable<ExpenseClaim> q) =>
        q.Join(db.Employees, c => c.EmployeeId, e => e.Id, (c, e) => new ExpenseClaimRow(c.Id, c.EmployeeId, e.FirstName + " " + e.LastName, e.EmployeeCode,
            c.Title, c.CreatedAtUtc, c.Total, c.Status, c.ManagerDecisionBy, c.DecidedBy, c.DecisionNotes, c.Payout, c.PaidOn, c.PaymentReference, c.Items.Count));

    public async Task<IReadOnlyList<ExpenseClaimRow>> MineAsync(int employeeId, CancellationToken ct = default) =>
        await Rows(db.ExpenseClaims.AsNoTracking().Where(c => c.EmployeeId == employeeId).OrderByDescending(c => c.Id).Take(50)).ToListAsync(ct);

    public async Task<IReadOnlyList<ExpenseClaimRow>> AwaitingManagerAsync(int managerUserId, CancellationToken ct = default) =>
        await Rows(db.ExpenseClaims.AsNoTracking().Where(c => c.ManagerUserId == managerUserId && c.Status == ExpenseStatus.Submitted).OrderBy(c => c.Id)).ToListAsync(ct);

    public async Task<PagedResult<ExpenseClaimRow>> ListAsync(IReadOnlyCollection<ExpenseStatus>? statuses, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.ExpenseClaims.AsNoTracking();
        if (statuses is { Count: > 0 }) q = q.Where(c => statuses.Contains(c.Status));
        var total = await q.CountAsync(ct);
        var rows = await Rows(q.OrderByDescending(c => c.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedResult<ExpenseClaimRow>(rows, total, page, size);
    }

    private IQueryable<ExpenseClaim> Visible(int? viewerEmployeeId, int? managerUserId)
    {
        var q = db.ExpenseClaims.AsNoTracking();
        if (viewerEmployeeId is int e) q = q.Where(c => c.EmployeeId == e);
        else if (managerUserId is int m) q = q.Where(c => c.ManagerUserId == m);
        return q;
    }

    public async Task<ExpenseClaimView?> GetAsync(int claimId, int? viewerEmployeeId, int? managerUserId, CancellationToken ct = default)
    {
        var row = await Rows(Visible(viewerEmployeeId, managerUserId).Where(c => c.Id == claimId)).FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var mgr = await db.ExpenseClaims.Where(c => c.Id == claimId).Select(c => c.ManagerUserId).FirstAsync(ct);
        var items = await db.ExpenseItems.AsNoTracking().Where(i => i.ClaimId == claimId).OrderBy(i => i.Date)
            .Select(i => new ExpenseItemView(i.Id, i.Category.Name, i.Date, i.Description, i.Amount, i.ReceiptKey != null, i.ReceiptFileName)).ToListAsync(ct);
        return new ExpenseClaimView(row, items, mgr);
    }

    public async Task<Result> ManagerDecideAsync(int claimId, int managerUserId, bool approve, string? notes, CancellationToken ct = default)
    {
        var c = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == claimId && x.ManagerUserId == managerUserId, ct);
        if (c is null) return Result.Fail("Claim not found.");
        if (c.Status != ExpenseStatus.Submitted) return Result.Fail("This claim has already been reviewed.");
        if (!approve && string.IsNullOrWhiteSpace(notes)) return Result.Fail("Tell the employee why the claim is rejected.");
        var me = await MyNameAsync(ct);
        c.ManagerDecisionBy = me;
        c.ManagerDecidedAtUtc = clock.UtcNow;
        if (approve)
        {
            c.Status = ExpenseStatus.ManagerApproved;
            var finance = await notifications.UsersWithPermissionAsync(TenantPermissions.ExpensesManage, ct);
            notifications.Queue(finance.Where(u => u != managerUserId), "Expense claim approved by manager", $"{c.Title} — ₹{c.Total:N0}. Ready for payment approval.", $"/org/expenses/{c.Id}");
        }
        else
        {
            c.Status = ExpenseStatus.Rejected;
            c.DecidedBy = me; c.DecidedAtUtc = clock.UtcNow; c.DecisionNotes = notes!.Trim();
            await NotifyClaimantAsync(c, "Expense claim rejected", notes.Trim(), ct);
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> FinanceApproveAsync(int claimId, ExpensePayout payout, string? notes, CancellationToken ct = default)
    {
        var c = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == claimId, ct);
        if (c is null) return Result.Fail("Claim not found.");
        if (c.Status is not (ExpenseStatus.Submitted or ExpenseStatus.ManagerApproved)) return Result.Fail("This claim is not awaiting approval.");
        var claimant = await db.Employees.Where(e => e.Id == c.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (claimant is int u && u == context.UserId) return Result.Fail("You can't approve your own expense claim.");
        c.Status = ExpenseStatus.Approved;
        c.Payout = payout;
        c.DecidedBy = await MyNameAsync(ct);
        c.DecidedAtUtc = clock.UtcNow;
        c.DecisionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (payout == ExpensePayout.Payroll)
        {
            var month = await adjustments.NextOpenMonthAsync(ct);
            var adj = new PayrollAdjustment
            {
                EmployeeId = c.EmployeeId, Year = month.Year, Month = month.Month, Category = AdjustmentCategory.Reimbursement,
                Name = $"Expense reimbursement #{c.Id}", Amount = c.Total, IsTaxable = false, Source = AdjustmentSource.Expense, SourceId = c.Id,
                Notes = c.Title.Length > 300 ? c.Title[..300] : c.Title
            };
            db.PayrollAdjustments.Add(adj);
            await db.SaveChangesAsync(ct);
            c.AdjustmentId = adj.Id;
        }
        audit.Add("Expense.Approve", nameof(ExpenseClaim), c.Id.ToString(), $"₹{c.Total:N0} via {payout}");
        await NotifyClaimantAsync(c, "Expense claim approved",
            payout == ExpensePayout.Payroll ? $"₹{c.Total:N0} will be reimbursed with your next salary." : $"₹{c.Total:N0} will be paid to you directly.", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> FinanceRejectAsync(int claimId, string notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notes)) return Result.Fail("Tell the employee why the claim is rejected.");
        var c = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == claimId, ct);
        if (c is null) return Result.Fail("Claim not found.");
        if (c.Status is not (ExpenseStatus.Submitted or ExpenseStatus.ManagerApproved)) return Result.Fail("This claim is not awaiting approval.");
        c.Status = ExpenseStatus.Rejected;
        c.DecidedBy = await MyNameAsync(ct);
        c.DecidedAtUtc = clock.UtcNow;
        c.DecisionNotes = notes.Trim();
        await NotifyClaimantAsync(c, "Expense claim rejected", notes.Trim(), ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarkPaidAsync(int claimId, DateOnly paidOn, string reference, CancellationToken ct = default)
    {
        var c = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == claimId, ct);
        if (c is null) return Result.Fail("Claim not found.");
        if (c.Status != ExpenseStatus.Approved || c.Payout != ExpensePayout.Direct) return Result.Fail("Only claims approved for direct payment are marked paid here; payroll claims are paid when that payroll is finalized.");
        if (string.IsNullOrWhiteSpace(reference)) return Result.Fail("Enter the payment reference.");
        if (paidOn > (await db.OrgClockAsync(OrgId, clock, ct)).Today) return Result.Fail("The payment date cannot be in the future.");
        c.Status = ExpenseStatus.Paid;
        c.PaidOn = paidOn;
        c.PaymentReference = reference.Trim();
        await NotifyClaimantAsync(c, "Expense claim paid", $"₹{c.Total:N0} paid on {paidOn:dd MMM yyyy} (ref {c.PaymentReference}).", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<ReceiptFile?> ReceiptAsync(int itemId, int? viewerEmployeeId, int? managerUserId, CancellationToken ct = default)
    {
        var item = await db.ExpenseItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item?.ReceiptKey is null) return null;
        if (!await Visible(viewerEmployeeId, managerUserId).AnyAsync(c => c.Id == item.ClaimId, ct)) return null;
        var stream = await storage.OpenReadAsync(item.ReceiptKey, ct);
        return stream is null ? null : new ReceiptFile(stream, item.ReceiptContentType ?? "application/octet-stream", item.ReceiptFileName ?? "receipt");
    }

    private async Task NotifyClaimantAsync(ExpenseClaim c, string title, string message, CancellationToken ct)
    {
        var uid = await db.Employees.Where(e => e.Id == c.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (uid is int u) notifications.Queue([u], title, $"{c.Title}: {message}", "/me/expenses");
    }
}

/// <summary>Starting expense categories for every organization (limits left open for HR to set).</summary>
public static class ExpenseDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        foreach (var (name, receipt) in new[] { ("Travel & conveyance", true), ("Food & meals", true), ("Internet & phone", true), ("Office supplies", true), ("Other", true) })
            db.ExpenseCategories.Add(new ExpenseCategory { OrganizationId = organizationId, Name = name, ReceiptRequired = receipt });
    }
}
