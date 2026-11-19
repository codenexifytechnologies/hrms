using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;

namespace Codenexify.HRMS.Tests;

/// <summary>Exact arithmetic of the payroll engine and tax calculator against hand-computed figures.</summary>
public sealed class PayrollEngineTests
{
    private static readonly StatutoryRateSet Rates = new()
    {
        PfEmployeeRate = 12, PfEmployerRate = 12, EpsRate = 8.33m, PfWageCeiling = 15000, EdliRate = 0.5m, PfAdminChargesRate = 0.5m,
        EsiEmployeeRate = 0.75m, EsiEmployerRate = 3.25m, EsiWageLimit = 21000,
        GratuityMinServiceYears = 5, GratuityDaysPerYear = 15, GratuityWageDivisor = 26, GratuityCeiling = 2000000, IsVerified = true
    };

    private static readonly IReadOnlyList<ComponentDef> Standard =
    [
        new("BASIC", "Basic", ComponentType.Earning, ComponentCalculation.PercentOfGross, 50, true, true, true, true, true, false, 10),
        new("HRA", "HRA", ComponentType.Earning, ComponentCalculation.PercentOfBasic, 40, true, false, true, true, false, true, 20),
        new("SPECIAL", "Special", ComponentType.Earning, ComponentCalculation.Balancing, 0, true, false, true, true, false, false, 90)
    ];

    private static IncomeTaxRule NewRegime() => new()
    {
        Regime = TaxRegime.New, StandardDeduction = 75000, RebateIncomeLimit = 1200000, RebateMaxAmount = 60000, CessRate = 4,
        Slabs =
        [
            new() { FromIncome = 0, ToIncome = 400000, Rate = 0 }, new() { FromIncome = 400000, ToIncome = 800000, Rate = 5 },
            new() { FromIncome = 800000, ToIncome = 1200000, Rate = 10 }, new() { FromIncome = 1200000, ToIncome = 1600000, Rate = 15 },
            new() { FromIncome = 1600000, ToIncome = 2000000, Rate = 20 }, new() { FromIncome = 2000000, ToIncome = 2400000, Rate = 25 },
            new() { FromIncome = 2400000, ToIncome = null, Rate = 30 }
        ],
        Surcharges = [new() { AboveIncome = 5000000, Rate = 10 }, new() { AboveIncome = 10000000, Rate = 15 }]
    };

    private static IncomeTaxRule OldRegime() => new()
    {
        Regime = TaxRegime.Old, StandardDeduction = 50000, RebateIncomeLimit = 500000, RebateMaxAmount = 12500, CessRate = 4,
        Section80CLimit = 150000, Section80DLimit = 25000, Section80CCD1BLimit = 50000, HomeLoanInterestLimit = 200000,
        Slabs = [new() { FromIncome = 0, ToIncome = 250000, Rate = 0 }, new() { FromIncome = 250000, ToIncome = 500000, Rate = 5 },
                 new() { FromIncome = 500000, ToIncome = 1000000, Rate = 20 }, new() { FromIncome = 1000000, ToIncome = null, Rate = 30 }]
    };

    private static decimal Line(PayResult r, string code) => r.Lines.Where(l => l.Code == code).Sum(l => l.Amount);

    [Fact]
    public void Breakup_follows_the_structure_and_balances_to_gross()
    {
        var (amounts, error) = PayrollEngine.Breakup(50000, Standard);
        Assert.Null(error);
        Assert.Equal([25000m, 10000m, 15000m], amounts.Select(a => a.MonthlyAmount));

        var tooRich = Standard.Select(c => c.Code == "BASIC" ? c with { Calculation = ComponentCalculation.FixedAmount, Value = 60000 } : c).ToList();
        Assert.NotNull(PayrollEngine.Breakup(50000, tooRich).Error);
    }

    [Fact]
    public void Pf_is_capped_at_the_ceiling_and_eps_is_split_from_the_employer_share()
    {
        var r = PayrollEngine.Calculate(new PayInput { Month = 5, DaysInMonth = 31, PaidDays = 31, AnnualGross = 600000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false });
        Assert.Equal(50000, r.GrossEarnings);
        Assert.Equal(25000, r.PfWages);
        Assert.Equal(1800, Line(r, "PF"));      // 12% of 15,000
        Assert.Equal(1250, Line(r, "EPS"));     // 8.33% of 15,000 = 1,249.5 → 1,250
        Assert.Equal(550, Line(r, "PF-ER"));    // 1,800 − 1,250
        Assert.Equal(75, Line(r, "EDLI"));
        Assert.Equal(75, Line(r, "PF-ADM"));
        Assert.Equal(48200, r.NetPay);

        var uncapped = PayrollEngine.Calculate(new PayInput { Month = 5, DaysInMonth = 31, PaidDays = 31, AnnualGross = 600000, Components = Standard, Rates = Rates, PfRestrictToCeiling = false, EsiEnabled = false, PtEnabled = false, LwfEnabled = false });
        Assert.Equal(3000, Line(uncapped, "PF"));  // 12% of 25,000
        Assert.Equal(1250, Line(uncapped, "EPS")); // EPS always on capped wages

        var optedOut = PayrollEngine.Calculate(new PayInput { Month = 5, DaysInMonth = 31, PaidDays = 31, AnnualGross = 600000, Components = Standard, Rates = Rates, PfApplicable = false, EsiEnabled = false, PtEnabled = false, LwfEnabled = false });
        Assert.Equal(0, Line(optedOut, "PF"));
    }

