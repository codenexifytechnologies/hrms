using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

public sealed record PayrollRunRow(int Id, int Year, int Month, PayrollRunStatus Status, int Employees, decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, DateTime? FinalizedAtUtc, DateOnly? PaidOn);
public sealed record PayrollDetailRow(int Id, int EmployeeId, string Name, string Code, string? Department, decimal PaidDays, decimal LopDays, decimal? LopOverride,
    decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, bool OnHold, string? HoldReason, string? Warnings, decimal Tds);

public sealed class Payslip
{
    public required PayrollDetail Detail { get; init; }
    public required PayrollRun Run { get; init; }
    public required Employee Employee { get; init; }
    public required string OrganizationName { get; init; }
    public string? OrganizationAddress { get; init; }
    public string? BankLast4 { get; init; }
    public string? BankName { get; init; }
}

public interface IPayrollRunService
{
    Task<IReadOnlyList<PayrollRunRow>> RunsAsync(CancellationToken ct = default);
    Task<Result<int>> CreateAsync(int year, int month, CancellationToken ct = default);
    Task<PayrollRun?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollDetailRow>> DetailsAsync(int runId, CancellationToken ct = default);
    Task<Result> ProcessAsync(int runId, CancellationToken ct = default);
    Task<Result> SetLopOverrideAsync(int detailId, decimal? lopDays, CancellationToken ct = default);
    Task<Result> SetHoldAsync(int detailId, bool hold, string? reason, CancellationToken ct = default);
    Task<Result> FinalizeAsync(int runId, CancellationToken ct = default);
    Task<Result> MarkPaidAsync(int runId, DateOnly paidOn, CancellationToken ct = default);
    Task<Result> DeleteAsync(int runId, CancellationToken ct = default);
    /// <param name="employeeId">When set (self-service), only that employee's published payslip is returned.</param>
    Task<Payslip?> PayslipAsync(int detailId, int? employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<(int DetailId, int Year, int Month, decimal Net)>> MyPayslipsAsync(int employeeId, CancellationToken ct = default);
    Task<ExportFile> ExportAsync(int runId, string kind, CancellationToken ct = default);
}

public sealed class PayrollRunService(
    IAppDbContext db, ICurrentContext context, IClock clock, IPayrollSetupService setup, IStatutoryRuleService statutory,
    IFeatureAccessService features, ISecretProtector protector, IExcelExporter excel, INotificationService notifications, IAuditWriter audit,
    IAttendanceService attendanceService, Benefits.IBenefitService benefits) : IPayrollRunService
{
    private int OrgId => context.OrgId();

    public async Task<IReadOnlyList<PayrollRunRow>> RunsAsync(CancellationToken ct = default) =>
        await db.PayrollRuns.AsNoTracking().OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new PayrollRunRow(r.Id, r.Year, r.Month, r.Status, r.EmployeeCount, r.TotalGross, r.TotalDeductions, r.TotalNet, r.TotalEmployerCost, r.FinalizedAtUtc, r.PaidOn))
            .ToListAsync(ct);

    public async Task<Result<int>> CreateAsync(int year, int month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12 || year < 2000) return Result<int>.Fail("Choose a valid month.");
        var today = (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        if (new DateOnly(year, month, 1) > today) return Result<int>.Fail("Payroll cannot be run for a future month.");
        if (await db.PayrollRuns.AnyAsync(r => r.Year == year && r.Month == month, ct)) return Result<int>.Fail("A payroll run for this month already exists.");
        if (await db.PayrollRuns.AnyAsync(r => (r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid) && r.Year * 100 + r.Month > year * 100 + month, ct))
            return Result<int>.Fail("A later month is already finalized. Earlier months cannot be added afterwards.");
        var run = new PayrollRun { Year = year, Month = month };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(run.Id);
    }

    public Task<PayrollRun?> GetAsync(int id, CancellationToken ct = default) => db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<PayrollDetailRow>> DetailsAsync(int runId, CancellationToken ct = default) =>
        await db.PayrollDetails.AsNoTracking().Where(d => d.PayrollRunId == runId)
            .OrderBy(d => d.Employee.FirstName).ThenBy(d => d.Employee.LastName)
            .Select(d => new PayrollDetailRow(d.Id, d.EmployeeId, d.Employee.FirstName + " " + d.Employee.LastName, d.Employee.EmployeeCode,
                d.Employee.Department != null ? d.Employee.Department.Name : null, d.PaidDays, d.LopDays, d.LopOverride, d.GrossEarnings,
                d.TotalDeductions, d.NetPay, d.EmployerContributions, d.OnHold, d.HoldReason, d.Warnings,
                d.Lines.Where(l => l.Code == "TDS").Sum(l => (decimal?)l.Amount) ?? 0))
            .ToListAsync(ct);

