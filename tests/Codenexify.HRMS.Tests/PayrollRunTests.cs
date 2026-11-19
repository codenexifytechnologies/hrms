using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Payroll runs over the database. Clock: Monday 15 June 2026; payroll for May 2026 (31 days, 5 Sundays).</summary>
public sealed class PayrollRunTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "FOUNDATION", trial: false);
        _other = await _host.ProvisionAsync("globex", "FOUNDATION", trial: false);
        _host.Clock.UtcNow = new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<(int Employee, int User)> HireAsync(int org, string name, decimal annualGross, DateOnly? joined = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "P", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = joined ?? new DateOnly(2025, 1, 1) },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        await _host.RunAsync(sp => S<IEmployeeService>(sp).UpdatePersonalAsync(r.Value, new PersonalInput { Nationality = "Indian", Gender = Gender.Male }, false));
        var structure = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.Select(s => s.Id).SingleAsync());
        var assign = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(r.Value, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = annualGross, EffectiveFrom = joined ?? new DateOnly(2025, 1, 1) }));
        Assert.True(assign.Succeeded, assign.Error);
        var uid = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync());
        return (r.Value, uid);
    }

    private async Task UseMaharashtraAsync(int org)
    {
        _host.Context.AsTenant(org);
        var s = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).GetSettingsAsync());
        s.DefaultState = "Maharashtra";
        Assert.True((await _host.RunAsync(sp => S<IPayrollSetupService>(sp).SaveSettingsAsync(s))).Succeeded);
    }

    private Task<PayrollDetail> DetailAsync(int runId, int employeeId) =>
        _host.RunAsync(sp => S<AppDbContext>(sp).PayrollDetails.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.PayrollRunId == runId && d.EmployeeId == employeeId));

    [Fact]
    public async Task Absences_become_lop_overrides_survive_reprocessing_and_statutory_lines_are_added()
    {
        await UseMaharashtraAsync(_org);
        var (emp, _) = await HireAsync(_org, "Asha", 600000);
        _host.Context.AsTenant(_org);
        var runs = (Func<IServiceProvider, IPayrollRunService>)S<IPayrollRunService>;
        var run = await _host.RunAsync(sp => runs(sp).CreateAsync(2026, 5));
        Assert.True(run.Succeeded, run.Error);
        Assert.True((await _host.RunAsync(sp => runs(sp).ProcessAsync(run.Value))).Succeeded);

        // No punches in May: all 26 working days are absences → 5 paid days (the Sundays).
        var d = await DetailAsync(run.Value, emp);
        Assert.Equal(26, d.LopDays);
        Assert.Equal(5, d.PaidDays);
        Assert.Equal(8064, d.GrossEarnings); // 5/31 of each component: 4,032 + 1,613 + 2,419

        Assert.True((await _host.RunAsync(sp => runs(sp).SetLopOverrideAsync(d.Id, 1))).Succeeded);
        d = await DetailAsync(run.Value, emp);
        Assert.Equal(30, d.PaidDays);
        Assert.Equal(1, d.LopOverride);
        Assert.Equal(48387, d.GrossEarnings);
        Assert.Equal(1800, d.Lines.Single(l => l.Code == "PF").Amount);
        Assert.Equal(200, d.Lines.Single(l => l.Code == "PT").Amount);   // Maharashtra
        Assert.DoesNotContain(d.Lines, l => l.Code == "LWF");            // only June/December
        Assert.DoesNotContain(d.Lines, l => l.Code == "ESI");            // above the ESI limit

        Assert.True((await _host.RunAsync(sp => runs(sp).ProcessAsync(run.Value))).Succeeded); // override kept
        Assert.Equal(1, (await DetailAsync(run.Value, emp)).LopDays);
    }

    [Fact]
    public async Task Finalizing_recovers_loans_pays_encashment_publishes_payslips_and_locks_the_run()
    {
        await UseMaharashtraAsync(_org);
        var (emp, empUser) = await HireAsync(_org, "Bala", 600000);
        _host.Context.AsTenant(_org);

        var loanReq = await _host.RunAsync(sp => S<ILoanService>(sp).RequestAsync(emp, new LoanRequestInput { Type = LoanType.Loan, Amount = 10000, InstallmentAmount = 2000, RecoveryStart = new DateOnly(2026, 6, 1), Reason = "Medical" }, byHr: true));
        Assert.True(loanReq.Succeeded, loanReq.Error);
        var loanId = await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeLoans.Select(l => l.Id).SingleAsync());
        Assert.True((await _host.RunAsync(sp => S<ILoanService>(sp).DecideAsync(loanId, true, "ok"))).Succeeded);
        await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeLoans.Where(l => l.Id == loanId).ExecuteUpdateAsync(u => u.SetProperty(l => l.RecoveryStart, new DateOnly(2026, 5, 1))));

        var el = await _host.RunAsync(sp => S<AppDbContext>(sp).LeaveTypes.Where(t => t.Code == "EL").Select(t => t.Id).SingleAsync());
        Assert.True((await _host.RunAsync(sp => S<ILeaveService>(sp).EncashAsync(emp, el, 2026, 2))).Succeeded);

        var runs = (Func<IServiceProvider, IPayrollRunService>)S<IPayrollRunService>;
        var run = (await _host.RunAsync(sp => runs(sp).CreateAsync(2026, 5))).Value;
        await _host.RunAsync(sp => runs(sp).ProcessAsync(run));
        var d = await DetailAsync(run, emp);
        await _host.RunAsync(sp => runs(sp).SetLopOverrideAsync(d.Id, 0));
        d = await DetailAsync(run, emp);
        Assert.Equal(2000, d.Lines.Single(l => l.Code == $"LOAN{loanId}").Amount);
        Assert.Equal(1923, d.Lines.Single(l => l.Code == "LEAVEENC").Amount); // 2 days × 25,000 / 26
        Assert.Equal(2, d.LeaveEncashmentDays);

        // Not visible to the employee before finalization.
        _host.Context.AsTenant(_org, empUser);
        Assert.Null(await _host.RunAsync(sp => runs(sp).PayslipAsync(d.Id, emp)));

        _host.Context.AsTenant(_org);
        var fin = await _host.RunAsync(sp => runs(sp).FinalizeAsync(run));
        Assert.True(fin.Succeeded, fin.Error);
        Assert.False((await _host.RunAsync(sp => runs(sp).ProcessAsync(run))).Succeeded);
        Assert.False((await _host.RunAsync(sp => runs(sp).SetLopOverrideAsync(d.Id, 3))).Succeeded);
        Assert.False((await _host.RunAsync(sp => runs(sp).DeleteAsync(run))).Succeeded);

        var loan = await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeLoans.AsNoTracking().Include(l => l.Repayments).SingleAsync(l => l.Id == loanId));
        Assert.Equal(8000, loan.Outstanding);
        Assert.Single(loan.Repayments);
        var balance = await _host.RunAsync(sp => S<AppDbContext>(sp).LeaveBalances.AsNoTracking().SingleAsync(b => b.EmployeeId == emp && b.LeaveTypeId == el && b.Year == 2026));
        Assert.Equal(2, balance.EncashmentPaid);

        _host.Context.AsTenant(_org, empUser);
        var slip = await _host.RunAsync(sp => runs(sp).PayslipAsync(d.Id, emp));
        Assert.NotNull(slip);
        Assert.Equal(d.NetPay, slip!.Detail.NetPay);
        Assert.Single(await _host.RunAsync(sp => runs(sp).MyPayslipsAsync(emp)));
        Assert.Equal(2, await _host.RunAsync(sp => S<INotificationService>(sp).UnreadCountAsync())); // loan approved + payslip published

        // June: encashment already paid, loan continues.
        _host.Context.AsTenant(_org);
        var june = (await _host.RunAsync(sp => runs(sp).CreateAsync(2026, 6))).Value;
        await _host.RunAsync(sp => runs(sp).ProcessAsync(june));
        var jd = await DetailAsync(june, emp);
        Assert.DoesNotContain(jd.Lines, l => l.Code == "LEAVEENC");
        Assert.Equal(2000, jd.Lines.Single(l => l.Code == $"LOAN{loanId}").Amount);
        Assert.Contains(jd.Lines, l => l.Code == "LWF"); // Maharashtra LWF in June

        // Exports
        var register = await _host.RunAsync(sp => runs(sp).ExportAsync(run, "register"));
        Assert.Equal((byte)'P', register.Content[0]);
        var journal = await _host.RunAsync(sp => runs(sp).ExportAsync(run, "journal"));
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(journal.Content));
        var ws = wb.Worksheet(1);
        decimal Col(int c) => ws.RowsUsed().Skip(1).Sum(r => r.Cell(c).IsEmpty() ? 0 : r.Cell(c).GetValue<decimal>());
        Assert.Equal(Col(3), Col(4)); // debits = credits
        await Assert.ThrowsAsync<InvalidOperationException>(() => _host.RunAsync(sp => runs(sp).ExportAsync(june, "bank"))); // not final yet
    }

    [Fact]
    public async Task Mid_month_joiner_is_paid_only_for_employed_days_and_future_months_are_refused()
    {
        var (emp, _) = await HireAsync(_org, "Chen", 600000, new DateOnly(2026, 5, 20));
        _host.Context.AsTenant(_org);
        var runs = (Func<IServiceProvider, IPayrollRunService>)S<IPayrollRunService>;
        Assert.False((await _host.RunAsync(sp => runs(sp).CreateAsync(2026, 7))).Succeeded);
        var run = (await _host.RunAsync(sp => runs(sp).CreateAsync(2026, 5))).Value;
        await _host.RunAsync(sp => runs(sp).ProcessAsync(run));
        var d = await DetailAsync(run, emp);
        await _host.RunAsync(sp => runs(sp).SetLopOverrideAsync(d.Id, 0));
        d = await DetailAsync(run, emp);
        Assert.Equal(12, d.PaidDays); // 20–31 May
        Assert.Contains("Employed 12 of 31 days", d.Warnings);
    }

    [Fact]
    public async Task Payroll_data_is_isolated_between_organizations()
    {
        var (emp, _) = await HireAsync(_org, "Dev", 600000);
        _host.Context.AsTenant(_org);
        var run = (await _host.RunAsync(sp => S<IPayrollRunService>(sp).CreateAsync(2026, 5))).Value;
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(run));
        var detail = await DetailAsync(run, emp);

        _host.Context.AsTenant(_other);
        Assert.Empty(await _host.RunAsync(sp => S<IPayrollRunService>(sp).RunsAsync()));
        Assert.Null(await _host.RunAsync(sp => S<IPayrollRunService>(sp).PayslipAsync(detail.Id, null)));
        Assert.False((await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(run))).Succeeded);
        Assert.False((await _host.RunAsync(sp => S<IPayrollRunService>(sp).SetHoldAsync(detail.Id, true, "x"))).Succeeded);
        var structureA = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.IgnoreQueryFilters().Where(s => s.OrganizationId == _org).Select(s => s.Id).FirstAsync());
        var (empB, _) = await HireAsync(_other, "Other", 300000);
        _host.Context.AsTenant(_other);
        Assert.False((await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(empB, new SalaryAssignInput { SalaryStructureId = structureA, AnnualGross = 1, EffectiveFrom = new DateOnly(2026, 6, 1) }))).Succeeded);
    }

    [Fact]
    public async Task Full_and_final_includes_gratuity_leave_encashment_and_loan_recovery()
    {
        var (emp, _) = await HireAsync(_org, "Elder", 600000, new DateOnly(2019, 1, 1));
        _host.Context.AsTenant(_org);
        await _host.RunAsync(sp => S<ILoanService>(sp).RequestAsync(emp, new LoanRequestInput { Type = LoanType.SalaryAdvance, Amount = 5000, InstallmentAmount = 5000, RecoveryStart = new DateOnly(2026, 7, 1), Reason = "x" }, true));
        var loanId = await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeLoans.Where(l => l.EmployeeId == emp).Select(l => l.Id).SingleAsync());
        await _host.RunAsync(sp => S<ILoanService>(sp).DecideAsync(loanId, true, null));
        var exit = await _host.RunAsync(sp => S<IExitService>(sp).InitiateAsync(emp, new ExitInitiateInput { Reason = "Retiring", RequestedLastWorkingDay = new DateOnly(2026, 6, 30), Type = ExitType.Retirement }, false));
        await _host.RunAsync(sp => S<IExitService>(sp).ApproveAsync(exit.Value, new DateOnly(2026, 6, 30), null));

        var prep = await _host.RunAsync(sp => S<ISettlementService>(sp).PrepareAsync(emp));
        Assert.True(prep.Succeeded, prep.Error);
        var s = (await _host.RunAsync(sp => S<ISettlementService>(sp).GetAsync(prep.Value)))!;
        Assert.Contains(s.Lines, l => l.Description.StartsWith("Gratuity") && l.Amount == 100962); // 25,000 × 15/26 × 7 years (7y 5m → 7)
        Assert.Contains(s.Lines, l => l.Description.StartsWith("Leave encashment"));
        Assert.Contains(s.Lines, l => l.Amount == -5000);

        Assert.True((await _host.RunAsync(sp => S<ISettlementService>(sp).AddLineAsync(prep.Value, "Notice pay shortfall", -3000))).Succeeded);
        s = (await _host.RunAsync(sp => S<ISettlementService>(sp).GetAsync(prep.Value)))!;
        Assert.Equal(8000, s.TotalRecoverable);
        Assert.Equal(s.TotalPayable - 8000, s.NetAmount);

        Assert.True((await _host.RunAsync(sp => S<ISettlementService>(sp).ApproveAsync(prep.Value, null))).Succeeded);
        Assert.False((await _host.RunAsync(sp => S<ISettlementService>(sp).AddLineAsync(prep.Value, "late", 1))).Succeeded);
        Assert.True((await _host.RunAsync(sp => S<ISettlementService>(sp).MarkPaidAsync(prep.Value, new DateOnly(2026, 7, 5)))).Succeeded);
        Assert.Equal(LoanStatus.Closed, await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeLoans.Where(l => l.Id == loanId).Select(l => l.Status).SingleAsync()));
    }

    [Fact]
    public async Task Employee_regime_choice_drives_tds_once_submitted()
    {
        await UseMaharashtraAsync(_org);
        var (emp, _) = await HireAsync(_org, "Farah", 1800000);
        _host.Context.AsTenant(_org);
        var decl = (Func<IServiceProvider, ITaxDeclarationService>)S<ITaxDeclarationService>;
        Assert.True((await _host.RunAsync(sp => decl(sp).SaveAsync(emp, new DeclarationInput { Regime = TaxRegime.Old, Section80C = 150000, MonthlyRentPaid = 30000, MetroCity = true, Submit = true }))).Succeeded);
        var estimates = await _host.RunAsync(sp => decl(sp).CompareRegimesAsync(emp));
        Assert.Equal(2, estimates.Count);

        // April = first month of the financial year, so the projection covers all 12 months like the estimate.
        var run = (await _host.RunAsync(sp => S<IPayrollRunService>(sp).CreateAsync(2026, 4))).Value;
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(run));
        var d = await DetailAsync(run, emp);
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).SetLopOverrideAsync(d.Id, 0));
        d = await DetailAsync(run, emp);
        Assert.Equal(TaxRegime.Old, d.Regime);
        var estimate = estimates.Single(e => e.Regime == TaxRegime.Old).Computation.TotalTax;
        Assert.InRange(d.ProjectedAnnualTax, estimate - 1000, estimate); // same basis; payroll also deducts professional tax
        Assert.True(d.Lines.Single(l => l.Code == "TDS").Amount > 0);
    }
}
