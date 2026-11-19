using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

// ------------------------------------------------------------------ loans & advances

public sealed class LoanRequestInput
{
    public LoanType Type { get; set; } = LoanType.SalaryAdvance;
    [Range(1, 100_000_000)] public decimal Amount { get; set; }
    [Range(1, 100_000_000), Display(Name = "Monthly instalment")] public decimal InstallmentAmount { get; set; }
    [Required, Display(Name = "Recover from (month)")] public DateOnly RecoveryStart { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}

public sealed record LoanRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, LoanType Type, decimal Amount, decimal Installment,
    DateOnly RecoveryStart, decimal Outstanding, LoanStatus Status, string Reason, string? DecisionNotes, DateTime RequestedAtUtc);

public interface ILoanService
{
    Task<Result> RequestAsync(int employeeId, LoanRequestInput input, bool byHr, CancellationToken ct = default);
    Task<Result> DecideAsync(int id, bool approve, string? notes, CancellationToken ct = default);
    Task<Result> RecordRepaymentAsync(int id, decimal amount, DateOnly date, string? note, CancellationToken ct = default);
    Task<PagedResult<LoanRow>> ListAsync(LoanStatus? status, int page, CancellationToken ct = default);
    Task<IReadOnlyList<LoanRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default);
}

public sealed class LoanService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications, IAuditWriter audit) : ILoanService
{
    public async Task<Result> RequestAsync(int employeeId, LoanRequestInput input, bool byHr, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return Result.Fail("Employee not found.");
        if (!e.IsCurrent) return Result.Fail("Loans can only be given to current employees.");
        if (input.InstallmentAmount > input.Amount) return Result.Fail("The instalment cannot exceed the amount.");
        var start = new DateOnly(input.RecoveryStart.Year, input.RecoveryStart.Month, 1);
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        if (start < new DateOnly(today.Year, today.Month, 1)) return Result.Fail("Recovery cannot start in a past month.");
        if (await db.EmployeeLoans.AnyAsync(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Requested, ct))
            return Result.Fail("A request is already awaiting a decision.");
        db.EmployeeLoans.Add(new EmployeeLoan
        {
            EmployeeId = employeeId, Type = input.Type, Amount = input.Amount, InstallmentAmount = input.InstallmentAmount,
            RecoveryStart = start, Outstanding = input.Amount, Reason = input.Reason.Trim()
        });
        if (!byHr)
            notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.PayrollManage, ct), "Loan / advance request",
                $"{e.FullName} requested a {(input.Type == LoanType.Loan ? "loan" : "salary advance")} of ₹{input.Amount:N0}.", "/org/payroll/loans");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DecideAsync(int id, bool approve, string? notes, CancellationToken ct = default)
    {
        var loan = await db.EmployeeLoans.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (loan is null) return Result.Fail("Request not found.");
        if (loan.Status != LoanStatus.Requested) return Result.Fail("This request has already been decided.");
        if (loan.Employee.UserId == context.UserId) return Result.Fail("You cannot approve your own request.");
        if (!approve && string.IsNullOrWhiteSpace(notes)) return Result.Fail("Give a reason for rejecting.");
        loan.Status = approve ? LoanStatus.Active : LoanStatus.Rejected;
        loan.DecidedBy = context.UserName;
        loan.DecidedAtUtc = clock.UtcNow;
        loan.DecisionNotes = notes?.Trim();
        if (loan.Employee.UserId is int uid)
            notifications.Queue([uid], $"Loan request {(approve ? "approved" : "rejected")}", $"Your request for ₹{loan.Amount:N0} was {(approve ? "approved" : "rejected")}.", "/me/payslips");
        audit.Add(approve ? "Loan.Approve" : "Loan.Reject", nameof(EmployeeLoan), id.ToString(), $"₹{loan.Amount:N0} for {loan.Employee.EmployeeCode}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RecordRepaymentAsync(int id, decimal amount, DateOnly date, string? note, CancellationToken ct = default)
    {
        var loan = await db.EmployeeLoans.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (loan is null) return Result.Fail("Loan not found.");
        if (loan.Status != LoanStatus.Active) return Result.Fail("Only active loans can be repaid.");
        if (amount <= 0 || amount > loan.Outstanding) return Result.Fail($"Enter an amount between 1 and {loan.Outstanding:N0}.");
        loan.Outstanding -= amount;
        if (loan.Outstanding == 0) loan.Status = LoanStatus.Closed;
        db.LoanRepayments.Add(new LoanRepayment { EmployeeLoanId = id, Amount = amount, Date = date, Note = note?.Trim() ?? "Manual repayment" });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static IQueryable<LoanRow> Project(IQueryable<EmployeeLoan> q) =>
        q.Select(l => new LoanRow(l.Id, l.EmployeeId, l.Employee.FirstName + " " + l.Employee.LastName, l.Employee.EmployeeCode, l.Type, l.Amount,
            l.InstallmentAmount, l.RecoveryStart, l.Outstanding, l.Status, l.Reason, l.DecisionNotes, l.CreatedAtUtc));

    public async Task<PagedResult<LoanRow>> ListAsync(LoanStatus? status, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.EmployeeLoans.AsNoTracking();
        if (status is { } s) q = q.Where(l => l.Status == s);
        var total = await q.CountAsync(ct);
        return new PagedResult<LoanRow>(await Project(q.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), total, page, size);
    }

    public async Task<IReadOnlyList<LoanRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        await Project(db.EmployeeLoans.AsNoTracking().Where(l => l.EmployeeId == employeeId).OrderByDescending(l => l.Id)).ToListAsync(ct);
}

// ------------------------------------------------------------------ tax declarations

public sealed class DeclarationInput
{
    public TaxRegime Regime { get; set; } = TaxRegime.New;
    [Range(0, 100_000_000), Display(Name = "80C investments (PPF, ELSS, LIC, tuition…)")] public decimal Section80C { get; set; }
    [Range(0, 100_000_000), Display(Name = "80D medical insurance")] public decimal Section80D { get; set; }
    [Range(0, 100_000_000), Display(Name = "80CCD(1B) NPS")] public decimal Section80CCD1B { get; set; }
    [Range(0, 100_000_000), Display(Name = "Home-loan interest (24b)")] public decimal HomeLoanInterest { get; set; }
    [Range(0, 100_000_000), Display(Name = "Other deductions")] public decimal OtherDeductions { get; set; }
    [Range(0, 10_000_000), Display(Name = "Monthly rent paid")] public decimal MonthlyRentPaid { get; set; }
    [Display(Name = "Living in a metro city")] public bool MetroCity { get; set; }
    [Range(0, 100_000_000), Display(Name = "Income from previous employer (this FY)")] public decimal PreviousEmployerIncome { get; set; }
    [Range(0, 100_000_000), Display(Name = "TDS by previous employer")] public decimal PreviousEmployerTds { get; set; }
    public bool Submit { get; set; }
}

public sealed record TaxEstimate(TaxRegime Regime, decimal AnnualGross, decimal Deductions, TaxComputation Computation);

public interface ITaxDeclarationService
{
    Task<(TaxDeclaration? Declaration, int FinancialYear)> MineAsync(int employeeId, CancellationToken ct = default);
    Task<Result> SaveAsync(int employeeId, DeclarationInput input, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result> ReopenAsync(int id, CancellationToken ct = default);
    Task<PagedResult<TaxDeclaration>> ListAsync(int financialYear, DeclarationStatus? status, int page, CancellationToken ct = default);
    /// <summary>Annual tax under both regimes from the current salary and declaration (guidance for the employee).</summary>
    Task<IReadOnlyList<TaxEstimate>> CompareRegimesAsync(int employeeId, CancellationToken ct = default);
    Task<int> CurrentFinancialYearAsync(CancellationToken ct = default);
}

public sealed class TaxDeclarationService(IAppDbContext db, ICurrentContext context, IClock clock, IPayrollSetupService setup, IStatutoryRuleService statutory) : ITaxDeclarationService
{
    public async Task<int> CurrentFinancialYearAsync(CancellationToken ct = default)
    {
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        return PayrollEngine.FinancialYearOf(today.Year, today.Month);
    }

    public async Task<(TaxDeclaration?, int)> MineAsync(int employeeId, CancellationToken ct = default)
    {
        var fy = await CurrentFinancialYearAsync(ct);
        return (await db.TaxDeclarations.AsNoTracking().FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.FinancialYear == fy, ct), fy);
    }

    public async Task<Result> SaveAsync(int employeeId, DeclarationInput i, CancellationToken ct = default)
    {
        var fy = await CurrentFinancialYearAsync(ct);
        var d = await db.TaxDeclarations.FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.FinancialYear == fy, ct);
        if (d?.Status == DeclarationStatus.Approved) return Result.Fail("Your declaration is approved. Ask payroll to reopen it to make changes.");
        d ??= db.TaxDeclarations.Add(new TaxDeclaration { EmployeeId = employeeId, FinancialYear = fy }).Entity;
        d.Regime = i.Regime; d.Section80C = i.Section80C; d.Section80D = i.Section80D; d.Section80CCD1B = i.Section80CCD1B; d.HomeLoanInterest = i.HomeLoanInterest;
        d.OtherDeductions = i.OtherDeductions; d.MonthlyRentPaid = i.MonthlyRentPaid; d.MetroCity = i.MetroCity;
        d.PreviousEmployerIncome = i.PreviousEmployerIncome; d.PreviousEmployerTds = i.PreviousEmployerTds;
        d.Status = i.Submit ? DeclarationStatus.Submitted : DeclarationStatus.Draft;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ApproveAsync(int id, CancellationToken ct = default)
    {
        var d = await db.TaxDeclarations.Include(t => t.Employee).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (d is null) return Result.Fail("Declaration not found.");
        if (d.Status != DeclarationStatus.Submitted) return Result.Fail("Only submitted declarations can be approved.");
        if (d.Employee.UserId == context.UserId) return Result.Fail("You cannot approve your own declaration.");
        d.Status = DeclarationStatus.Approved;
        d.ReviewedBy = context.UserName;
        d.ReviewedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReopenAsync(int id, CancellationToken ct = default)
    {
        var d = await db.TaxDeclarations.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (d is null) return Result.Fail("Declaration not found.");
        d.Status = DeclarationStatus.Draft;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<PagedResult<TaxDeclaration>> ListAsync(int financialYear, DeclarationStatus? status, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.TaxDeclarations.AsNoTracking().Include(t => t.Employee).Where(t => t.FinancialYear == financialYear);
        if (status is { } s) q = q.Where(t => t.Status == s);
        var total = await q.CountAsync(ct);
        return new PagedResult<TaxDeclaration>(await q.OrderBy(t => t.Employee.FirstName).Skip((page - 1) * size).Take(size).ToListAsync(ct), total, page, size);
    }

    public async Task<IReadOnlyList<TaxEstimate>> CompareRegimesAsync(int employeeId, CancellationToken ct = default)
    {
        var fy = await CurrentFinancialYearAsync(ct);
        var salary = await setup.SalaryAsOfAsync(employeeId, new DateOnly(fy + 1, 3, 31), ct);
        if (salary is null) return [];
        var defs = await setup.DefinitionsAsync(salary.SalaryStructureId, ct);
        var (amounts, error) = PayrollEngine.Breakup(PayrollEngine.R(salary.AnnualGross / 12m), defs);
        if (error is not null) return [];
        var decl = await db.TaxDeclarations.AsNoTracking().FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.FinancialYear == fy, ct);
        var annualTaxable = amounts.Where(a => a.Component.Type == ComponentType.Earning && a.Component.IsTaxable).Sum(a => a.MonthlyAmount) * 12 + (decl?.PreviousEmployerIncome ?? 0);
        var rates = await statutory.RatesAsOfAsync(clock.Today, ct);
        var settings = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
        var pfWages = amounts.Where(a => a.Component.IsPfWage).Sum(a => a.MonthlyAmount);
        var annualPf = rates is null || !salary.PfApplicable || !settings.PfEnabled ? 0
            : PayrollEngine.R((settings.PfRestrictToCeiling ? Math.Min(pfWages, rates.PfWageCeiling) : pfWages) * rates.PfEmployeeRate / 100m) * 12;

        var list = new List<TaxEstimate>();
        foreach (var regime in new[] { TaxRegime.New, TaxRegime.Old })
        {
            var rule = await statutory.TaxRuleAsync(fy, regime, ct);
            if (rule is null) continue;
            decimal deductions = rule.StandardDeduction;
            if (regime == TaxRegime.Old && decl is not null)
            {
                var basic = amounts.Where(a => a.Component.IsBasic).Sum(a => a.MonthlyAmount) * 12;
                var hra = amounts.Where(a => a.Component.IsHra).Sum(a => a.MonthlyAmount) * 12;
                if (decl.MonthlyRentPaid > 0 && hra > 0)
                    deductions += Math.Max(0, Math.Min(hra, Math.Min(decl.MonthlyRentPaid * 12 - basic * 0.10m, basic * (decl.MetroCity ? 0.50m : 0.40m))));
                deductions += Math.Min(decl.Section80C + annualPf, rule.Section80CLimit) + Math.Min(decl.Section80D, rule.Section80DLimit)
                              + Math.Min(decl.Section80CCD1B, rule.Section80CCD1BLimit) + Math.Min(decl.HomeLoanInterest, rule.HomeLoanInterestLimit) + decl.OtherDeductions;
            }
            else if (regime == TaxRegime.Old) deductions += Math.Min(annualPf, rule.Section80CLimit);
            list.Add(new TaxEstimate(regime, annualTaxable, deductions, TaxCalculator.Compute(Math.Max(0, annualTaxable - deductions), rule)));
        }
        return list;
    }
}

// ------------------------------------------------------------------ gratuity & full and final

public sealed record GratuityRow(int EmployeeId, string Name, string Code, DateOnly Joined, decimal ServiceYears, int CountedYears, decimal MonthlyBasic, bool Eligible, decimal Amount);

public interface ISettlementService
{
    Task<IReadOnlyList<GratuityRow>> GratuityLiabilityAsync(CancellationToken ct = default);
    Task<IReadOnlyList<(int EmployeeId, string Name, string Code, DateOnly? Lwd, FullAndFinalSettlement? Settlement)>> CandidatesAsync(CancellationToken ct = default);
    Task<Result<int>> PrepareAsync(int employeeId, CancellationToken ct = default);
    Task<FullAndFinalSettlement?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> AddLineAsync(int id, string description, decimal amount, CancellationToken ct = default);
    Task<Result> RemoveLineAsync(int id, int lineId, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, string? notes, CancellationToken ct = default);
    Task<Result> MarkPaidAsync(int id, DateOnly paidOn, CancellationToken ct = default);
}

public sealed class SettlementService(IAppDbContext db, ICurrentContext context, IClock clock, IPayrollSetupService setup, IStatutoryRuleService statutory,
    IAuditWriter audit, ILeaveService leave) : ISettlementService
{
    private async Task<decimal> MonthlyBasicAsync(int employeeId, DateOnly asOf, CancellationToken ct)
    {
        var salary = await setup.SalaryAsOfAsync(employeeId, asOf, ct);
        if (salary is null) return 0;
        var (amounts, _) = PayrollEngine.Breakup(PayrollEngine.R(salary.AnnualGross / 12m), await setup.DefinitionsAsync(salary.SalaryStructureId, ct));
        return amounts.Where(a => a.Component.IsBasic).Sum(a => a.MonthlyAmount);
    }

    public async Task<IReadOnlyList<GratuityRow>> GratuityLiabilityAsync(CancellationToken ct = default)
    {
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var rates = await statutory.RatesAsOfAsync(today, ct);
        if (rates is null) return [];
        var employees = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .OrderBy(e => e.DateOfJoining).Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, e.DateOfJoining }).ToListAsync(ct);
        var rows = new List<GratuityRow>();
        foreach (var e in employees)
        {
            var (years, counted) = PayrollEngine.ServiceFor(e.DateOfJoining, today);
            var basic = await MonthlyBasicAsync(e.Id, today, ct);
            rows.Add(new GratuityRow(e.Id, e.Name, e.EmployeeCode, e.DateOfJoining, years, counted, basic, years >= rates.GratuityMinServiceYears, PayrollEngine.Gratuity(basic, counted, rates)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<(int, string, string, DateOnly?, FullAndFinalSettlement?)>> CandidatesAsync(CancellationToken ct = default)
    {
        var leavers = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Exited || e.Status == EmployeeStatus.NoticePeriod)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, e.DateOfExit,
                Lwd = db.ExitRequests.Where(x => x.EmployeeId == e.Id && (x.Status == ExitStatus.Approved || x.Status == ExitStatus.Completed)).OrderByDescending(x => x.Id).Select(x => x.ApprovedLastWorkingDay).FirstOrDefault() })
            .ToListAsync(ct);
        var settlements = await db.FullAndFinalSettlements.AsNoTracking().ToDictionaryAsync(s => s.EmployeeId, ct);
        return leavers.OrderByDescending(l => l.DateOfExit ?? l.Lwd).Select(l => (l.Id, l.Name, l.EmployeeCode, l.DateOfExit ?? l.Lwd, settlements.GetValueOrDefault(l.Id))).ToList();
    }

    public async Task<Result<int>> PrepareAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return Result<int>.Fail("Employee not found.");
        var lwd = e.DateOfExit ?? await db.ExitRequests.Where(x => x.EmployeeId == employeeId && (x.Status == ExitStatus.Approved || x.Status == ExitStatus.Completed))
            .OrderByDescending(x => x.Id).Select(x => x.ApprovedLastWorkingDay).FirstOrDefaultAsync(ct);
        if (lwd is null) return Result<int>.Fail("The employee has no approved exit.");
        var existing = await db.FullAndFinalSettlements.Include(s => s.Lines).FirstOrDefaultAsync(s => s.EmployeeId == employeeId, ct);
        if (existing is { Status: not SettlementStatus.Draft }) return Result<int>.Fail("The settlement is already approved.");

        var rates = await statutory.RatesAsOfAsync(lwd.Value, ct);
        if (rates is null) return Result<int>.Fail("No statutory rate set is configured.");
        var settings = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
        var basic = await MonthlyBasicAsync(employeeId, lwd.Value, ct);
        var lines = new List<SettlementLine>();

        // Unused encashable leave (types allowing encashment), paid at the daily basic rate.
        await leave.EnsureBalancesAsync(lwd.Value.Year, [employeeId], ct);
        var balances = await db.LeaveBalances.AsNoTracking().Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == employeeId && b.Year == lwd.Value.Year && b.LeaveType.IsPaid && b.LeaveType.MaxEncashableDaysPerYear > 0).ToListAsync(ct);
        foreach (var b in balances.Where(b => b.Available > 0))
            lines.Add(new SettlementLine { Description = $"Leave encashment: {b.Available:0.#} day(s) {b.LeaveType.Name}", Amount = PayrollEngine.R(b.Available * basic / settings.DailyRateDivisor), SortOrder = 10 });

        var (years, counted) = PayrollEngine.ServiceFor(e.DateOfJoining, lwd.Value);
        if (years >= rates.GratuityMinServiceYears)
            lines.Add(new SettlementLine { Description = $"Gratuity ({counted} year(s) × {rates.GratuityDaysPerYear:0.#}/{rates.GratuityWageDivisor:0.#} × basic ₹{basic:N0})", Amount = PayrollEngine.Gratuity(basic, counted, rates), SortOrder = 20 });

        var loans = await db.EmployeeLoans.AsNoTracking().Where(l => l.EmployeeId == employeeId && l.Status == LoanStatus.Active && l.Outstanding > 0).ToListAsync(ct);
        foreach (var l in loans)
            lines.Add(new SettlementLine { Description = $"Recovery: outstanding {(l.Type == LoanType.Loan ? "loan" : "salary advance")}", Amount = -l.Outstanding, SortOrder = 30 });

        if (existing is not null)
        {
            db.SettlementLines.RemoveRange(existing.Lines.Where(x => !x.IsManual));
            foreach (var l in lines) existing.Lines.Add(l);
            existing.LastWorkingDay = lwd.Value;
            Recalculate(existing);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(existing.Id);
        }
        var s = new FullAndFinalSettlement { EmployeeId = employeeId, LastWorkingDay = lwd.Value, Lines = lines };
        Recalculate(s);
        db.FullAndFinalSettlements.Add(s);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(s.Id);
    }

    private static void Recalculate(FullAndFinalSettlement s)
    {
        var active = s.Lines.Where(l => !l.IsDeleted).ToList();
        s.TotalPayable = active.Where(l => l.Amount > 0).Sum(l => l.Amount);
        s.TotalRecoverable = -active.Where(l => l.Amount < 0).Sum(l => l.Amount);
        s.NetAmount = s.TotalPayable - s.TotalRecoverable;
    }

    public Task<FullAndFinalSettlement?> GetAsync(int id, CancellationToken ct = default) =>
        db.FullAndFinalSettlements.AsNoTracking().Include(s => s.Employee).Include(s => s.Lines.OrderBy(l => l.SortOrder)).FirstOrDefaultAsync(s => s.Id == id, ct);

    private async Task<(FullAndFinalSettlement? S, string? Error)> DraftAsync(int id, CancellationToken ct)
    {
        var s = await db.FullAndFinalSettlements.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return (null, "Settlement not found.");
        return s.Status != SettlementStatus.Draft ? (null, "The settlement is approved and locked.") : (s, null);
    }

    public async Task<Result> AddLineAsync(int id, string description, decimal amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > 200) return Result.Fail("Enter a description (max 200 characters).");
        if (amount == 0) return Result.Fail("Enter a non-zero amount (negative to recover).");
        var (s, error) = await DraftAsync(id, ct);
        if (s is null) return Result.Fail(error!);
        s.Lines.Add(new SettlementLine { Description = description.Trim(), Amount = Math.Round(amount, 2), IsManual = true, SortOrder = 50 });
        Recalculate(s);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveLineAsync(int id, int lineId, CancellationToken ct = default)
    {
        var (s, error) = await DraftAsync(id, ct);
        if (s is null) return Result.Fail(error!);
        var line = s.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return Result.Fail("Line not found.");
        db.SettlementLines.Remove(line);
        line.IsDeleted = true;
        Recalculate(s);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ApproveAsync(int id, string? notes, CancellationToken ct = default)
    {
        var (s, error) = await DraftAsync(id, ct);
        if (s is null) return Result.Fail(error!);
        s.Status = SettlementStatus.Approved;
        s.ApprovedBy = context.UserName;
        s.ApprovedAtUtc = clock.UtcNow;
        s.Notes = notes?.Trim();
        audit.Add("Settlement.Approve", nameof(FullAndFinalSettlement), id.ToString(), $"Net ₹{s.NetAmount:N0}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarkPaidAsync(int id, DateOnly paidOn, CancellationToken ct = default)
    {
        var s = await db.FullAndFinalSettlements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Settlement not found.");
        if (s.Status != SettlementStatus.Approved) return Result.Fail("Approve the settlement first.");
        s.Status = SettlementStatus.Paid;
        s.PaidOn = paidOn;
        // Recoveries settle outstanding loans.
        foreach (var loan in await db.EmployeeLoans.Where(l => l.EmployeeId == s.EmployeeId && l.Status == LoanStatus.Active && l.Outstanding > 0).ToListAsync(ct))
        {
            db.LoanRepayments.Add(new LoanRepayment { EmployeeLoanId = loan.Id, Amount = loan.Outstanding, Date = paidOn, Note = "Recovered in full & final settlement" });
            loan.Outstanding = 0;
            loan.Status = LoanStatus.Closed;
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