    [Fact]
    public void Lop_prorates_earnings_and_esi_applies_below_the_wage_limit()
    {
        var r = PayrollEngine.Calculate(new PayInput { Month = 6, DaysInMonth = 30, PaidDays = 25, AnnualGross = 216000, Components = Standard, Rates = Rates, PtEnabled = false, LwfEnabled = false });
        Assert.Equal(18000, r.MonthlyGross);
        Assert.Equal(7500, Line(r, "BASIC"));    // 9,000 × 25/30
        Assert.Equal(3000, Line(r, "HRA"));      // 3,600 × 25/30
        Assert.Equal(4500, Line(r, "SPECIAL"));  // 5,400 × 25/30
        Assert.Equal(15000, r.EsiWages);
        Assert.Equal(113, Line(r, "ESI"));       // 0.75% of 15,000 = 112.5 → rounded up
        Assert.Equal(488, Line(r, "ESI-ER"));    // 3.25% of 15,000 = 487.5 → rounded up
        Assert.Equal(900, Line(r, "PF"));        // 12% of 7,500

        var above = PayrollEngine.Calculate(new PayInput { Month = 6, DaysInMonth = 30, PaidDays = 30, AnnualGross = 600000, Components = Standard, Rates = Rates, PtEnabled = false, LwfEnabled = false });
        Assert.Equal(0, Line(above, "ESI"));
    }

    [Fact]
    public void Professional_tax_and_lwf_follow_state_slabs_gender_and_months()
    {
        var slabs = new List<ProfessionalTaxSlab>
        {
            new() { MinMonthlyGross = 0, MaxMonthlyGross = 7500, MonthlyAmount = 0, Gender = Gender.Male },
            new() { MinMonthlyGross = 7500.01m, MaxMonthlyGross = 10000, MonthlyAmount = 175, Gender = Gender.Male },
            new() { MinMonthlyGross = 10000.01m, MonthlyAmount = 200, SpecialMonth = 2, SpecialMonthAmount = 300, Gender = Gender.Male },
            new() { MinMonthlyGross = 0, MaxMonthlyGross = 25000, MonthlyAmount = 0, Gender = Gender.Female },
            new() { MinMonthlyGross = 25000.01m, MonthlyAmount = 200, Gender = Gender.Female, SpecialMonth = 2, SpecialMonthAmount = 300 }
        };
        var lwf = new LabourWelfareFundRule { EmployeeAmount = 25, EmployerAmount = 75, Months = "6,12" };
        PayResult Run(int month, decimal annual, Gender g) => PayrollEngine.Calculate(new PayInput
            { Month = month, DaysInMonth = 30, PaidDays = 30, AnnualGross = annual, Components = Standard, Rates = Rates, EsiEnabled = false, PtSlabs = slabs, Lwf = lwf, Gender = g });

        Assert.Equal(200, Line(Run(6, 600000, Gender.Male), "PT"));
        Assert.Equal(300, Line(Run(2, 600000, Gender.Male), "PT"));
        Assert.Equal(0, Line(Run(6, 240000, Gender.Female), "PT"));   // 20,000/month, women exempt up to 25,000
        Assert.Equal(25, Line(Run(6, 600000, Gender.Male), "LWF"));
        Assert.Equal(75, Line(Run(12, 600000, Gender.Male), "LWF-ER"));
        Assert.Equal(0, Line(Run(7, 600000, Gender.Male), "LWF"));
    }

    [Fact]
    public void Tax_calculator_applies_slabs_rebate_marginal_relief_surcharge_and_cess()
    {
        Assert.Equal(0, TaxCalculator.Compute(1200000, NewRegime()).TotalTax);           // full 87A rebate
        Assert.Equal(10400, TaxCalculator.Compute(1210000, NewRegime()).TotalTax);       // marginal relief: 10,000 + 4% cess
        Assert.Equal(150800, TaxCalculator.Compute(1725000, NewRegime()).TotalTax);      // 1,45,000 + cess
        Assert.Equal(1578720, TaxCalculator.Compute(6000000, NewRegime()).TotalTax);     // 13,80,000 + 10% surcharge + cess
        Assert.Equal(117000, TaxCalculator.Compute(1000000, OldRegime()).TotalTax);      // 1,12,500 + cess
        Assert.Equal(0, TaxCalculator.Compute(500000, OldRegime()).TotalTax);            // 87A old regime
    }

