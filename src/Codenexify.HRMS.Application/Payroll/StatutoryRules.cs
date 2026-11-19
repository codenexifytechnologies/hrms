using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Payroll;

/// <summary>
/// Starting statutory values seeded once into the platform tables. They are DATA, editable by the platform owner,
/// flagged unverified until someone confirms them against the current notifications and Finance Act.
/// </summary>
public static class StatutoryDefaults
{
    public static void AddTo(IAppDbContext db)
    {
        db.StatutoryRateSets.Add(new StatutoryRateSet
        {
            EffectiveFrom = new DateOnly(2017, 1, 1),
            PfEmployeeRate = 12, PfEmployerRate = 12, EpsRate = 8.33m, PfWageCeiling = 15000, EdliRate = 0.5m, PfAdminChargesRate = 0.5m,
            EsiEmployeeRate = 0.75m, EsiEmployerRate = 3.25m, EsiWageLimit = 21000,
            GratuityMinServiceYears = 5, GratuityDaysPerYear = 15, GratuityWageDivisor = 26, GratuityCeiling = 2000000,
            Notes = "Seeded starting values. Verify against current EPFO/ESIC notifications and the Payment of Gratuity Act before use."
        });

        var from = new DateOnly(2017, 1, 1);
        void Pt(string state, decimal min, decimal? max, decimal amount, Gender? g = null, int? sm = null, decimal? smAmount = null) =>
            db.ProfessionalTaxSlabs.Add(new ProfessionalTaxSlab { State = state, EffectiveFrom = from, MinMonthlyGross = min, MaxMonthlyGross = max, MonthlyAmount = amount, Gender = g, SpecialMonth = sm, SpecialMonthAmount = smAmount });
        Pt("Maharashtra", 0, 7500, 0, Gender.Male);
        Pt("Maharashtra", 7500.01m, 10000, 175, Gender.Male);
        Pt("Maharashtra", 10000.01m, null, 200, Gender.Male, 2, 300);
        Pt("Maharashtra", 0, 25000, 0, Gender.Female);
        Pt("Maharashtra", 25000.01m, null, 200, Gender.Female, 2, 300);
        Pt("Maharashtra", 0, 7500, 0, Gender.Other);
        Pt("Maharashtra", 7500.01m, 10000, 175, Gender.Other);
        Pt("Maharashtra", 10000.01m, null, 200, Gender.Other, 2, 300);
        Pt("Karnataka", 0, 24999.99m, 0);
        Pt("Karnataka", 25000, null, 200);
        Pt("Gujarat", 0, 11999.99m, 0);
        Pt("Gujarat", 12000, null, 200);
        Pt("Telangana", 0, 15000, 0);
        Pt("Telangana", 15000.01m, 20000, 150);
        Pt("Telangana", 20000.01m, null, 200);

        db.LabourWelfareFundRules.Add(new LabourWelfareFundRule { State = "Maharashtra", EffectiveFrom = from, EmployeeAmount = 25, EmployerAmount = 75, Months = "6,12" });
        db.LabourWelfareFundRules.Add(new LabourWelfareFundRule { State = "Karnataka", EffectiveFrom = from, EmployeeAmount = 50, EmployerAmount = 100, Months = "12" });

        foreach (var fy in new[] { 2025, 2026 })
        {
            db.IncomeTaxRules.Add(new IncomeTaxRule
            {
                FinancialYear = fy, Regime = TaxRegime.New, StandardDeduction = 75000, RebateIncomeLimit = 1200000, RebateMaxAmount = 60000, CessRate = 4,
                Slabs =
                [
                    new() { FromIncome = 0, ToIncome = 400000, Rate = 0 }, new() { FromIncome = 400000, ToIncome = 800000, Rate = 5 },
                    new() { FromIncome = 800000, ToIncome = 1200000, Rate = 10 }, new() { FromIncome = 1200000, ToIncome = 1600000, Rate = 15 },
                    new() { FromIncome = 1600000, ToIncome = 2000000, Rate = 20 }, new() { FromIncome = 2000000, ToIncome = 2400000, Rate = 25 },
                    new() { FromIncome = 2400000, ToIncome = null, Rate = 30 }
                ],
                Surcharges = [new() { AboveIncome = 5000000, Rate = 10 }, new() { AboveIncome = 10000000, Rate = 15 }, new() { AboveIncome = 20000000, Rate = 25 }]
            });
            db.IncomeTaxRules.Add(new IncomeTaxRule
            {
                FinancialYear = fy, Regime = TaxRegime.Old, StandardDeduction = 50000, RebateIncomeLimit = 500000, RebateMaxAmount = 12500, CessRate = 4,
                Section80CLimit = 150000, Section80DLimit = 25000, Section80CCD1BLimit = 50000, HomeLoanInterestLimit = 200000,
                Slabs =
                [
                    new() { FromIncome = 0, ToIncome = 250000, Rate = 0 }, new() { FromIncome = 250000, ToIncome = 500000, Rate = 5 },
                    new() { FromIncome = 500000, ToIncome = 1000000, Rate = 20 }, new() { FromIncome = 1000000, ToIncome = null, Rate = 30 }
                ],
                Surcharges = [new() { AboveIncome = 5000000, Rate = 10 }, new() { AboveIncome = 10000000, Rate = 15 }, new() { AboveIncome = 20000000, Rate = 25 }, new() { AboveIncome = 50000000, Rate = 37 }]
            });
        }
    }
}

