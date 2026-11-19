using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;

namespace Codenexify.HRMS.Application.Payroll;

public sealed record ComponentDef(string Code, string Name, ComponentType Type, ComponentCalculation Calculation, decimal Value,
    bool IsTaxable, bool IsPfWage, bool IsEsiWage, bool IsProrated, bool IsBasic, bool IsHra, int SortOrder);

public sealed record ComponentAmount(ComponentDef Component, decimal MonthlyAmount);

public sealed class TaxInputs
{
    public required IncomeTaxRule Rule { get; init; }
    public TaxDeclaration? Declaration { get; init; }
    public decimal YtdTaxableEarnings { get; init; }
    public decimal YtdTds { get; init; }
    public decimal YtdProfessionalTax { get; init; }
    public decimal YtdEmployeePf { get; init; }
    /// <summary>Payroll months left in the financial year after the current one.</summary>
    public int RemainingMonthsAfterThis { get; init; }
}

public sealed class PayInput
{
    public required int Month { get; init; }
    public required int DaysInMonth { get; init; }
    public required decimal PaidDays { get; init; }
    public required decimal AnnualGross { get; init; }
    public required IReadOnlyList<ComponentDef> Components { get; init; }
    public required StatutoryRateSet Rates { get; init; }
    public bool PfApplicable { get; init; } = true;
    public bool PfEnabled { get; init; } = true;
    public bool PfRestrictToCeiling { get; init; } = true;
    public bool EsiEnabled { get; init; } = true;
    public bool PtEnabled { get; init; } = true;
    public bool LwfEnabled { get; init; } = true;
    public IReadOnlyList<ProfessionalTaxSlab> PtSlabs { get; init; } = [];
    public LabourWelfareFundRule? Lwf { get; init; }
    public Gender? Gender { get; init; }
    public decimal OvertimePay { get; init; }
    public decimal LeaveEncashmentPay { get; init; }
    public IReadOnlyList<(string Code, string Name, decimal Amount)> LoanDeductions { get; init; } = [];
    /// <summary>One-time items (bonus, arrears, reimbursements, recoveries). Never PF/ESI wages; earnings count for TDS when taxable.</summary>
    public IReadOnlyList<OneTimeItem> OneTimeItems { get; init; } = [];
    /// <summary>Null when TDS is disabled or no tax rule exists for the year.</summary>
    public TaxInputs? Tax { get; init; }
}

public sealed record OneTimeItem(string Code, string Name, bool IsDeduction, decimal Amount, bool IsTaxable);

public sealed record PayLine(string Code, string Name, PayrollLineKind Kind, decimal Amount, decimal? FullAmount, bool IsTaxable, int SortOrder);

