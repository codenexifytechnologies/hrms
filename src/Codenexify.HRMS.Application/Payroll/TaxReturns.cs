using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

public interface ITaxReturnService
{
    /// <summary>Financial years (start year) with finalized payroll for the employee, or for anyone when null.</summary>
    Task<IReadOnlyList<int>> YearsAsync(int? employeeId, CancellationToken ct = default);
    /// <summary>Everyone (current or former) with finalized payroll.</summary>
    Task<IReadOnlyList<LookupItem>> EmployeesAsync(CancellationToken ct = default);
    /// <param name="revealPan">Show the full PAN (the employee themself, or HR with sensitive-data access).</param>
    Task<Form16Pdf?> Form16Async(int employeeId, int financialYear, bool revealPan, CancellationToken ct = default);
    /// <summary>Deductee details for the quarterly TDS return on salary (Form 24Q, annexure I), as Excel.</summary>
    Task<ExportFile> Form24QAnnexureAsync(int financialYear, int quarter, CancellationToken ct = default);
}

public sealed class TaxReturnService(IAppDbContext db, ICurrentContext context, IClock clock, IStatutoryRuleService statutory,
    ISecretProtector protector, IExcelExporter excel) : ITaxReturnService
{
    private static bool Final(PayrollRunStatus s) => s is PayrollRunStatus.Finalized or PayrollRunStatus.Paid;

    public async Task<IReadOnlyList<int>> YearsAsync(int? employeeId, CancellationToken ct = default)
    {
        var months = await db.PayrollDetails.AsNoTracking()
            .Where(d => (employeeId == null || d.EmployeeId == employeeId) && !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid))
            .Select(d => new { d.Run.Year, d.Run.Month }).Distinct().ToListAsync(ct);
        return months.Select(m => PayrollEngine.FinancialYearOf(m.Year, m.Month)).Distinct().OrderByDescending(y => y).ToList();
    }

    public async Task<IReadOnlyList<LookupItem>> EmployeesAsync(CancellationToken ct = default) =>
        await db.Employees.AsNoTracking()
            .Where(e => db.PayrollDetails.Any(d => d.EmployeeId == e.Id && !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid)))
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName + " (" + e.EmployeeCode + ")" + (e.Status == EmployeeStatus.Exited ? " · former" : "")))
            .ToListAsync(ct);

    private string? Pan(Employee e, bool reveal) =>
        reveal && e.PanEncrypted is { } enc ? protector.Unprotect(enc) : e.PanMasked;

    public async Task<Form16Pdf?> Form16Async(int employeeId, int financialYear, bool revealPan, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.Designation).FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return null;
        var from = financialYear * 100 + 4;
        var to = (financialYear + 1) * 100 + 3;
        var details = await db.PayrollDetails.AsNoTracking().Include(d => d.Lines).Include(d => d.Run)
            .Where(d => d.EmployeeId == employeeId && !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid)
                        && d.Run.Year * 100 + d.Run.Month >= from && d.Run.Year * 100 + d.Run.Month <= to)
            .OrderBy(d => d.Run.Year).ThenBy(d => d.Run.Month).ToListAsync(ct);
        if (details.Count == 0) return null;

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == context.OrgId(), ct);
        var settings = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PayrollSetting();
        var regime = details[^1].Regime;
        var rule = await statutory.TaxRuleAsync(financialYear, regime, ct);
        var decl = await db.TaxDeclarations.AsNoTracking().FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.FinancialYear == financialYear && t.Status != DeclarationStatus.Draft, ct);
        var components = await db.SalaryComponents.AsNoTracking().Select(c => new { c.Code, c.IsBasic, c.IsHra }).ToListAsync(ct);
        var basicCodes = components.Where(c => c.IsBasic).Select(c => c.Code).ToHashSet();
        var hraCodes = components.Where(c => c.IsHra).Select(c => c.Code).ToHashSet();
        decimal Sum(Func<PayrollLine, bool> p) => details.Sum(d => d.Lines.Where(p).Sum(l => l.Amount));

        var gross = details.Sum(d => d.GrossEarnings);
        var taxable = details.Sum(d => d.TaxableEarnings);
        var exempt = gross - taxable;
        var previousIncome = decl?.PreviousEmployerIncome ?? 0;
        var standard = rule?.StandardDeduction ?? 0;
        var pt = Sum(l => l.Code == "PT");
        var eePf = Sum(l => l.Code == "PF");
        var tds = Sum(l => l.Code == "TDS");
        var lines = new List<(string, decimal, bool)>
        {
            ("1. Gross salary paid by this employer", gross, false),
            ("   Less: exempt / non-taxable components (e.g. reimbursements)", exempt, false),
        };
        if (previousIncome > 0) lines.Add(("   Add: salary from previous employer(s) (as declared)", previousIncome, false));
        var salaryIncome = taxable + previousIncome;
        decimal hraExemption = 0, chapterVia = 0, homeLoan = 0;
        lines.Add(("2. Taxable salary", salaryIncome, true));
        lines.Add(("   Less: standard deduction u/s 16(ia)", standard, false));
        if (regime == TaxRegime.Old)
        {
            if (decl is { MonthlyRentPaid: > 0 })
            {
                var basic = Sum(l => basicCodes.Contains(l.Code));
                var hra = Sum(l => hraCodes.Contains(l.Code));
                var rent = decl.MonthlyRentPaid * details.Count;
                hraExemption = Math.Max(0, Math.Min(hra, Math.Min(rent - basic * 0.10m, basic * (decl.MetroCity ? 0.50m : 0.40m))));
                lines.Add(("   Less: HRA exemption u/s 10(13A)", hraExemption, false));
            }
            lines.Add(("   Less: professional tax u/s 16(iii)", pt, false));
            if (rule is not null)
            {
                var c80 = Math.Min((decl?.Section80C ?? 0) + eePf, rule.Section80CLimit);
                var d80 = Math.Min(decl?.Section80D ?? 0, rule.Section80DLimit);
                var nps = Math.Min(decl?.Section80CCD1B ?? 0, rule.Section80CCD1BLimit);
                homeLoan = Math.Min(decl?.HomeLoanInterest ?? 0, rule.HomeLoanInterestLimit);
                chapterVia = c80 + d80 + nps + (decl?.OtherDeductions ?? 0);
                lines.Add(("   Less: interest on housing loan u/s 24(b)", homeLoan, false));
                lines.Add(($"   Less: 80C (incl. employee PF ₹{eePf:N0})", c80, false));
                if (d80 > 0) lines.Add(("   Less: 80D", d80, false));
                if (nps > 0) lines.Add(("   Less: 80CCD(1B)", nps, false));
                if (decl?.OtherDeductions > 0) lines.Add(("   Less: other deductions", decl.OtherDeductions, false));
            }
        }
        var totalIncome = Math.Max(0, salaryIncome - standard - hraExemption - (regime == TaxRegime.Old ? pt + homeLoan + chapterVia : 0));
        lines.Add(("3. Total taxable income", PayrollEngine.R(totalIncome), true));
        if (rule is not null)
        {
            var tax = TaxCalculator.Compute(PayrollEngine.R(totalIncome), rule);
            lines.Add(("   Tax on total income", tax.SlabTax, false));
            if (tax.Rebate > 0) lines.Add(("   Less: rebate u/s 87A", tax.Rebate, false));
            if (tax.Surcharge > 0) lines.Add(("   Add: surcharge", tax.Surcharge, false));
            lines.Add(("   Add: health & education cess", tax.Cess, false));
            lines.Add(("4. Tax payable", tax.TotalTax, true));
        }
        else lines.Add(("4. Tax payable — income-tax rules for this year are not configured", 0, true));
        if (decl?.PreviousEmployerTds > 0) lines.Add(("   TDS by previous employer(s) (as declared)", decl.PreviousEmployerTds, false));
        lines.Add(("5. Tax deducted at source by this employer", tds, true));

        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var fyEnd = new DateOnly(financialYear + 1, 3, 31);
        var start = new[] { e.DateOfJoining, new DateOnly(financialYear, 4, 1) }.Max();
        var end = new[] { e.DateOfExit ?? fyEnd, fyEnd }.Min();
        var expectedMonths = Enumerable.Range(0, 12).Select(i => new DateOnly(financialYear, 4, 1).AddMonths(i))
            .Count(m => m <= end && m.AddMonths(1) > start && m <= today);
        var provisional = today <= fyEnd && (e.DateOfExit is null || e.DateOfExit > today) || details.Count < expectedMonths;

        return new Form16Pdf(LetterService.Head(org), org.Pan, settings.Tan, e.FullName, e.EmployeeCode, Pan(e, revealPan), e.Designation?.Name,
            $"{financialYear + 1}-{(financialYear + 2) % 100:00}", $"{financialYear}-{(financialYear + 1) % 100:00}",
            $"{start:dd MMM yyyy} to {end:dd MMM yyyy}", regime == TaxRegime.New ? "New regime (115BAC)" : "Old regime", provisional, lines,
            details.Select(d => (new DateOnly(d.Run.Year, d.Run.Month, 1).ToString("MMM yyyy"), d.GrossEarnings, d.Lines.Where(l => l.Code == "TDS").Sum(l => l.Amount))).ToList(),
            tds);
    }

    public async Task<ExportFile> Form24QAnnexureAsync(int financialYear, int quarter, CancellationToken ct = default)
    {
        if (quarter is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(quarter));
        // Q1 = Apr–Jun, Q2 = Jul–Sep, Q3 = Oct–Dec, Q4 = Jan–Mar.
        var first = new DateOnly(financialYear, 4, 1).AddMonths((quarter - 1) * 3);
        var keys = Enumerable.Range(0, 3).Select(i => first.AddMonths(i)).Select(m => m.Year * 100 + m.Month).ToList();
        var rows = await db.PayrollDetails.AsNoTracking().Include(d => d.Run).Include(d => d.Employee).Include(d => d.Lines)
            .Where(d => !d.OnHold && (d.Run.Status == PayrollRunStatus.Finalized || d.Run.Status == PayrollRunStatus.Paid) && keys.Contains(d.Run.Year * 100 + d.Run.Month))
            .OrderBy(d => d.Employee.EmployeeCode).ThenBy(d => d.Run.Year).ThenBy(d => d.Run.Month).ToListAsync(ct);
        var data = rows.Select(d =>
        {
            var monthEnd = new DateOnly(d.Run.Year, d.Run.Month, 1).AddMonths(1).AddDays(-1);
            var pan = d.Employee.PanEncrypted is { } enc ? protector.Unprotect(enc) : "PANNOTAVBL";
            return (IReadOnlyList<object?>)new object?[]
            {
                d.Employee.EmployeeCode, pan, d.Employee.FullName, "192", (d.Run.PaidOn ?? monthEnd).ToDateTime(TimeOnly.MinValue),
                d.GrossEarnings, d.TaxableEarnings, d.Lines.Where(l => l.Code == "TDS").Sum(l => l.Amount), monthEnd.ToDateTime(TimeOnly.MinValue),
                d.Regime == TaxRegime.Old ? "Y" : "N"
            };
        });
        var label = $"FY{financialYear % 100:00}-{(financialYear + 1) % 100:00}-Q{quarter}";
        return new ExportFile(excel.Export($"24Q {label}",
            ["Employee ID", "PAN", "Name", "Section", "Date of payment/credit", "Amount paid/credited", "Taxable amount", "TDS deducted", "Date of deduction", "Opted out of 115BAC (Y/N)"],
            data), $"form24q-annexure-{label}.xlsx");
    }
}