    [Fact]
    public void Tds_is_the_projected_annual_tax_spread_over_remaining_months()
    {
        // 18 L gross, new regime, April: taxable 17,25,000 → 1,50,800 for the year → /12
        var april = PayrollEngine.Calculate(new PayInput
        {
            Month = 4, DaysInMonth = 30, PaidDays = 30, AnnualGross = 1800000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false, PfEnabled = false,
            Tax = new TaxInputs { Rule = NewRegime(), RemainingMonthsAfterThis = 11 }
        });
        Assert.Equal(150800, april.ProjectedAnnualTax);
        Assert.Equal(12567, Line(april, "TDS"));

        // Same person in October after 6 months of TDS at 12,567: the remainder is spread over 6 months.
        var october = PayrollEngine.Calculate(new PayInput
        {
            Month = 10, DaysInMonth = 31, PaidDays = 31, AnnualGross = 1800000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false, PfEnabled = false,
            Tax = new TaxInputs { Rule = NewRegime(), RemainingMonthsAfterThis = 5, YtdTaxableEarnings = 900000, YtdTds = 75402 }
        });
        Assert.Equal(12566, Line(october, "TDS")); // (1,50,800 − 75,402) / 6 = 12,566.33

        // 12 L gross under the new regime: no tax at all.
        var rebate = PayrollEngine.Calculate(new PayInput
        {
            Month = 4, DaysInMonth = 30, PaidDays = 30, AnnualGross = 1200000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false,
            Tax = new TaxInputs { Rule = NewRegime(), RemainingMonthsAfterThis = 11 }
        });
        Assert.Equal(0, Line(rebate, "TDS"));
    }

    [Fact]
    public void Old_regime_uses_hra_exemption_80c_including_pf_and_other_declarations()
    {
        var r = PayrollEngine.Calculate(new PayInput
        {
            Month = 4, DaysInMonth = 30, PaidDays = 30, AnnualGross = 1200000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false,
            Tax = new TaxInputs
            {
                Rule = OldRegime(), RemainingMonthsAfterThis = 11,
                Declaration = new TaxDeclaration { Regime = TaxRegime.Old, MonthlyRentPaid = 25000, MetroCity = true, Section80C = 100000 }
            }
        });
        // Taxable = 12,00,000 − 50,000 std − 2,40,000 HRA − 1,21,600 (80C incl. PF 1,800×12) = 7,88,400 → 70,180 + 4% cess = 72,987
        Assert.Equal(TaxRegime.Old, r.Regime);
        Assert.Equal(72987, r.ProjectedAnnualTax);
        Assert.Equal(6082, Line(r, "TDS"));
    }

    [Fact]
    public void Overtime_encashment_and_loans_are_added_and_net_pay_is_consistent()
    {
        var r = PayrollEngine.Calculate(new PayInput
        {
            Month = 5, DaysInMonth = 31, PaidDays = 31, AnnualGross = 600000, Components = Standard, Rates = Rates, EsiEnabled = false, PtEnabled = false, LwfEnabled = false,
            OvertimePay = 1442.3m, LeaveEncashmentPay = 1923.08m, LoanDeductions = [("LOAN7", "Loan recovery", 2000)]
        });
        Assert.Equal(1442, Line(r, "OT"));
        Assert.Equal(1923, Line(r, "LEAVEENC"));
        Assert.Equal(2000, Line(r, "LOAN7"));
        Assert.Equal(50000 + 1442 + 1923, r.GrossEarnings);
        Assert.Equal(r.GrossEarnings - r.TotalDeductions, r.NetPay);
        Assert.Equal(r.TotalDeductions, r.Lines.Where(l => l.Kind == PayrollLineKind.Deduction).Sum(l => l.Amount));
    }

    [Fact]
    public void Gratuity_counts_part_years_over_six_months_and_respects_the_ceiling()
    {
        Assert.Equal((4.92m, 5), PayrollEngine.ServiceFor(new DateOnly(2020, 1, 1), new DateOnly(2024, 12, 10)));
        Assert.Equal((5.42m, 5), PayrollEngine.ServiceFor(new DateOnly(2020, 1, 1), new DateOnly(2025, 6, 15)));
        Assert.Equal((5.58m, 6), PayrollEngine.ServiceFor(new DateOnly(2020, 1, 1), new DateOnly(2025, 8, 15)));
        Assert.Equal(86538, PayrollEngine.Gratuity(25000, 6, Rates));       // 25,000 × 15/26 × 6
        Assert.Equal(2000000, PayrollEngine.Gratuity(500000, 30, Rates));   // capped
        Assert.Equal(11, PayrollEngine.RemainingMonthsAfter(4));
        Assert.Equal(0, PayrollEngine.RemainingMonthsAfter(3));
        Assert.Equal(2025, PayrollEngine.FinancialYearOf(2026, 3));
    }
}
