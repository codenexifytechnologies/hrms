using System.Text;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Bonus/arrears/reimbursements, expense claims, ECR and Form 16. Clock: 15 June 2026; payroll for May 2026 (31 days).</summary>
public sealed class PayrollExtrasTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other, _owner, _otherOwner;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "GROWTH", trial: false);
        _other = await _host.ProvisionAsync("globex", "GROWTH", trial: false);
        _host.Clock.UtcNow = new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc);
        _host.Context.AsPlatform();
        _owner = await OwnerAsync(_org);
        _otherOwner = await OwnerAsync(_other);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private Task<int> OwnerAsync(int org) =>
        _host.RunAsync(sp => S<AppDbContext>(sp).Users.IgnoreQueryFilters().Where(u => u.OrganizationId == org && u.IsOrganizationOwner).Select(u => u.Id).SingleAsync());

    private Task<T> As<T, TService>(int org, int user, Func<TService, Task<T>> a) where TService : notnull
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => a(S<TService>(sp)));
    }

    private async Task<(int Employee, int User)> HireAsync(int org, int owner, string name, decimal annualGross)
    {
        _host.Context.AsTenant(org, owner);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "P", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = new DateOnly(2025, 1, 1) },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var structure = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.Select(s => s.Id).SingleAsync());
        var assign = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(r.Value, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = annualGross, EffectiveFrom = new DateOnly(2025, 1, 1) }));
        Assert.True(assign.Succeeded, assign.Error);
        return (r.Value, await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync()));
    }

    /// <summary>Creates, processes with no LOP, and optionally finalizes a month.</summary>
    private async Task<int> RunAsync(int year, int month, bool finalize, Action<PayrollDetail>? beforeFinalize = null)
    {
        _host.Context.AsTenant(_org, _owner);
        var run = await _host.RunAsync(sp => S<IPayrollRunService>(sp).CreateAsync(year, month));
        Assert.True(run.Succeeded, run.Error);
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(run.Value));
        // Each override re-processes the run (details are recreated), so look the detail up again every time.
        foreach (var employeeId in await _host.RunAsync(sp => S<AppDbContext>(sp).PayrollDetails.Where(d => d.PayrollRunId == run.Value).Select(d => d.EmployeeId).ToListAsync()))
        {
            var id = await _host.RunAsync(sp => S<AppDbContext>(sp).PayrollDetails.Where(d => d.PayrollRunId == run.Value && d.EmployeeId == employeeId).Select(d => d.Id).SingleAsync());
            var r = await _host.RunAsync(sp => S<IPayrollRunService>(sp).SetLopOverrideAsync(id, 0));
            Assert.True(r.Succeeded, r.Error);
        }
        if (finalize)
        {
            var f = await _host.RunAsync(sp => S<IPayrollRunService>(sp).FinalizeAsync(run.Value));
            Assert.True(f.Succeeded, f.Error);
        }
        return run.Value;
    }

    private Task<PayrollDetail> DetailAsync(int runId, int employeeId)
    {
        _host.Context.AsTenant(_org, _owner);
        return _host.RunAsync(sp => S<AppDbContext>(sp).PayrollDetails.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.PayrollRunId == runId && d.EmployeeId == employeeId));
    }

    // ------------------------------------------------------------------ arrears

    [Fact]
    public void Arrears_are_the_difference_from_what_was_paid_prorated_from_the_effective_date()
    {
        // 50,000 a month paid; revised to 60,000 from 16 April.
        var paid = new List<PaidMonth> { new(2026, 4, 50000, 30, 30), new(2026, 5, 50000, 31, 31), new(2026, 3, 50000, 31, 31) };
        Assert.Equal(15000, ArrearsCalculator.Compute(720000, new DateOnly(2026, 4, 16), paid)); // half of April + all of May; March ignored
        Assert.Equal(5000, ArrearsCalculator.Compute(720000, new DateOnly(2026, 5, 1), [new(2026, 5, 50000, 15.5m, 31)])); // only the days actually paid
        Assert.Equal(-12000, ArrearsCalculator.Compute(528000, new DateOnly(2026, 4, 1), paid.Take(2).ToList())); // 44,000 vs 50,000 for Apr + May → recovery
    }

    [Fact]
    public async Task Back_dated_revision_pays_arrears_with_one_time_items_in_the_next_payroll()
    {
        var (emp, _) = await HireAsync(_org, _owner, "Asha", 600000);
        var may = await RunAsync(2026, 5, finalize: true);
        var structure = await As<int, AppDbContext>(_org, _owner, db => db.SalaryStructures.Select(s => s.Id).SingleAsync());
        var revision = new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = 720000, EffectiveFrom = new DateOnly(2026, 5, 1) };

        var refused = await As<Result<decimal>, IPayrollSetupService>(_org, _owner, s => s.AssignAsync(emp, revision));
        Assert.False(refused.Succeeded);
        revision.PayArrears = true;
        var saved = await As<Result<decimal>, IPayrollSetupService>(_org, _owner, s => s.AssignAsync(emp, revision));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(10000, saved.Value);
        // A second back-dated revision while arrears are unpaid is refused.
        Assert.False((await As<Result<decimal>, IPayrollSetupService>(_org, _owner, s => s.AssignAsync(emp, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = 780000, EffectiveFrom = new DateOnly(2026, 5, 1), PayArrears = true }))).Succeeded);

        // One-time items cannot go into a finalized month; they can into June.
        var adj = (Func<IPayrollAdjustmentService, AdjustmentInput, Task<Result>>)((s, i) => s.AddAsync(i));
        Assert.False((await As<Result, IPayrollAdjustmentService>(_org, _owner, s => s.AddAsync(new AdjustmentInput { EmployeeId = emp, Category = AdjustmentCategory.Bonus, Name = "Late bonus", Amount = 100, Year = 2026, Month = 5 }))).Succeeded);
        foreach (var i in new[]
        {
            new AdjustmentInput { EmployeeId = emp, Category = AdjustmentCategory.Bonus, Name = "Performance bonus", Amount = 5000, Year = 2026, Month = 6, IsTaxable = true },
            new AdjustmentInput { EmployeeId = emp, Category = AdjustmentCategory.Reimbursement, Name = "Phone bill", Amount = 2000, Year = 2026, Month = 6, IsTaxable = true },
            new AdjustmentInput { EmployeeId = emp, Category = AdjustmentCategory.Recovery, Name = "Notice pay shortfall", Amount = 1000, Year = 2026, Month = 6 },
        })
            Assert.True((await As<Result, IPayrollAdjustmentService>(_org, _owner, s => adj(s, i))).Succeeded);

        var june = await RunAsync(2026, 6, finalize: false);
        var d = await DetailAsync(june, emp);
        var earnings = d.Lines.Where(l => l.Kind == PayrollLineKind.Earning && l.Code.StartsWith("ADJ")).ToDictionary(l => l.Name);
        Assert.Equal(10000, earnings["Salary arrears (May 2026 – May 2026)"].Amount);
        Assert.True(earnings["Salary arrears (May 2026 – May 2026)"].IsTaxable);
        Assert.False(earnings["Phone bill"].IsTaxable); // reimbursements are never taxable
        Assert.Equal(1000, d.Lines.Single(l => l.Kind == PayrollLineKind.Deduction && l.Name == "Notice pay shortfall").Amount);
        Assert.Equal(60000 + 10000 + 5000 + 2000, d.GrossEarnings);
        Assert.Equal(60000 + 10000 + 5000, d.TaxableEarnings);
        Assert.Equal(1800, d.Lines.Single(l => l.Code == "PF").Amount); // one-time items are not PF wages (ceiling unchanged)

        Assert.True((await As<Result, IPayrollRunService>(_org, _owner, s => s.FinalizeAsync(june))).Succeeded);
        var pending = await As<IReadOnlyList<AdjustmentRow>, IPayrollAdjustmentService>(_org, _owner, s => s.ListAsync(true));
        Assert.Empty(pending);
        var paidId = (await As<IReadOnlyList<AdjustmentRow>, IPayrollAdjustmentService>(_org, _owner, s => s.ListAsync(false))).First().Id;
        Assert.False((await As<Result, IPayrollAdjustmentService>(_org, _owner, s => s.DeleteAsync(paidId))).Succeeded);
        // Arrears already paid for May: a later revision touching May is refused.
        Assert.False((await As<Result<decimal>, IPayrollSetupService>(_org, _owner, s => s.AssignAsync(emp, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = 780000, EffectiveFrom = new DateOnly(2026, 5, 1), PayArrears = true }))).Succeeded);
    }

    [Fact]
    public async Task One_time_items_of_a_held_salary_carry_forward()
    {
        var (emp, _) = await HireAsync(_org, _owner, "Ravi", 600000);
        Assert.True((await As<Result, IPayrollAdjustmentService>(_org, _owner, s => s.AddAsync(new AdjustmentInput { EmployeeId = emp, Category = AdjustmentCategory.Incentive, Name = "Sales incentive", Amount = 3000, Year = 2026, Month = 5 }))).Succeeded);
        _host.Context.AsTenant(_org, _owner);
        var may = await _host.RunAsync(sp => S<IPayrollRunService>(sp).CreateAsync(2026, 5));
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(may.Value));
        var detail = await DetailAsync(may.Value, emp);
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).SetLopOverrideAsync(detail.Id, 0));
        detail = await DetailAsync(may.Value, emp);
        var hold = await _host.RunAsync(sp => S<IPayrollRunService>(sp).SetHoldAsync(detail.Id, true, "Bank details pending"));
        Assert.True(hold.Succeeded, hold.Error);
        var fin = await _host.RunAsync(sp => S<IPayrollRunService>(sp).FinalizeAsync(may.Value));
        Assert.True(fin.Succeeded, fin.Error);
        Assert.Single(await As<IReadOnlyList<AdjustmentRow>, IPayrollAdjustmentService>(_org, _owner, s => s.ListAsync(true)));

        var june = await RunAsync(2026, 6, finalize: false);
        Assert.Contains((await DetailAsync(june, emp)).Lines, l => l.Name == "Sales incentive" && l.Amount == 3000);
    }

    // ------------------------------------------------------------------ expenses

    private static UploadedFile Png() => new(new MemoryStream([0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4]), "bill.png", 8);

    [Fact]
    public async Task Expense_claims_need_receipts_and_two_approvals_then_are_paid_with_payroll()
    {
        var (mgrEmp, mgrUser) = await HireAsync(_org, _owner, "Meera", 900000);
        var (emp, empUser) = await HireAsync(_org, _owner, "Kiran", 600000);
        _host.Context.AsTenant(_org, _owner);
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); (await db.Employees.SingleAsync(e => e.Id == emp)).ReportingManagerId = mgrEmp; await db.SaveChangesAsync(); });
        var cats = await As<IReadOnlyList<ExpenseCategoryRow>, IExpenseService>(_org, _owner, s => s.CategoriesAsync(true));
        var travel = cats.Single(c => c.Name == "Travel & conveyance");
        Assert.True((await As<Result, IExpenseService>(_org, _owner, s => s.SaveCategoryAsync(travel.Id, travel.Name, 1500, true, true))).Succeeded);
        var day = new DateOnly(2026, 6, 10);
        ExpenseClaimInput Claim(decimal amount) => new() { Title = "Client visit", Items = [new() { CategoryId = travel.Id, Date = day, Description = "Cab", Amount = amount }] };

        Assert.Contains("receipt", (await As<Result<int>, IExpenseService>(_org, empUser, s => s.SubmitAsync(emp, Claim(800), new Dictionary<int, UploadedFile>()))).Error);
        Assert.Contains("1,500", (await As<Result<int>, IExpenseService>(_org, empUser, s => s.SubmitAsync(emp, Claim(2000), new Dictionary<int, UploadedFile> { [0] = Png() }))).Error);
        var fake = new UploadedFile(new MemoryStream(Encoding.ASCII.GetBytes("not an image")), "bill.png", 12);
        Assert.Contains("does not match", (await As<Result<int>, IExpenseService>(_org, empUser, s => s.SubmitAsync(emp, Claim(800), new Dictionary<int, UploadedFile> { [0] = fake }))).Error);

        var id = (await As<Result<int>, IExpenseService>(_org, empUser, s => s.SubmitAsync(emp, Claim(800), new Dictionary<int, UploadedFile> { [0] = Png() }))).Value;
        Assert.Single(await As<IReadOnlyList<ExpenseClaimRow>, IExpenseService>(_org, mgrUser, s => s.AwaitingManagerAsync(mgrUser)));

        // Isolation: another organization's people can't see or decide the claim, nor open its receipt.
        var item = (await As<ExpenseClaimView?, IExpenseService>(_org, empUser, s => s.GetAsync(id, emp, null)))!.Items.Single();
        Assert.Null(await As<ExpenseClaimView?, IExpenseService>(_other, _otherOwner, s => s.GetAsync(id, null, null)));
        Assert.Null(await As<ReceiptFile?, IExpenseService>(_other, _otherOwner, s => s.ReceiptAsync(item.Id, null, null)));
        Assert.False((await As<Result, IExpenseService>(_other, _otherOwner, s => s.FinanceApproveAsync(id, ExpensePayout.Payroll, null))).Succeeded);
        Assert.NotNull(await As<ReceiptFile?, IExpenseService>(_org, mgrUser, s => s.ReceiptAsync(item.Id, null, mgrUser)));

        Assert.True((await As<Result, IExpenseService>(_org, mgrUser, s => s.ManagerDecideAsync(id, mgrUser, true, null))).Succeeded);
        // The claimant can never approve their own claim, whatever their role.
        Assert.False((await As<Result, IExpenseService>(_org, empUser, s => s.FinanceApproveAsync(id, ExpensePayout.Payroll, null))).Succeeded);
        Assert.True((await As<Result, IExpenseService>(_org, _owner, s => s.FinanceApproveAsync(id, ExpensePayout.Payroll, "ok"))).Succeeded);

        var june = await RunAsync(2026, 6, finalize: false); // nothing finalized yet → lands in the current month
        var line = (await DetailAsync(june, emp)).Lines.Single(l => l.Name == $"Expense reimbursement #{id}");
        Assert.Equal((800m, false), (line.Amount, line.IsTaxable));
        Assert.True((await As<Result, IPayrollRunService>(_org, _owner, s => s.FinalizeAsync(june))).Succeeded);
        var claim = (await As<ExpenseClaimView?, IExpenseService>(_org, empUser, s => s.GetAsync(id, emp, null)))!.Claim;
        Assert.Equal((ExpenseStatus.Paid, new DateOnly(2026, 6, 30)), (claim.Status, claim.PaidOn!.Value));
    }

    // ------------------------------------------------------------------ returns

    [Fact]
    public async Task Ecr_needs_uan_and_form16_and_24q_are_built_from_finalized_payroll()
    {
        var (emp, empUser) = await HireAsync(_org, _owner, "Asha", 600000);
        var may = await RunAsync(2026, 5, finalize: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => As<ExportFile, IPayrollRunService>(_org, _owner, s => s.ExportAsync(may, "ecr")));
        _host.Context.AsTenant(_org, _owner);
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); (await db.Employees.SingleAsync(e => e.Id == emp)).Uan = "100200300400"; await db.SaveChangesAsync(); });
        var ecr = Encoding.ASCII.GetString((await As<ExportFile, IPayrollRunService>(_org, _owner, s => s.ExportAsync(may, "ecr"))).Content);
        var fields = ecr.TrimEnd('\n').Split("#~#");
        Assert.Equal(11, fields.Length);
        Assert.Equal(("100200300400", "ASHA P", "50000", "15000", "1800", "0"), (fields[0], fields[1], fields[2], fields[3], fields[6], fields[9]));

        var form = await As<Form16Pdf?, ITaxReturnService>(_org, empUser, s => s.Form16Async(emp, 2026, revealPan: true));
        Assert.NotNull(form);
        Assert.True(form!.Provisional); // FY 2026-27 is not over
        Assert.Equal(("2027-28", 50000m), (form.AssessmentYear, form.Monthly.Single().Paid));
        Assert.Contains(form.Computation, c => c.Label.StartsWith("1. Gross salary") && c.Amount == 50000);
        var pdf = _host.Services.GetRequiredService<IPdfRenderer>().Form16(form);
        if (Environment.GetEnvironmentVariable("CNX_PDF_DUMP") is { Length: > 0 } dump) File.WriteAllBytes(Path.Combine(dump, "form16.pdf"), pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Null(await As<Form16Pdf?, ITaxReturnService>(_org, _owner, s => s.Form16Async(emp, 2025, false)));
        Assert.Null(await As<Form16Pdf?, ITaxReturnService>(_other, _otherOwner, s => s.Form16Async(emp, 2026, false)));

        var q1 = await As<ExportFile, ITaxReturnService>(_org, _owner, s => s.Form24QAnnexureAsync(2026, 1));
        Assert.Equal("form24q-annexure-FY26-27-Q1.xlsx", q1.FileName);
        Assert.True(q1.Content.Length > 0);
    }
}
