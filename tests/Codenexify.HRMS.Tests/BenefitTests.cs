using System.Text;
using Codenexify.HRMS.Application.Benefits;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Domain.Benefits;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Perks & benefits. Clock: 15 June 2026.</summary>
public sealed class BenefitTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("perks", "FOUNDATION", trial: false);
        _other = await _host.ProvisionAsync("rival", "FOUNDATION", trial: false);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private Task<T> Hr<T>(Func<IBenefitService, Task<T>> action, int? org = null)
    {
        _host.Context.AsTenant(org ?? _org);
        return _host.RunAsync(sp => action(S<IBenefitService>(sp)));
    }

    private async Task<int> HireAsync(string name, DateOnly joined, EmploymentType type = EmploymentType.FullTime, EmployeeStatus status = EmployeeStatus.Active, decimal salary = 0)
    {
        _host.Context.AsTenant(_org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "K", WorkEmail = $"{name.ToLowerInvariant()}@perks.test", DateOfJoining = joined, EmploymentType = type },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var e = await db.Employees.SingleAsync(x => x.Id == r.Value);
            e.Status = status;
            await db.SaveChangesAsync();
        });
        if (salary > 0)
        {
            var structure = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.Select(s => s.Id).SingleAsync());
            Assert.True((await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(r.Value, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = salary, EffectiveFrom = joined }))).Succeeded);
        }
        return r.Value;
    }

    private static BenefitPlanInput Health(BenefitEnrollmentMode mode = BenefitEnrollmentMode.Automatic) => new()
    {
        Name = "Group health", Category = BenefitCategory.HealthInsurance, CoverageAmount = 500000, EmployerCostPerMonth = 800,
        EmployeeContributionPerMonth = 200, CoversDependents = true, MaxDependents = 3, DependentContributionPerMonth = 150,
        EnrollmentMode = mode, EligibleEmploymentTypes = [EmploymentType.FullTime], MinServiceDays = 30
    };

    private Task<List<BenefitEnrollment>> EnrollmentsAsync(int planId) =>
        _host.RunAsync(sp => S<AppDbContext>(sp).BenefitEnrollments.IgnoreQueryFilters().Include(e => e.Dependents).Where(e => e.BenefitPlanId == planId).ToListAsync());

    [Fact]
    public async Task Automatic_plan_enrols_only_eligible_employees_and_hr_can_make_exceptions()
    {
        var veteran = await HireAsync("Veer", new DateOnly(2025, 1, 1));
        var newbie = await HireAsync("Neha", new DateOnly(2026, 6, 1));                        // < 30 days of service
        var intern = await HireAsync("Ishan", new DateOnly(2025, 1, 1), EmploymentType.Intern); // wrong type

        var saved = await Hr(s => s.SaveAsync(null, Health()));
        Assert.True(saved.Succeeded, saved.Error);
        var plan = saved.Value;
        var members = await EnrollmentsAsync(plan);
        Assert.Equal([veteran], members.Select(m => m.EmployeeId));
        Assert.Equal("Automatic", members[0].EnrolledBy);

        var view = await Hr(s => s.GetAsync(plan));
        Assert.DoesNotContain(view!.EligibleNotEnrolled, e => e.EmployeeId == newbie);
        Assert.Contains(view.IneligibleNotEnrolled, e => e.EmployeeId == intern);

        // HR exception: the intern is enrolled anyway and the hourly sync leaves it alone.
        Assert.True((await Hr(s => s.EnrollAsync(plan, intern, new DateOnly(2026, 6, 15)))).Succeeded);
        Assert.False((await Hr(s => s.EnrollAsync(plan, intern, new DateOnly(2026, 6, 15)))).Succeeded); // already enrolled
        Assert.Equal(0, await Hr(s => s.SyncAsync(plan)));

        // Once eligible (30 days of service), the new joiner is added by the sync.
        _host.Clock.UtcNow = new DateTime(2026, 7, 2, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, await Hr(s => s.SyncAsync(null)));
        Assert.Contains(await EnrollmentsAsync(plan), m => m.EmployeeId == newbie && m.StartsOn == new DateOnly(2026, 7, 2));

        // Another company never sees the plan.
        Assert.Null(await Hr(s => s.GetAsync(plan), _other));
        Assert.Empty(await Hr(s => s.ListAsync(), _other));
    }

    [Fact]
    public async Task Leavers_and_discontinued_plans_end_memberships()
    {
        var a = await HireAsync("Arun", new DateOnly(2025, 1, 1));
        var b = await HireAsync("Bina", new DateOnly(2025, 1, 1));
        var plan = (await Hr(s => s.SaveAsync(null, Health()))).Value;

        await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            foreach (var m in db.BenefitEnrollments.Where(x => x.BenefitPlanId == plan)) m.StartsOn = new DateOnly(2026, 4, 1);
            var e = await db.Employees.SingleAsync(x => x.Id == a);
            e.Status = EmployeeStatus.Exited;
            e.DateOfExit = new DateOnly(2026, 6, 10);
            await db.SaveChangesAsync();
        });
        Assert.Equal(1, await Hr(s => s.SyncAsync(null)));
        var ended = (await EnrollmentsAsync(plan)).Single(m => m.EmployeeId == a);
        Assert.Equal(BenefitEnrollmentStatus.Ended, ended.Status);
        Assert.Equal(new DateOnly(2026, 6, 10), ended.EndsOn);

        var input = (await Hr(s => s.GetForEditAsync(plan)))!;
        input.IsActive = false;
        Assert.True((await Hr(s => s.SaveAsync(plan, input))).Succeeded);
        Assert.All(await EnrollmentsAsync(plan), m => Assert.Equal(BenefitEnrollmentStatus.Ended, m.Status));
        Assert.Contains(await EnrollmentsAsync(plan), m => m.EmployeeId == b && m.EndReason == "Plan discontinued");
    }

    [Fact]
    public async Task Employee_opts_in_with_family_members_and_leaves_at_month_end()
    {
        var me = await HireAsync("Meera", new DateOnly(2025, 1, 1));
        var plan = (await Hr(s => s.SaveAsync(null, Health(BenefitEnrollmentMode.OptIn)))).Value;
        Assert.Empty(await EnrollmentsAsync(plan)); // opt-in: nobody added automatically

        _host.Context.AsTenant(_org);
        var mine = await _host.RunAsync(sp => S<IBenefitService>(sp).MineAsync(me));
        Assert.True(mine.Single().CanJoin);

        DependentInput Dep(string n, DependentRelation r) => new() { Name = n, Relation = r, DateOfBirth = new DateOnly(1995, 3, 1) };
        Assert.Contains("at most 3", (await Hr(s => s.JoinAsync(plan, me, [Dep("A", DependentRelation.Child), Dep("B", DependentRelation.Child), Dep("C", DependentRelation.Child), Dep("D", DependentRelation.Child)]))).Error);
        Assert.Contains("one spouse", (await Hr(s => s.JoinAsync(plan, me, [Dep("A", DependentRelation.Spouse), Dep("B", DependentRelation.Spouse)]))).Error);
        Assert.True((await Hr(s => s.JoinAsync(plan, me, [Dep("Ravi", DependentRelation.Spouse), Dep("Tara", DependentRelation.Child)]))).Succeeded);

        mine = await Hr(s => s.MineAsync(me));
        var b = mine.Single();
        Assert.Equal(200 + 2 * 150, b.MonthlyDeduction);
        Assert.True(b.CanLeave && b.CanEditDependents && !b.CanJoin);

        // Edit family: drop the child.
        Assert.True((await Hr(s => s.SaveDependentsAsync(b.Enrollment!.Id, me, [Dep("Ravi", DependentRelation.Spouse)]))).Succeeded);
        Assert.Equal(350, (await Hr(s => s.MineAsync(me))).Single().MonthlyDeduction);

        Assert.True((await Hr(s => s.LeaveAsync(plan, me))).Succeeded);
        var en = (await EnrollmentsAsync(plan)).Single();
        Assert.Equal(new DateOnly(2026, 6, 30), en.EndsOn);
        Assert.False((await Hr(s => s.JoinAsync(plan, me, []))).Succeeded); // still covered this month
    }

    [Fact]
    public async Task Enrolment_window_and_eligibility_are_enforced_for_opt_in()
    {
        var me = await HireAsync("Om", new DateOnly(2025, 1, 1), status: EmployeeStatus.Probation);
        var input = Health(BenefitEnrollmentMode.OptIn);
        input.ExcludeProbation = true;
        input.EnrollmentOpensOn = new DateOnly(2026, 7, 1);
        input.EnrollmentClosesOn = new DateOnly(2026, 7, 31);
        var plan = (await Hr(s => s.SaveAsync(null, input))).Value;

        Assert.Contains("probation", (await Hr(s => s.JoinAsync(plan, me, []))).Error);
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); (await db.Employees.SingleAsync(x => x.Id == me)).Status = EmployeeStatus.Active; await db.SaveChangesAsync(); });
        Assert.Contains("closed", (await Hr(s => s.JoinAsync(plan, me, []))).Error);
        Assert.Contains("opens on 01 Jul 2026", (await Hr(s => s.MineAsync(me))).Single().Note);

        _host.Clock.UtcNow = new DateTime(2026, 7, 5, 6, 0, 0, DateTimeKind.Utc);
        Assert.True((await Hr(s => s.JoinAsync(plan, me, []))).Succeeded);

        // Validation of the plan itself.
        var bad = Health();
        bad.EnrollmentOpensOn = new DateOnly(2026, 7, 1);
        Assert.Contains(bad.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(bad)), v => v.ErrorMessage!.Contains("opt-in"));
    }

    [Fact]
    public async Task Payroll_pays_allowances_and_deducts_contributions_for_active_months()
    {
        var emp = await HireAsync("Pia", new DateOnly(2025, 1, 1), salary: 600000);
        var late = await HireAsync("Lal", new DateOnly(2025, 1, 1), salary: 600000);
        var health = (await Hr(s => s.SaveAsync(null, Health(BenefitEnrollmentMode.OptIn)))).Value;
        var meal = (await Hr(s => s.SaveAsync(null, new BenefitPlanInput { Name = "Meal card", Category = BenefitCategory.Meal, AllowancePerMonth = 2200, AllowanceTaxable = false }))).Value;

        // Pia joined health from 1 May with her spouse; Lal is only in the automatic meal plan (enrolled today, 15 June).
        Assert.True((await Hr(s => s.EnrollAsync(health, emp, new DateOnly(2026, 5, 1)))).Succeeded);
        var en = (await EnrollmentsAsync(health)).Single();
        Assert.True((await Hr(s => s.SaveDependentsAsync(en.Id, null, [new DependentInput { Name = "Raj", Relation = DependentRelation.Spouse }]))).Succeeded);
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); foreach (var m in db.BenefitEnrollments.Where(x => x.BenefitPlanId == meal && x.EmployeeId == emp)) m.StartsOn = new DateOnly(2026, 5, 1); await db.SaveChangesAsync(); });

        _host.Context.AsTenant(_org);
        var run = await _host.RunAsync(sp => S<IPayrollRunService>(sp).CreateAsync(2026, 5));
        Assert.True(run.Succeeded, run.Error);
        await _host.RunAsync(sp => S<IPayrollRunService>(sp).ProcessAsync(run.Value));
        var lines = (await _host.RunAsync(sp => S<AppDbContext>(sp).PayrollDetails.AsNoTracking().Include(d => d.Lines).Where(d => d.PayrollRunId == run.Value).ToListAsync()))
            .SelectMany(d => d.Lines.Select(l => new { d.EmployeeId, l.Code, l.Kind, l.Amount, l.IsTaxable })).ToList();

        var pia = lines.Where(l => l.EmployeeId == emp).ToList();
        Assert.Contains(pia, l => l.Code == $"BEN{health}" && l.Kind == Domain.Payroll.PayrollLineKind.Deduction && l.Amount == 350);
        Assert.Contains(pia, l => l.Code == $"BENA{meal}" && l.Kind == Domain.Payroll.PayrollLineKind.Earning && l.Amount == 2200 && !l.IsTaxable);
        // Lal's meal membership starts in June: nothing in May.
        Assert.DoesNotContain(lines, l => l.EmployeeId == late && l.Code.StartsWith("BEN"));
    }

    [Fact]
    public async Task Policy_document_must_be_a_pdf_and_is_only_for_members_or_eligible_staff()
    {
        var member = await HireAsync("Mona", new DateOnly(2025, 1, 1));
        var intern = await HireAsync("Ira", new DateOnly(2025, 1, 1), EmploymentType.Intern);
        var plan = (await Hr(s => s.SaveAsync(null, Health()))).Value;

        UploadedFile File(string name, string content) => new(new MemoryStream(Encoding.ASCII.GetBytes(content)), name, content.Length);
        Assert.False((await Hr(s => s.UploadDocumentAsync(plan, File("policy.pdf", "not really a pdf")))).Succeeded);
        Assert.False((await Hr(s => s.UploadDocumentAsync(plan, File("policy.exe", "%PDF-1.7 fake")))).Succeeded);
        Assert.True((await Hr(s => s.UploadDocumentAsync(plan, File("policy.pdf", "%PDF-1.7 minimal")))).Succeeded);

        Assert.NotNull(await Hr(s => s.DocumentAsync(plan, null)));      // HR
        Assert.NotNull(await Hr(s => s.DocumentAsync(plan, member)));    // member
        Assert.Null(await Hr(s => s.DocumentAsync(plan, intern)));       // not eligible, not enrolled

        var export = await Hr(s => s.ExportMembersAsync(plan));
        Assert.NotNull(export);
        Assert.True(export!.Length > 100);
    }
}