public sealed record TaxComputation(decimal TaxableIncome, decimal SlabTax, decimal Rebate, decimal Surcharge, decimal Cess, decimal TotalTax);

/// <summary>Pure income-tax arithmetic for one financial year and regime (unit tested).</summary>
public static class TaxCalculator
{
    public static TaxComputation Compute(decimal taxableIncome, IncomeTaxRule rule)
    {
        var income = Math.Max(0, Math.Round(taxableIncome / 10, MidpointRounding.AwayFromZero) * 10); // s.288A rounding
        decimal tax = 0;
        foreach (var s in rule.Slabs.OrderBy(s => s.FromIncome))
        {
            if (income <= s.FromIncome) break;
            var upper = s.ToIncome is { } to ? Math.Min(income, to) : income;
            tax += (upper - s.FromIncome) * s.Rate / 100m;
        }
        var rebate = income <= rule.RebateIncomeLimit ? Math.Min(tax, rule.RebateMaxAmount) : 0;
        // New regime: marginal relief — tax just above the rebate limit may not exceed the income above that limit.
        if (rule.Regime == TaxRegime.New && rule.RebateMaxAmount > 0 && income > rule.RebateIncomeLimit)
            rebate = Math.Max(0, tax - (income - rule.RebateIncomeLimit));
        var afterRebate = tax - rebate;
        var surchargeRate = rule.Surcharges.Where(s => income > s.AboveIncome).Select(s => s.Rate).DefaultIfEmpty(0).Max();
        var surcharge = afterRebate * surchargeRate / 100m;
        var cess = (afterRebate + surcharge) * rule.CessRate / 100m;
        var total = Math.Round(afterRebate + surcharge + cess, MidpointRounding.AwayFromZero);
        return new TaxComputation(income, Math.Round(tax, 2), Math.Round(rebate, 2), Math.Round(surcharge, 2), Math.Round(cess, 2), total);
    }
}

// ------------------------------------------------------------------ platform maintenance