public sealed class PayResult
{
    public required IReadOnlyList<PayLine> Lines { get; init; }
    public decimal MonthlyGross { get; init; }
    public decimal GrossEarnings { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetPay { get; init; }
    public decimal EmployerContributions { get; init; }
    public decimal PfWages { get; init; }
    public decimal EsiWages { get; init; }
    public decimal TaxableEarnings { get; init; }
    public decimal ProjectedAnnualTax { get; init; }
    public TaxRegime Regime { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Pure monthly payroll arithmetic for one employee. All statutory parameters come from data;
/// nothing here is specific to a year. Amounts are rounded to whole rupees.
/// </summary>
public static class PayrollEngine
{
    public static decimal R(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    /// <summary>Full-month component amounts for a gross salary (no proration).</summary>
    public static (IReadOnlyList<ComponentAmount> Amounts, string? Error) Breakup(decimal monthlyGross, IReadOnlyList<ComponentDef> components)
    {
        var result = new Dictionary<string, decimal>();
        var earnings = components.Where(c => c.Type == ComponentType.Earning).ToList();
        var basic = earnings.FirstOrDefault(c => c.IsBasic);
        if (earnings.Count(c => c.Calculation == ComponentCalculation.Balancing) > 1) return ([], "Only one balancing component is allowed.");

        foreach (var c in components.Where(c => c.Calculation is ComponentCalculation.FixedAmount or ComponentCalculation.PercentOfGross))
            result[c.Code] = c.Calculation == ComponentCalculation.FixedAmount ? c.Value : R(monthlyGross * c.Value / 100m);
        var basicAmount = basic is not null && result.TryGetValue(basic.Code, out var b) ? b : 0;
        foreach (var c in components.Where(c => c.Calculation == ComponentCalculation.PercentOfBasic))
        {
            if (basic is null) return ([], $"{c.Name} is a percentage of basic, but the structure has no basic component.");
            result[c.Code] = R(basicAmount * c.Value / 100m);
        }
        var fixedEarnings = earnings.Where(c => c.Calculation != ComponentCalculation.Balancing).Sum(c => result[c.Code]);
        if (fixedEarnings > monthlyGross) return ([], $"Earnings ({fixedEarnings:N0}) exceed the monthly gross ({monthlyGross:N0}). Increase the salary or adjust the structure.");
        var balancing = earnings.FirstOrDefault(c => c.Calculation == ComponentCalculation.Balancing);
        if (balancing is not null) result[balancing.Code] = monthlyGross - fixedEarnings;
        else if (monthlyGross - fixedEarnings > 0.5m) return ([], "The structure does not add up to the gross. Add a balancing component (e.g. Special Allowance).");

        return (components.OrderBy(c => c.SortOrder).Select(c => new ComponentAmount(c, result[c.Code])).ToList(), null);
    }

    public static PayResult Calculate(PayInput input)
    {
        var warnings = new List<string>();
        var lines = new List<PayLine>();
        var monthlyGross = R(input.AnnualGross / 12m);
        var (amounts, error) = Breakup(monthlyGross, input.Components);
        if (error is not null) return Empty(monthlyGross, [error]);

        var ratio = input.DaysInMonth == 0 ? 0 : Math.Clamp(input.PaidDays / input.DaysInMonth, 0, 1);
        decimal Earned(ComponentAmount a) => a.Component.IsProrated ? R(a.MonthlyAmount * ratio) : a.MonthlyAmount;

        var order = 0;
        foreach (var a in amounts.Where(a => a.Component.Type == ComponentType.Earning))
            lines.Add(new PayLine(a.Component.Code, a.Component.Name, PayrollLineKind.Earning, Earned(a), a.MonthlyAmount, a.Component.IsTaxable, order += 10));
        if (input.OvertimePay > 0) lines.Add(new PayLine("OT", "Overtime", PayrollLineKind.Earning, R(input.OvertimePay), null, true, order += 10));
        if (input.LeaveEncashmentPay > 0) lines.Add(new PayLine("LEAVEENC", "Leave encashment", PayrollLineKind.Earning, R(input.LeaveEncashmentPay), null, true, order += 10));
        foreach (var item in input.OneTimeItems.Where(i => !i.IsDeduction && i.Amount > 0))
            lines.Add(new PayLine(item.Code, item.Name, PayrollLineKind.Earning, R(item.Amount), null, item.IsTaxable, order += 10));

        var earnedComponents = amounts.Where(a => a.Component.Type == ComponentType.Earning).Select(a => (a, Earned(a))).ToList();
        var grossEarnings = lines.Where(l => l.Kind == PayrollLineKind.Earning).Sum(l => l.Amount);
        var rates = input.Rates;

        // ---- Provident Fund
        var pfWages = earnedComponents.Where(x => x.a.Component.IsPfWage).Sum(x => x.Item2);
        decimal eePf = 0;
        if (input.PfEnabled && input.PfApplicable && pfWages > 0)
        {
            var capped = Math.Min(pfWages, rates.PfWageCeiling);
            var pfBase = input.PfRestrictToCeiling ? capped : pfWages;
            eePf = R(pfBase * rates.PfEmployeeRate / 100m);
            var erTotal = R(pfBase * rates.PfEmployerRate / 100m);
            var eps = Math.Min(R(capped * rates.EpsRate / 100m), erTotal);
            lines.Add(new PayLine("PF", "Provident Fund", PayrollLineKind.Deduction, eePf, null, false, 100));
            lines.Add(new PayLine("PF-ER", "Employer PF (EPF)", PayrollLineKind.EmployerContribution, erTotal - eps, null, false, 200));
            lines.Add(new PayLine("EPS", "Employer pension (EPS)", PayrollLineKind.EmployerContribution, eps, null, false, 210));
            lines.Add(new PayLine("EDLI", "EDLI", PayrollLineKind.EmployerContribution, R(capped * rates.EdliRate / 100m), null, false, 220));
            lines.Add(new PayLine("PF-ADM", "PF admin charges", PayrollLineKind.EmployerContribution, R(pfBase * rates.PfAdminChargesRate / 100m), null, false, 230));
        }
        else if (input.PfEnabled && !input.PfApplicable) pfWages = 0;

        // ---- ESI: eligibility on the full monthly gross; contribution on wages actually paid (incl. overtime), rounded up.
        decimal esiWages = 0;
        if (input.EsiEnabled && monthlyGross <= rates.EsiWageLimit)
        {
            esiWages = earnedComponents.Where(x => x.a.Component.IsEsiWage).Sum(x => x.Item2) + R(input.OvertimePay);
            if (esiWages > 0)
            {
                lines.Add(new PayLine("ESI", "ESI", PayrollLineKind.Deduction, Math.Ceiling(esiWages * rates.EsiEmployeeRate / 100m), null, false, 110));
                lines.Add(new PayLine("ESI-ER", "Employer ESI", PayrollLineKind.EmployerContribution, Math.Ceiling(esiWages * rates.EsiEmployerRate / 100m), null, false, 240));
            }
        }

        // ---- Professional tax
        decimal pt = 0;
        if (input.PtEnabled && input.PtSlabs.Count > 0 && grossEarnings > 0)
        {
            var genderSlabs = input.PtSlabs.Where(s => s.Gender == input.Gender).ToList();
            if (genderSlabs.Count == 0) genderSlabs = input.PtSlabs.Where(s => s.Gender is null).ToList();
            if (genderSlabs.Count == 0) genderSlabs = input.PtSlabs.Where(s => s.Gender == Gender.Male).ToList();
            var slab = genderSlabs.FirstOrDefault(s => grossEarnings >= s.MinMonthlyGross && (s.MaxMonthlyGross is null || grossEarnings <= s.MaxMonthlyGross));
            if (slab is not null)
            {
                pt = slab.SpecialMonth == input.Month && slab.SpecialMonthAmount is { } special ? special : slab.MonthlyAmount;
                if (pt > 0) lines.Add(new PayLine("PT", "Professional tax", PayrollLineKind.Deduction, pt, null, false, 120));
            }
        }

        // ---- Labour welfare fund
        if (input.LwfEnabled && input.Lwf is { } lwf && lwf.Months.Split(',').Select(int.Parse).Contains(input.Month) && grossEarnings > 0)
        {
            lines.Add(new PayLine("LWF", "Labour welfare fund", PayrollLineKind.Deduction, lwf.EmployeeAmount, null, false, 130));
            lines.Add(new PayLine("LWF-ER", "Employer LWF", PayrollLineKind.EmployerContribution, lwf.EmployerAmount, null, false, 250));
        }

        // ---- Income tax (TDS) by annual projection
        var taxableThisMonth = lines.Where(l => l.Kind == PayrollLineKind.Earning && l.IsTaxable).Sum(l => l.Amount);
        decimal projectedTax = 0;
        var regime = input.Tax?.Declaration?.Regime ?? TaxRegime.New;
        if (input.Tax is { } tax)
        {
            if (tax.Rule.Regime != regime) warnings.Add($"Tax rule for the {regime} regime was not found; {tax.Rule.Regime} regime used.");
            regime = tax.Rule.Regime;
            var fullTaxable = amounts.Where(a => a.Component.Type == ComponentType.Earning && a.Component.IsTaxable).Sum(a => a.MonthlyAmount);
            var fullPf = input.PfEnabled && input.PfApplicable
                ? R(Math.Min(amounts.Where(a => a.Component.IsPfWage).Sum(a => a.MonthlyAmount), input.PfRestrictToCeiling ? rates.PfWageCeiling : decimal.MaxValue) * rates.PfEmployeeRate / 100m)
                : 0;
            var months = tax.RemainingMonthsAfterThis;
            var decl = tax.Declaration;
            var annualSalary = tax.YtdTaxableEarnings + taxableThisMonth + fullTaxable * months + (decl?.PreviousEmployerIncome ?? 0);

            decimal deductions = tax.Rule.StandardDeduction;
            if (regime == TaxRegime.Old)
            {
                var basicFull = amounts.Where(a => a.Component.IsBasic).Sum(a => a.MonthlyAmount) * 12;
                var hraFull = amounts.Where(a => a.Component.IsHra).Sum(a => a.MonthlyAmount) * 12;
                if (decl is { MonthlyRentPaid: > 0 } && hraFull > 0)
                {
                    var rent = decl.MonthlyRentPaid * 12;
                    var exemption = Math.Min(hraFull, Math.Min(rent - basicFull * 0.10m, basicFull * (decl.MetroCity ? 0.50m : 0.40m)));
                    deductions += Math.Max(0, exemption);
                }
                var annualPf = tax.YtdEmployeePf + eePf + fullPf * months;
                deductions += Math.Min((decl?.Section80C ?? 0) + annualPf, tax.Rule.Section80CLimit);
                deductions += Math.Min(decl?.Section80D ?? 0, tax.Rule.Section80DLimit);
                deductions += Math.Min(decl?.Section80CCD1B ?? 0, tax.Rule.Section80CCD1BLimit);
                deductions += Math.Min(decl?.HomeLoanInterest ?? 0, tax.Rule.HomeLoanInterestLimit);
                deductions += decl?.OtherDeductions ?? 0;
                deductions += tax.YtdProfessionalTax + pt + (pt > 0 ? pt * months : 0); // professional tax u/s 16(iii)
            }
            var computed = TaxCalculator.Compute(Math.Max(0, annualSalary - deductions), tax.Rule);
            projectedTax = computed.TotalTax;
            var remaining = projectedTax - tax.YtdTds - (decl?.PreviousEmployerTds ?? 0);
            var tds = Math.Max(0, R(remaining / (months + 1)));
            if (tds > 0) lines.Add(new PayLine("TDS", "Income tax (TDS)", PayrollLineKind.Deduction, tds, null, false, 140));
        }

        foreach (var a in amounts.Where(a => a.Component.Type == ComponentType.Deduction))
        {
            var amount = a.Component.IsProrated ? R(a.MonthlyAmount * ratio) : a.MonthlyAmount;
            if (amount > 0) lines.Add(new PayLine(a.Component.Code, a.Component.Name, PayrollLineKind.Deduction, amount, a.MonthlyAmount, false, 150 + a.Component.SortOrder));
        }
        foreach (var loan in input.LoanDeductions.Where(l => l.Amount > 0))
            lines.Add(new PayLine(loan.Code, loan.Name, PayrollLineKind.Deduction, R(loan.Amount), null, false, 180));
        foreach (var item in input.OneTimeItems.Where(i => i.IsDeduction && i.Amount > 0))
            lines.Add(new PayLine(item.Code, item.Name, PayrollLineKind.Deduction, R(item.Amount), null, false, 190));

        var totalDeductions = lines.Where(l => l.Kind == PayrollLineKind.Deduction).Sum(l => l.Amount);
        var net = grossEarnings - totalDeductions;
        if (net < 0) warnings.Add("Deductions exceed earnings; the payslip is put on hold.");
        if (!rates.IsVerified) warnings.Add("Statutory rates are not marked as verified by the platform owner.");

        return new PayResult
        {
            Lines = lines.OrderBy(l => l.SortOrder).ToList(),
            MonthlyGross = monthlyGross,
            GrossEarnings = grossEarnings,
            TotalDeductions = totalDeductions,
            NetPay = net,
            EmployerContributions = lines.Where(l => l.Kind == PayrollLineKind.EmployerContribution).Sum(l => l.Amount),
            PfWages = pfWages,
            EsiWages = esiWages,
            TaxableEarnings = taxableThisMonth,
            ProjectedAnnualTax = projectedTax,
            Regime = regime,
            Warnings = warnings
        };
    }

    private static PayResult Empty(decimal gross, List<string> warnings) => new() { Lines = [], MonthlyGross = gross, Warnings = warnings };

    /// <summary>Financial year (starting year) for a payroll month, e.g. Mar 2027 → 2026.</summary>
    public static int FinancialYearOf(int year, int month) => month >= 4 ? year : year - 1;

    /// <summary>Payroll months left in the financial year after this one (Apr → 11, Mar → 0).</summary>
    public static int RemainingMonthsAfter(int month) => month >= 4 ? 12 - month + 3 : 3 - month;

    public static (decimal ServiceYears, int CountedYears) ServiceFor(DateOnly joined, DateOnly asOf)
    {
        if (asOf < joined) return (0, 0);
        var totalMonths = (asOf.Year - joined.Year) * 12 + asOf.Month - joined.Month - (asOf.Day < joined.Day ? 1 : 0);
        var years = totalMonths / 12;
        var extraMonths = totalMonths % 12;
        // Payment of Gratuity Act: a part year of more than six months counts as a full year.
        return (Math.Round(totalMonths / 12m, 2), extraMonths > 6 ? years + 1 : years);
    }

    public static decimal Gratuity(decimal monthlyBasic, int countedYears, StatutoryRateSet rates) =>
        Math.Min(R(monthlyBasic * rates.GratuityDaysPerYear / rates.GratuityWageDivisor * countedYears), rates.GratuityCeiling);
}