    // ------------------------------------------------------------------ processing
    public async Task<Result> ProcessAsync(int runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return Result.Fail("Payroll run not found.");
        if (run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid) return Result.Fail("This payroll is finalized and cannot be re-processed.");

        var org = await db.OrgClockAsync(OrgId, clock, ct);
        var start = new DateOnly(run.Year, run.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        if (start > org.Today) return Result.Fail("Payroll cannot be processed for a future month.");
        var enabled = await features.GetEnabledFeaturesAsync(OrgId, ct);
        var settings = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
        var rates = await statutory.RatesAsOfAsync(end, ct);
        if (rates is null) return Result.Fail("No statutory rate set is effective for this month. Ask the platform owner to configure one.");
        var statutoryOn = enabled.Contains(FeatureCodes.Statutory);
        var orgState = await db.Organizations.Where(o => o.Id == OrgId).Select(o => o.State).FirstOrDefaultAsync(ct);

        var employees = await db.Employees.AsNoTracking().Include(e => e.Branch)
            .Where(e => e.DateOfJoining <= end && (e.DateOfExit == null || e.DateOfExit >= start) && e.Status != EmployeeStatus.Inactive)
            .ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();
        var salaries = (await db.EmployeeSalaries.AsNoTracking().Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= end).ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());

        // Attendance is calculated lazily; make sure the whole month (up to today) is current before reading LOP.
        if (enabled.Contains(FeatureCodes.Attendance) && ids.Count > 0)
            await attendanceService.RecalculateRangeAsync(ids, start, end < org.Today ? end : org.Today, ct);
        var attendance = enabled.Contains(FeatureCodes.Attendance)
            ? (await db.Attendance.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.Date >= start && a.Date <= end).Select(a => new { a.EmployeeId, a.Date, a.Status }).ToListAsync(ct))
                .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.ToList())
            : [];
        var calendar = await WorkCalendar.LoadAsync(db, start, end, ids, ct);
        var unpaidLeaves = enabled.Contains(FeatureCodes.Leave)
            ? await db.LeaveApplications.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.Status == LeaveStatus.Approved && !a.LeaveType.IsPaid && a.FromDate <= end && a.ToDate >= start)
                .Select(a => new { a.EmployeeId, a.FromDate, a.ToDate, a.IsHalfDay }).ToListAsync(ct)
            : [];
        var overtime = enabled.Contains(FeatureCodes.Overtime)
            ? (await db.OvertimeRequests.AsNoTracking().Where(o => ids.Contains(o.EmployeeId) && o.Status == OvertimeStatus.Approved && o.Date >= start && o.Date <= end)
                .Select(o => new { o.EmployeeId, Minutes = o.ApprovedMinutes ?? o.Minutes }).ToListAsync(ct)).GroupBy(o => o.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Minutes))
            : [];
        var encash = enabled.Contains(FeatureCodes.Leave)
            ? (await db.LeaveBalances.AsNoTracking().Where(b => ids.Contains(b.EmployeeId) && b.Encashed > b.EncashmentPaid).Select(b => new { b.EmployeeId, Days = b.Encashed - b.EncashmentPaid }).ToListAsync(ct))
                .GroupBy(b => b.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Days))
            : [];
        var loans = enabled.Contains(FeatureCodes.Loans)
            ? (await db.EmployeeLoans.AsNoTracking().Where(l => ids.Contains(l.EmployeeId) && l.Status == LoanStatus.Active && l.Outstanding > 0 && l.RecoveryStart <= start).ToListAsync(ct))
                .GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList())
            : [];

        var fy = PayrollEngine.FinancialYearOf(run.Year, run.Month);
        var fyStartKey = fy * 100 + 4;
        var thisKey = run.Year * 100 + run.Month;
        // One-time items for this month, plus any earlier ones not yet paid (e.g. the salary was on hold).
        var oneTime = (await db.PayrollAdjustments.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.SettledRunId == null && a.Year * 100 + a.Month <= thisKey).ToListAsync(ct))
            .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Id).ToList());
        // Perks & benefits: allowances paid and employee contributions deducted for memberships active in the month.
        var benefitItems = enabled.Contains(FeatureCodes.Benefits)
            ? await benefits.PayrollItemsAsync(ids, start, end, ct)
            : new Dictionary<int, IReadOnlyList<OneTimeItem>>();
        var tdsOn =settings.TdsEnabled && enabled.Contains(FeatureCodes.EmployeeTax);
        var ytd = tdsOn
            ? (await db.PayrollDetails.AsNoTracking()
                .Where(d => ids.Contains(d.EmployeeId) && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid)
                            && d.Run.Year * 100 + d.Run.Month >= fyStartKey && d.Run.Year * 100 + d.Run.Month < thisKey)
                .Select(d => new
                {
                    d.EmployeeId, d.TaxableEarnings,
                    Tds = d.Lines.Where(l => l.Code == "TDS").Sum(l => (decimal?)l.Amount) ?? 0,
                    Pt = d.Lines.Where(l => l.Code == "PT").Sum(l => (decimal?)l.Amount) ?? 0,
                    Pf = d.Lines.Where(l => l.Code == "PF").Sum(l => (decimal?)l.Amount) ?? 0
                }).ToListAsync(ct)).GroupBy(x => x.EmployeeId).ToDictionary(g => g.Key, g => (Taxable: g.Sum(x => x.TaxableEarnings), Tds: g.Sum(x => x.Tds), Pt: g.Sum(x => x.Pt), Pf: g.Sum(x => x.Pf)))
            : [];
        var declarations = tdsOn
            ? await db.TaxDeclarations.AsNoTracking().Where(t => ids.Contains(t.EmployeeId) && t.FinancialYear == fy && t.Status != DeclarationStatus.Draft).ToDictionaryAsync(t => t.EmployeeId, ct)
            : [];
        var taxRules = new Dictionary<TaxRegime, IncomeTaxRule?>
        {
            [TaxRegime.New] = tdsOn ? await statutory.TaxRuleAsync(fy, TaxRegime.New, ct) : null,
            [TaxRegime.Old] = tdsOn ? await statutory.TaxRuleAsync(fy, TaxRegime.Old, ct) : null
        };
        var definitions = new Dictionary<int, IReadOnlyList<ComponentDef>>();
        var ptCache = new Dictionary<string, IReadOnlyList<ProfessionalTaxSlab>>();
        var lwfCache = new Dictionary<string, LabourWelfareFundRule?>();

        // Keep HR overrides across re-processing, then replace the previous calculation.
        var previous = await db.PayrollDetails.Include(d => d.Lines).Where(d => d.PayrollRunId == runId).ToListAsync(ct);
        var keep = previous.ToDictionary(d => d.EmployeeId, d => (d.LopOverride, d.OnHold, d.HoldReason));
        foreach (var d in previous) { db.PayrollLines.RemoveRange(d.Lines); db.PayrollDetails.Remove(d); }
        await db.SaveChangesAsync(ct);

        var daysInMonth = end.Day;
        var details = new List<PayrollDetail>();
        foreach (var e in employees)
        {
            if (!salaries.TryGetValue(e.Id, out var salary)) continue;
            if (!definitions.TryGetValue(salary.SalaryStructureId, out var defs))
                definitions[salary.SalaryStructureId] = defs = await setup.DefinitionsAsync(salary.SalaryStructureId, ct);

            var from = e.DateOfJoining > start ? e.DateOfJoining : start;
            var to = e.DateOfExit is { } x && x < end ? x : end;
            var employedDays = to.DayNumber - from.DayNumber + 1;
            var unpaidOutsideEmployment = daysInMonth - employedDays;

            decimal lop = 0;
            if (attendance.TryGetValue(e.Id, out var days))
                lop += days.Where(d => d.Date >= from && d.Date <= to).Sum(d => d.Status == AttendanceStatus.Absent ? 1m : d.Status == AttendanceStatus.HalfDay ? 0.5m : 0m);
            foreach (var l in unpaidLeaves.Where(l => l.EmployeeId == e.Id))
            {
                if (l.IsHalfDay) { if (l.FromDate >= from && l.FromDate <= to) lop += 0.5m; continue; }
                lop += calendar.WorkingDays(e.Id, l.FromDate > from ? l.FromDate : from, l.ToDate < to ? l.ToDate : to);
            }
            keep.TryGetValue(e.Id, out var kept);
            var lopDays = kept.LopOverride ?? lop;
            var paidDays = Math.Max(0, employedDays - lopDays);

            var monthlyGross = PayrollEngine.R(salary.AnnualGross / 12m);
            var (amounts, breakupError) = PayrollEngine.Breakup(monthlyGross, defs);
            var basicFull = amounts.Where(a => a.Component.IsBasic).Sum(a => a.MonthlyAmount);
            var hourly = monthlyGross / settings.DailyRateDivisor / 8m;
            var otPay = overtime.TryGetValue(e.Id, out var otMinutes) ? otMinutes / 60m * hourly * settings.OvertimeRateMultiplier : 0;
            var encashDays = encash.GetValueOrDefault(e.Id);
            var encashPay = encashDays * basicFull / settings.DailyRateDivisor;
            var loanLines = loans.TryGetValue(e.Id, out var empLoans)
                ? empLoans.Select(l => ($"LOAN{l.Id}", l.Type == LoanType.SalaryAdvance ? "Salary advance recovery" : "Loan recovery", Math.Min(l.InstallmentAmount, l.Outstanding))).ToList()
                : [];

            var state = e.Branch?.State ?? settings.DefaultState ?? orgState;
            if (state is not null && !ptCache.ContainsKey(state))
            {
                ptCache[state] = await statutory.PtSlabsForAsync(state, end, ct);
                lwfCache[state] = await statutory.LwfForAsync(state, end, ct);
            }
            declarations.TryGetValue(e.Id, out var decl);
            var regime = decl?.Regime ?? TaxRegime.New;
            var rule = taxRules[regime] ?? taxRules[regime == TaxRegime.New ? TaxRegime.Old : TaxRegime.New];
            var y = ytd.GetValueOrDefault(e.Id);

            var result = PayrollEngine.Calculate(new PayInput
            {
                Month = run.Month, DaysInMonth = daysInMonth, PaidDays = paidDays, AnnualGross = salary.AnnualGross, Components = defs, Rates = rates,
                PfApplicable = salary.PfApplicable, PfEnabled = statutoryOn && settings.PfEnabled, PfRestrictToCeiling = settings.PfRestrictToCeiling,
                EsiEnabled = statutoryOn && settings.EsiEnabled, PtEnabled = statutoryOn && settings.PtEnabled, LwfEnabled = statutoryOn && settings.LwfEnabled,
                PtSlabs = state is null ? [] : ptCache[state], Lwf = state is null ? null : lwfCache[state], Gender = e.Gender,
                OvertimePay = otPay, LeaveEncashmentPay = encashPay, LoanDeductions = loanLines,
                OneTimeItems = (oneTime.TryGetValue(e.Id, out var items)
                        ? items.Select(a => new OneTimeItem($"ADJ{a.Id}", a.Name, a.IsDeduction, a.Amount, !a.IsDeduction && a.IsTaxable)).ToList()
                        : [])
                    .Concat(benefitItems.TryGetValue(e.Id, out var perks) ? perks : []).ToList(),
                Tax = rule is null ? null : new TaxInputs
                {
                    Rule = rule, Declaration = decl, YtdTaxableEarnings = y.Taxable, YtdTds = y.Tds, YtdProfessionalTax = y.Pt, YtdEmployeePf = y.Pf,
                    RemainingMonthsAfterThis = PayrollEngine.RemainingMonthsAfter(run.Month)
                }
            });

            var warnings = result.Warnings.ToList();
            if (breakupError is not null && !warnings.Contains(breakupError)) warnings.Add(breakupError);
            if (tdsOn && rule is null) warnings.Add($"No income-tax rules for FY {fy}-{(fy + 1) % 100:00}; TDS not calculated.");
            if (statutoryOn && settings.PtEnabled && state is null) warnings.Add("No state on the branch or in payroll settings; PT/LWF skipped.");
            if (unpaidOutsideEmployment > 0) warnings.Add($"Employed {employedDays} of {daysInMonth} days this month.");

            details.Add(new PayrollDetail
            {
                PayrollRunId = runId, EmployeeId = e.Id, DaysInMonth = daysInMonth, PaidDays = paidDays, LopDays = lopDays, LopOverride = kept.LopOverride,
                OnHold = kept.OnHold || result.NetPay < 0 || breakupError is not null, HoldReason = kept.HoldReason ?? (result.NetPay < 0 ? "Negative net pay" : breakupError),
                MonthlyGross = result.MonthlyGross, GrossEarnings = result.GrossEarnings, TotalDeductions = result.TotalDeductions, NetPay = result.NetPay,
                EmployerContributions = result.EmployerContributions, PfWages = result.PfWages, EsiWages = result.EsiWages, TaxableEarnings = result.TaxableEarnings,
                ProjectedAnnualTax = result.ProjectedAnnualTax, Regime = result.Regime, State = state, LeaveEncashmentDays = encashDays,
                Warnings = warnings.Count == 0 ? null : string.Join(" ", warnings) is { Length: > 1000 } w ? w[..1000] : string.Join(" ", warnings),
                Lines = result.Lines.Select(l => new PayrollLine { Code = l.Code, Name = l.Name, Kind = l.Kind, Amount = l.Amount, FullAmount = l.FullAmount, IsTaxable = l.IsTaxable, SortOrder = l.SortOrder }).ToList()
            });
        }
        db.PayrollDetails.AddRange(details);
        run.Status = PayrollRunStatus.Processed;
        run.ProcessedAtUtc = clock.UtcNow;
        Totals(run, details);
        await db.SaveChangesAsync(ct);
        var missing = employees.Count(e => !salaries.ContainsKey(e.Id));
        return missing > 0 ? Result.Fail($"Processed {details.Count} employee(s). {missing} employee(s) have no salary assigned and were skipped.") : Result.Ok();
    }

    private static void Totals(PayrollRun run, IReadOnlyCollection<PayrollDetail> details)
    {
        run.EmployeeCount = details.Count;
        run.TotalGross = details.Sum(d => d.GrossEarnings);
        run.TotalDeductions = details.Sum(d => d.TotalDeductions);
        run.TotalNet = details.Sum(d => d.NetPay);
        run.TotalEmployerCost = details.Sum(d => d.GrossEarnings + d.EmployerContributions);
    }

    private async Task<(PayrollDetail? Detail, string? Error)> EditableDetailAsync(int detailId, CancellationToken ct)
    {
        var d = await db.PayrollDetails.Include(x => x.Run).FirstOrDefaultAsync(x => x.Id == detailId, ct);
        if (d is null) return (null, "Payroll entry not found.");
        if (d.Run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid) return (null, "This payroll is finalized.");
        return (d, null);
    }

    public async Task<Result> SetLopOverrideAsync(int detailId, decimal? lopDays, CancellationToken ct = default)
    {
        var (d, error) = await EditableDetailAsync(detailId, ct);
        if (d is null) return Result.Fail(error!);
        if (lopDays is { } v && (v < 0 || v > d.DaysInMonth || v * 2 != Math.Floor(v * 2))) return Result.Fail($"LOP days must be between 0 and {d.DaysInMonth} in steps of 0.5.");
        d.LopOverride = lopDays;
        d.Run.Status = PayrollRunStatus.Draft; // needs re-processing
        await db.SaveChangesAsync(ct);
        return await ProcessAsync(d.PayrollRunId, ct);
    }

    public async Task<Result> SetHoldAsync(int detailId, bool hold, string? reason, CancellationToken ct = default)
    {
        var (d, error) = await EditableDetailAsync(detailId, ct);
        if (d is null) return Result.Fail(error!);
        if (hold && string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for holding the salary.");
        if (!hold && d.NetPay < 0) return Result.Fail("Net pay is negative; fix the deductions before releasing.");
        d.OnHold = hold;
        d.HoldReason = hold ? reason!.Trim() : null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> FinalizeAsync(int runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.Include(r => r.Details).ThenInclude(d => d.Lines).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return Result.Fail("Payroll run not found.");
        if (run.Status != PayrollRunStatus.Processed) return Result.Fail("Process the payroll (with no pending changes) before finalizing.");
        if (run.Details.Count == 0) return Result.Fail("There is nobody to pay in this run.");
        if (await db.PayrollRuns.AnyAsync(r => r.Year * 100 + r.Month < run.Year * 100 + run.Month && (r.Status == PayrollRunStatus.Draft || r.Status == PayrollRunStatus.Processed), ct))
            return Result.Fail("Finalize earlier months first.");

        var today = (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        foreach (var d in run.Details.Where(d => !d.OnHold))
        {
            foreach (var line in d.Lines.Where(l => l.Code.StartsWith("LOAN")))
            {
                if (!int.TryParse(line.Code[4..], out var loanId)) continue;
                var loan = await db.EmployeeLoans.FirstOrDefaultAsync(l => l.Id == loanId, ct);
                if (loan is null) continue;
                loan.Outstanding = Math.Max(0, loan.Outstanding - line.Amount);
                if (loan.Outstanding == 0) loan.Status = LoanStatus.Closed;
                db.LoanRepayments.Add(new LoanRepayment { EmployeeLoanId = loan.Id, Date = new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1), Amount = line.Amount, PayrollRunId = run.Id, Note = "Payroll deduction" });
            }
            // One-time items paid in this run are settled; expense claims paid through payroll become Paid.
            foreach (var line in d.Lines.Where(l => l.Code.StartsWith("ADJ")))
            {
                if (!int.TryParse(line.Code[3..], out var adjId)) continue;
                var adj = await db.PayrollAdjustments.FirstOrDefaultAsync(a => a.Id == adjId, ct);
                if (adj is null || adj.SettledRunId is not null) continue;
                adj.SettledRunId = run.Id;
                if (adj.Source == AdjustmentSource.Expense && adj.SourceId is int claimId
                    && await db.ExpenseClaims.FirstOrDefaultAsync(c => c.Id == claimId, ct) is { } claim)
                {
                    claim.Status = ExpenseStatus.Paid;
                    claim.PaidOn = new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1);
                    claim.PaymentReference = $"Payroll {run.Year}-{run.Month:00}";
                }
            }
            if (d.LeaveEncashmentDays > 0)
            {
                var remaining = d.LeaveEncashmentDays;
                foreach (var b in await db.LeaveBalances.Where(b => b.EmployeeId == d.EmployeeId && b.Encashed > b.EncashmentPaid).OrderBy(b => b.Year).ToListAsync(ct))
                {
                    var portion = Math.Min(remaining, b.Encashed - b.EncashmentPaid);
                    b.EncashmentPaid += portion;
                    remaining -= portion;
                    if (remaining <= 0) break;
                }
            }
        }
        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedAtUtc = clock.UtcNow;
        run.FinalizedBy = context.UserName;
        var monthName = new DateOnly(run.Year, run.Month, 1).ToString("MMMM yyyy");
        var paidEmployeeIds = run.Details.Where(d => !d.OnHold).Select(d => d.EmployeeId).ToList();
        var userIds = await db.Employees.Where(e => paidEmployeeIds.Contains(e.Id) && e.UserId != null)
            .Select(e => e.UserId!.Value).ToListAsync(ct);
        notifications.Queue(userIds, "Payslip available", $"Your payslip for {monthName} has been published.", "/me/payslips");
        audit.Add("Payroll.Finalize", nameof(PayrollRun), run.Id.ToString(), $"{monthName}: {run.EmployeeCount} employees, net {run.TotalNet:N0}");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail("The payroll changed while finalizing. Please reload and try again."); }
        return Result.Ok();
    }

    public async Task<Result> MarkPaidAsync(int runId, DateOnly paidOn, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return Result.Fail("Payroll run not found.");
        if (run.Status != PayrollRunStatus.Finalized) return Result.Fail("Only a finalized payroll can be marked as paid.");
        if (paidOn < new DateOnly(run.Year, run.Month, 1)) return Result.Fail("Payment date cannot be before the payroll month.");
        run.Status = PayrollRunStatus.Paid;
        run.PaidOn = paidOn;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int runId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.Include(r => r.Details).ThenInclude(d => d.Lines).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return Result.Fail("Payroll run not found.");
        if (run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid) return Result.Fail("Finalized payroll cannot be deleted.");
        foreach (var d in run.Details) { db.PayrollLines.RemoveRange(d.Lines); db.PayrollDetails.Remove(d); }
        db.PayrollRuns.Remove(run);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Payslip?> PayslipAsync(int detailId, int? employeeId, CancellationToken ct = default)
    {
        var q = db.PayrollDetails.AsNoTracking().Include(d => d.Lines).Include(d => d.Run)
            .Include(d => d.Employee).ThenInclude(e => e.Department).Include(d => d.Employee).ThenInclude(e => e.Designation)
            .Include(d => d.Employee).ThenInclude(e => e.BankDetail)
            .Where(d => d.Id == detailId);
        if (employeeId is int me)
            q = q.Where(d => d.EmployeeId == me && !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid));
        var d = await q.FirstOrDefaultAsync(ct);
        if (d is null) return null;
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == OrgId).Select(o => new { o.Name, o.AddressLine, o.City, o.State }).FirstAsync(ct);
        return new Payslip
        {
            Detail = d, Run = d.Run, Employee = d.Employee, OrganizationName = org.Name,
            OrganizationAddress = string.Join(", ", new[] { org.AddressLine, org.City, org.State }.Where(s => !string.IsNullOrWhiteSpace(s))),
            BankLast4 = d.Employee.BankDetail?.AccountNumberLast4, BankName = d.Employee.BankDetail?.BankName
        };
    }

    public async Task<IReadOnlyList<(int, int, int, decimal)>> MyPayslipsAsync(int employeeId, CancellationToken ct = default) =>
        (await db.PayrollDetails.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid))
            .OrderByDescending(d => d.Run.Year).ThenByDescending(d => d.Run.Month)
            .Select(d => new { d.Id, d.Run.Year, d.Run.Month, d.NetPay }).ToListAsync(ct))
        .Select(x => (x.Id, x.Year, x.Month, x.NetPay)).ToList();

    // ------------------------------------------------------------------ exports
    public async Task<ExportFile> ExportAsync(int runId, string kind, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct) ?? throw new InvalidOperationException("Payroll run not found.");
        var details = await db.PayrollDetails.AsNoTracking().Include(d => d.Lines).Include(d => d.Employee).ThenInclude(e => e.Department)
            .Include(d => d.Employee).ThenInclude(e => e.BankDetail)
            .Where(d => d.PayrollRunId == runId).OrderBy(d => d.Employee.EmployeeCode).ToListAsync(ct);
        var label = $"{run.Year}-{run.Month:00}";
        decimal L(PayrollDetail d, string code) => d.Lines.Where(l => l.Code == code).Sum(l => l.Amount);

        switch (kind)
        {
            case "register":
            {
                var earningCodes = details.SelectMany(d => d.Lines).Where(l => l.Kind == PayrollLineKind.Earning).GroupBy(l => l.Code).Select(g => (g.Key, g.First().Name, g.Min(l => l.SortOrder))).OrderBy(x => x.Item3).ToList();
                var deductionCodes = details.SelectMany(d => d.Lines).Where(l => l.Kind == PayrollLineKind.Deduction).GroupBy(l => l.Code).Select(g => (g.Key, g.First().Name, g.Min(l => l.SortOrder))).OrderBy(x => x.Item3).ToList();
                var employerCodes = details.SelectMany(d => d.Lines).Where(l => l.Kind == PayrollLineKind.EmployerContribution).GroupBy(l => l.Code).Select(g => (g.Key, g.First().Name, g.Min(l => l.SortOrder))).OrderBy(x => x.Item3).ToList();
                var headers = new List<string> { "Employee ID", "Name", "Department", "Paid days", "LOP days" };
                headers.AddRange(earningCodes.Select(c => c.Name)); headers.Add("Gross earnings");
                headers.AddRange(deductionCodes.Select(c => c.Name)); headers.Add("Total deductions"); headers.Add("Net pay");
                headers.AddRange(employerCodes.Select(c => c.Name)); headers.Add("On hold");
                var rows = details.Select(d =>
                {
                    var r = new List<object?> { d.Employee.EmployeeCode, d.Employee.FullName, d.Employee.Department?.Name, d.PaidDays, d.LopDays };
                    r.AddRange(earningCodes.Select(c => (object?)L(d, c.Key))); r.Add(d.GrossEarnings);
                    r.AddRange(deductionCodes.Select(c => (object?)L(d, c.Key))); r.Add(d.TotalDeductions); r.Add(d.NetPay);
                    r.AddRange(employerCodes.Select(c => (object?)L(d, c.Key))); r.Add(d.OnHold ? "Yes" : "");
                    return (IReadOnlyList<object?>)r;
                });
                return new ExportFile(excel.Export($"Register {label}", headers, rows), $"salary-register-{label}.xlsx");
            }
            case "bank":
            {
                EnsureFinal(run);
                var rows = details.Where(d => !d.OnHold && d.NetPay > 0).Select(d => (IReadOnlyList<object?>)new object?[]
                {
                    d.Employee.EmployeeCode, d.Employee.BankDetail?.AccountHolderName ?? d.Employee.FullName,
                    d.Employee.BankDetail is { } b ? protector.Unprotect(b.AccountNumberEncrypted) : "MISSING",
                    d.Employee.BankDetail?.Ifsc ?? "MISSING", d.Employee.BankDetail?.BankName, d.NetPay, $"Salary {label}"
                });
                return new ExportFile(excel.Export("Bank transfer", ["Employee ID", "Beneficiary name", "Account number", "IFSC", "Bank", "Amount", "Narration"], rows), $"bank-transfer-{label}.xlsx");
            }
            case "pf":
            {
                var rates = await statutory.RatesAsOfAsync(new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1), ct);
                var ceiling = rates?.PfWageCeiling ?? 0;
                var rows = details.Where(d => L(d, "PF") > 0).Select(d => (IReadOnlyList<object?>)new object?[]
                {
                    d.Employee.Uan, d.Employee.FullName, d.GrossEarnings, d.PfWages, Math.Min(d.PfWages, ceiling), Math.Min(d.PfWages, ceiling),
                    L(d, "PF"), L(d, "EPS"), L(d, "PF-ER"), Math.Round(d.LopDays, 0, MidpointRounding.AwayFromZero)
                });
                return new ExportFile(excel.Export("PF statement", ["UAN", "Member name", "Gross wages", "EPF wages", "EPS wages", "EDLI wages", "EPF contribution (EE)", "EPS contribution", "EPF difference (ER)", "NCP days"], rows), $"pf-statement-{label}.xlsx");
            }
            case "ecr":
            {
                // EPFO Electronic Challan cum Return (ECR 2.0) text upload: 11 fields separated by #~#.
                EnsureFinal(run, "The ECR file");
                var s = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
                var rates = await statutory.RatesAsOfAsync(new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1), ct);
                var ceiling = rates?.PfWageCeiling ?? 0;
                var members = details.Where(d => !d.OnHold && L(d, "PF") > 0).ToList();
                var noUan = members.Where(d => string.IsNullOrWhiteSpace(d.Employee.Uan)).Select(d => $"{d.Employee.FullName} ({d.Employee.EmployeeCode})").ToList();
                if (noUan.Count > 0) throw new InvalidOperationException($"Add the UAN for: {string.Join(", ", noUan)} — EPFO rejects ECR lines without a UAN.");
                if (members.Count == 0) throw new InvalidOperationException("Nobody in this payroll has a PF contribution.");
                static string N(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var sb = new System.Text.StringBuilder();
                foreach (var d in members)
                {
                    var epfWages = s.PfRestrictToCeiling ? Math.Min(d.PfWages, ceiling) : d.PfWages;
                    var eps = L(d, "EPS");
                    var name = new string(d.Employee.FullName.ToUpperInvariant().Where(c => char.IsLetter(c) || c == ' ' || c == '.').ToArray());
                    sb.Append(string.Join("#~#", d.Employee.Uan!.Trim(), name, N(d.GrossEarnings), N(epfWages), N(eps > 0 ? Math.Min(d.PfWages, ceiling) : 0),
                        N(Math.Min(d.PfWages, ceiling)), N(L(d, "PF")), N(eps), N(L(d, "PF-ER")), N(d.LopDays), "0")).Append('\n');
                }
                return new ExportFile(System.Text.Encoding.ASCII.GetBytes(sb.ToString()), $"ECR-{label}.txt");
            }
            case "esi":
            {
                var rows = details.Where(d => L(d, "ESI") > 0).Select(d => (IReadOnlyList<object?>)new object?[]
                    { d.Employee.EmployeeCode, d.Employee.FullName, d.PaidDays, d.EsiWages, L(d, "ESI"), L(d, "ESI-ER") });
                return new ExportFile(excel.Export("ESI statement", ["Employee ID", "Name", "Paid days", "ESI wages", "Employee contribution", "Employer contribution"], rows), $"esi-statement-{label}.xlsx");
            }
            case "journal":
            {
                var s = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
                decimal Sum(params string[] codes) => details.Sum(d => d.Lines.Where(l => codes.Contains(l.Code)).Sum(l => l.Amount));
                var gross = details.Sum(d => d.GrossEarnings);
                var employer = details.Sum(d => d.EmployerContributions);
                var pf = Sum("PF", "PF-ER", "EPS", "EDLI", "PF-ADM");
                var esi = Sum("ESI", "ESI-ER");
                var pt = Sum("PT");
                var lwf = Sum("LWF", "LWF-ER");
                var tds = Sum("TDS");
                var loansTotal = details.Sum(d => d.Lines.Where(l => l.Code.StartsWith("LOAN")).Sum(l => l.Amount));
                var net = details.Sum(d => d.NetPay);
                var other = gross + employer - net - pf - esi - pt - lwf - tds - loansTotal;
                var date = new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(-1).ToDateTime(TimeOnly.MinValue);
                var entries = new List<(string Account, decimal Debit, decimal Credit)>
                {
                    (s.SalaryExpenseAccount, gross, 0), (s.EmployerContributionAccount, employer, 0),
                    (s.SalaryPayableAccount, 0, net), (s.PfPayableAccount, 0, pf), (s.EsiPayableAccount, 0, esi), (s.PtPayableAccount, 0, pt),
                    (s.LwfPayableAccount, 0, lwf), (s.TdsPayableAccount, 0, tds), (s.LoanRecoveryAccount, 0, loansTotal),
                    (s.SalaryPayableAccount + " (other deductions)", 0, other)
                };
                var rows = entries.Where(e => e.Debit != 0 || e.Credit != 0)
                    .Select(e => (IReadOnlyList<object?>)new object?[] { date, e.Account, e.Debit == 0 ? null : e.Debit, e.Credit == 0 ? null : e.Credit, $"Payroll {label}" });
                return new ExportFile(excel.Export("Journal", ["Date", "Ledger", "Debit", "Credit", "Narration"], rows), $"payroll-journal-{label}.xlsx");
            }
            default: throw new ArgumentException("Unknown export.", nameof(kind));
        }
    }

    private static void EnsureFinal(PayrollRun run, string what = "The bank transfer file")
    {
        if (run.Status is not (PayrollRunStatus.Finalized or PayrollRunStatus.Paid))
            throw new InvalidOperationException($"{what} is available after the payroll is finalized.");
    }
}