public sealed class RateSetInput
{
    [Required, Display(Name = "Effective from")] public DateOnly EffectiveFrom { get; set; }
    [Range(0, 100), Display(Name = "PF employee %")] public decimal PfEmployeeRate { get; set; }
    [Range(0, 100), Display(Name = "PF employer %")] public decimal PfEmployerRate { get; set; }
    [Range(0, 100), Display(Name = "EPS %")] public decimal EpsRate { get; set; }
    [Range(0, 10_000_000), Display(Name = "PF wage ceiling")] public decimal PfWageCeiling { get; set; }
    [Range(0, 100), Display(Name = "EDLI %")] public decimal EdliRate { get; set; }
    [Range(0, 100), Display(Name = "PF admin charges %")] public decimal PfAdminChargesRate { get; set; }
    [Range(0, 100), Display(Name = "ESI employee %")] public decimal EsiEmployeeRate { get; set; }
    [Range(0, 100), Display(Name = "ESI employer %")] public decimal EsiEmployerRate { get; set; }
    [Range(0, 10_000_000), Display(Name = "ESI wage limit")] public decimal EsiWageLimit { get; set; }
    [Range(0, 50), Display(Name = "Gratuity: min service (years)")] public decimal GratuityMinServiceYears { get; set; }
    [Range(0, 60), Display(Name = "Gratuity: days per year")] public decimal GratuityDaysPerYear { get; set; }
    [Range(1, 31), Display(Name = "Gratuity: wage divisor")] public decimal GratuityWageDivisor { get; set; }
    [Range(0, 100_000_000), Display(Name = "Gratuity ceiling")] public decimal GratuityCeiling { get; set; }
    [Display(Name = "Verified against current regulations")] public bool IsVerified { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class TaxRuleInput
{
    [Range(2000, 2100), Display(Name = "Financial year (start)")] public int FinancialYear { get; set; }
    public TaxRegime Regime { get; set; }
    [Range(0, 10_000_000), Display(Name = "Standard deduction")] public decimal StandardDeduction { get; set; }
    [Range(0, 100_000_000), Display(Name = "Rebate u/s 87A: income up to")] public decimal RebateIncomeLimit { get; set; }
    [Range(0, 10_000_000), Display(Name = "Rebate u/s 87A: maximum")] public decimal RebateMaxAmount { get; set; }
    [Range(0, 100), Display(Name = "Health & education cess %")] public decimal CessRate { get; set; }
    [Range(0, 10_000_000), Display(Name = "80C limit")] public decimal Section80CLimit { get; set; }
    [Range(0, 10_000_000), Display(Name = "80D limit")] public decimal Section80DLimit { get; set; }
    [Range(0, 10_000_000), Display(Name = "80CCD(1B) limit")] public decimal Section80CCD1BLimit { get; set; }
    [Range(0, 10_000_000), Display(Name = "Home-loan interest limit")] public decimal HomeLoanInterestLimit { get; set; }
    [Display(Name = "Verified against the Finance Act")] public bool IsVerified { get; set; }
    /// <summary>One slab per line: "from,to,rate" (leave "to" empty for the top slab).</summary>
    [Required, Display(Name = "Slabs (from,to,rate% — one per line)")] public string Slabs { get; set; } = string.Empty;
    [Display(Name = "Surcharge (above income,rate% — one per line)")] public string? Surcharges { get; set; }
}

public interface IStatutoryRuleService
{
    Task<IReadOnlyList<StatutoryRateSet>> RateSetsAsync(CancellationToken ct = default);
    Task<Result> SaveRateSetAsync(int? id, RateSetInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ProfessionalTaxSlab>> PtSlabsAsync(CancellationToken ct = default);
    Task<Result> AddPtSlabAsync(ProfessionalTaxSlab slab, CancellationToken ct = default);
    Task<IReadOnlyList<LabourWelfareFundRule>> LwfRulesAsync(CancellationToken ct = default);
    Task<Result> AddLwfRuleAsync(LabourWelfareFundRule rule, CancellationToken ct = default);
    Task<Result> DeleteAsync(string kind, int id, CancellationToken ct = default);
    Task<IReadOnlyList<IncomeTaxRule>> TaxRulesAsync(CancellationToken ct = default);
    Task<TaxRuleInput?> GetTaxRuleAsync(int id, CancellationToken ct = default);
    Task<Result> SaveTaxRuleAsync(int? id, TaxRuleInput input, CancellationToken ct = default);

    // Reads used by payroll (no platform permission needed: rules are public reference data)
    Task<StatutoryRateSet?> RatesAsOfAsync(DateOnly date, CancellationToken ct = default);
    Task<IncomeTaxRule?> TaxRuleAsync(int financialYear, TaxRegime regime, CancellationToken ct = default);
    Task<IReadOnlyList<ProfessionalTaxSlab>> PtSlabsForAsync(string? state, DateOnly date, CancellationToken ct = default);
    Task<LabourWelfareFundRule?> LwfForAsync(string? state, DateOnly date, CancellationToken ct = default);
}

public sealed class StatutoryRuleService(IAppDbContext db, ICurrentContext context) : IStatutoryRuleService
{
    private void EnsurePlatform()
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Statutory rules are maintained by the platform owner.");
    }

    public async Task<IReadOnlyList<StatutoryRateSet>> RateSetsAsync(CancellationToken ct = default) =>
        await db.StatutoryRateSets.AsNoTracking().OrderByDescending(r => r.EffectiveFrom).ToListAsync(ct);

    public async Task<Result> SaveRateSetAsync(int? id, RateSetInput i, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (i.EpsRate > i.PfEmployerRate) return Result.Fail("EPS cannot exceed the employer PF rate.");
        if (await db.StatutoryRateSets.AnyAsync(r => r.EffectiveFrom == i.EffectiveFrom && r.Id != id, ct)) return Result.Fail("A rate set already starts on this date.");
        StatutoryRateSet? r;
        if (id is null) r = db.StatutoryRateSets.Add(new StatutoryRateSet()).Entity;
        else if ((r = await db.StatutoryRateSets.FirstOrDefaultAsync(x => x.Id == id, ct)) is null) return Result.Fail("Rate set not found.");
        r.EffectiveFrom = i.EffectiveFrom; r.PfEmployeeRate = i.PfEmployeeRate; r.PfEmployerRate = i.PfEmployerRate; r.EpsRate = i.EpsRate;
        r.PfWageCeiling = i.PfWageCeiling; r.EdliRate = i.EdliRate; r.PfAdminChargesRate = i.PfAdminChargesRate; r.EsiEmployeeRate = i.EsiEmployeeRate;
        r.EsiEmployerRate = i.EsiEmployerRate; r.EsiWageLimit = i.EsiWageLimit; r.GratuityMinServiceYears = i.GratuityMinServiceYears;
        r.GratuityDaysPerYear = i.GratuityDaysPerYear; r.GratuityWageDivisor = i.GratuityWageDivisor; r.GratuityCeiling = i.GratuityCeiling;
        r.IsVerified = i.IsVerified; r.Notes = i.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ProfessionalTaxSlab>> PtSlabsAsync(CancellationToken ct = default) =>
        await db.ProfessionalTaxSlabs.AsNoTracking().OrderBy(s => s.State).ThenByDescending(s => s.EffectiveFrom).ThenBy(s => s.Gender).ThenBy(s => s.MinMonthlyGross).ToListAsync(ct);

    public async Task<Result> AddPtSlabAsync(ProfessionalTaxSlab slab, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (string.IsNullOrWhiteSpace(slab.State)) return Result.Fail("State is required.");
        if (slab.MaxMonthlyGross is { } max && max < slab.MinMonthlyGross) return Result.Fail("Maximum must be at least the minimum.");
        if (slab.MonthlyAmount < 0) return Result.Fail("Amount cannot be negative.");
        if (slab.SpecialMonth is { } m && (m < 1 || m > 12)) return Result.Fail("Special month must be 1–12.");
        slab.Id = 0; // posted directly from a form: never trust ids or audit fields
        slab.CreatedBy = null; slab.UpdatedBy = null; slab.UpdatedAtUtc = null;
        slab.State = slab.State.Trim();
        db.ProfessionalTaxSlabs.Add(slab);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<LabourWelfareFundRule>> LwfRulesAsync(CancellationToken ct = default) =>
        await db.LabourWelfareFundRules.AsNoTracking().OrderBy(r => r.State).ThenByDescending(r => r.EffectiveFrom).ToListAsync(ct);

    public async Task<Result> AddLwfRuleAsync(LabourWelfareFundRule rule, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (string.IsNullOrWhiteSpace(rule.State)) return Result.Fail("State is required.");
        var months = (rule.Months ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (months.Length == 0 || months.Any(m => !int.TryParse(m, out var n) || n < 1 || n > 12)) return Result.Fail("Months must be numbers 1–12 separated by commas.");
        rule.Id = 0;
        rule.CreatedBy = null; rule.UpdatedBy = null; rule.UpdatedAtUtc = null;
        rule.State = rule.State.Trim();
        rule.Months = string.Join(",", months.Select(int.Parse).Distinct().OrderBy(m => m));
        db.LabourWelfareFundRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(string kind, int id, CancellationToken ct = default)
    {
        EnsurePlatform();
        switch (kind)
        {
            case "pt":
                var pt = await db.ProfessionalTaxSlabs.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (pt is null) return Result.Fail("Slab not found.");
                db.ProfessionalTaxSlabs.Remove(pt);
                break;
            case "lwf":
                var lwf = await db.LabourWelfareFundRules.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (lwf is null) return Result.Fail("Rule not found.");
                db.LabourWelfareFundRules.Remove(lwf);
                break;
            default: return Result.Fail("Unknown rule type.");
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<IncomeTaxRule>> TaxRulesAsync(CancellationToken ct = default) =>
        await db.IncomeTaxRules.AsNoTracking().Include(r => r.Slabs).Include(r => r.Surcharges)
            .OrderByDescending(r => r.FinancialYear).ThenBy(r => r.Regime).ToListAsync(ct);

    public async Task<TaxRuleInput?> GetTaxRuleAsync(int id, CancellationToken ct = default)
    {
        var r = await db.IncomeTaxRules.AsNoTracking().Include(x => x.Slabs).Include(x => x.Surcharges).FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : new TaxRuleInput
        {
            FinancialYear = r.FinancialYear, Regime = r.Regime, StandardDeduction = r.StandardDeduction, RebateIncomeLimit = r.RebateIncomeLimit,
            RebateMaxAmount = r.RebateMaxAmount, CessRate = r.CessRate, Section80CLimit = r.Section80CLimit, Section80DLimit = r.Section80DLimit,
            Section80CCD1BLimit = r.Section80CCD1BLimit, HomeLoanInterestLimit = r.HomeLoanInterestLimit, IsVerified = r.IsVerified,
            Slabs = string.Join("\n", r.Slabs.OrderBy(s => s.FromIncome).Select(s => $"{s.FromIncome:0.##},{s.ToIncome:0.##},{s.Rate:0.##}")),
            Surcharges = string.Join("\n", r.Surcharges.OrderBy(s => s.AboveIncome).Select(s => $"{s.AboveIncome:0.##},{s.Rate:0.##}"))
        };
    }

    public async Task<Result> SaveTaxRuleAsync(int? id, TaxRuleInput i, CancellationToken ct = default)
    {
        EnsurePlatform();
        var slabs = new List<IncomeTaxSlab>();
        foreach (var line in i.Slabs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split(',', StringSplitOptions.TrimEntries);
            if (p.Length != 3 || !decimal.TryParse(p[0], out var f) || !decimal.TryParse(p[2], out var rate) || (p[1] != "" && !decimal.TryParse(p[1], out _)))
                return Result.Fail($"Invalid slab line: '{line}'. Use from,to,rate.");
            decimal? to = p[1] == "" ? null : decimal.Parse(p[1]);
            slabs.Add(new IncomeTaxSlab { FromIncome = f, ToIncome = to, Rate = rate });
        }
        slabs = slabs.OrderBy(s => s.FromIncome).ToList();
        if (slabs.Count == 0 || slabs[0].FromIncome != 0) return Result.Fail("Slabs must start at 0.");
        for (var k = 0; k < slabs.Count; k++)
        {
            var isLast = k == slabs.Count - 1;
            if (isLast != (slabs[k].ToIncome is null)) return Result.Fail("Only the last slab may (and must) have an empty 'to'.");
            if (!isLast && slabs[k].ToIncome != slabs[k + 1].FromIncome) return Result.Fail("Slabs must be contiguous (each 'to' equals the next 'from').");
        }
        var surcharges = new List<IncomeTaxSurcharge>();
        foreach (var line in (i.Surcharges ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split(',', StringSplitOptions.TrimEntries);
            if (p.Length != 2 || !decimal.TryParse(p[0], out var above) || !decimal.TryParse(p[1], out var rate)) return Result.Fail($"Invalid surcharge line: '{line}'.");
            surcharges.Add(new IncomeTaxSurcharge { AboveIncome = above, Rate = rate });
        }
        if (await db.IncomeTaxRules.AnyAsync(r => r.FinancialYear == i.FinancialYear && r.Regime == i.Regime && r.Id != id, ct))
            return Result.Fail("Rules for this financial year and regime already exist.");

        IncomeTaxRule? rule;
        if (id is null) rule = db.IncomeTaxRules.Add(new IncomeTaxRule()).Entity;
        else
        {
            rule = await db.IncomeTaxRules.Include(x => x.Slabs).Include(x => x.Surcharges).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (rule is null) return Result.Fail("Tax rule not found.");
            db.IncomeTaxSlabs.RemoveRange(rule.Slabs);
            db.IncomeTaxSurcharges.RemoveRange(rule.Surcharges);
        }
        rule.FinancialYear = i.FinancialYear; rule.Regime = i.Regime; rule.StandardDeduction = i.StandardDeduction; rule.RebateIncomeLimit = i.RebateIncomeLimit;
        rule.RebateMaxAmount = i.RebateMaxAmount; rule.CessRate = i.CessRate; rule.Section80CLimit = i.Section80CLimit; rule.Section80DLimit = i.Section80DLimit;
        rule.Section80CCD1BLimit = i.Section80CCD1BLimit; rule.HomeLoanInterestLimit = i.HomeLoanInterestLimit; rule.IsVerified = i.IsVerified;
        rule.Slabs = slabs;
        rule.Surcharges = surcharges;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<StatutoryRateSet?> RatesAsOfAsync(DateOnly date, CancellationToken ct = default) =>
        db.StatutoryRateSets.AsNoTracking().Where(r => r.EffectiveFrom <= date).OrderByDescending(r => r.EffectiveFrom).FirstOrDefaultAsync(ct);

    public Task<IncomeTaxRule?> TaxRuleAsync(int financialYear, TaxRegime regime, CancellationToken ct = default) =>
        db.IncomeTaxRules.AsNoTracking().Include(r => r.Slabs).Include(r => r.Surcharges)
            .FirstOrDefaultAsync(r => r.FinancialYear == financialYear && r.Regime == regime, ct);

    public async Task<IReadOnlyList<ProfessionalTaxSlab>> PtSlabsForAsync(string? state, DateOnly date, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state)) return [];
        var latest = await db.ProfessionalTaxSlabs.AsNoTracking().Where(s => s.State == state && s.EffectiveFrom <= date)
            .MaxAsync(s => (DateOnly?)s.EffectiveFrom, ct);
        return latest is null ? [] : await db.ProfessionalTaxSlabs.AsNoTracking().Where(s => s.State == state && s.EffectiveFrom == latest).ToListAsync(ct);
    }

    public Task<LabourWelfareFundRule?> LwfForAsync(string? state, DateOnly date, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(state) ? Task.FromResult<LabourWelfareFundRule?>(null)
        : db.LabourWelfareFundRules.AsNoTracking().Where(r => r.State == state && r.EffectiveFrom <= date).OrderByDescending(r => r.EffectiveFrom).FirstOrDefaultAsync(ct);
}
