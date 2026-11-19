using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Promotions and improvement plans. Clock: 15 June 2026. Maya manages Arjun and Bela; Chen has no manager.</summary>
public sealed class PromotionAndPipTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin, _junior, _senior;
    private int _maya, _arjun, _bela, _chen;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("grow", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@grow.test").Select(u => u.Id).SingleAsync());
        _host.Context.AsTenant(_org, _admin);
        (_junior, _senior) = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var j = new Domain.CoreHr.Designation { OrganizationId = _org, Name = "Engineer" };
            var s = new Domain.CoreHr.Designation { OrganizationId = _org, Name = "Senior Engineer" };
            db.Designations.AddRange(j, s);
            await db.SaveChangesAsync();
            return (j.Id, s.Id);
        });
        _maya = await HireAsync("Maya", null, null, 0);
        _arjun = await HireAsync("Arjun", _maya, _junior, 600000);
        _bela = await HireAsync("Bela", _maya, _junior, 0);
        _chen = await HireAsync("Chen", null, null, 0);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? manager, int? designation, decimal salary)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "G", WorkEmail = $"{name.ToLowerInvariant()}@grow.test", DateOfJoining = new DateOnly(2025, 1, 1), ReportingManagerId = manager, DesignationId = designation },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        if (salary > 0)
        {
            var structure = await _host.RunAsync(sp => S<AppDbContext>(sp).SalaryStructures.Select(s => s.Id).FirstAsync());
            var a = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).AssignAsync(r.Value, new SalaryAssignInput { SalaryStructureId = structure, AnnualGross = salary, EffectiveFrom = new DateOnly(2025, 1, 1) }));
            Assert.True(a.Succeeded, a.Error);
        }
        return r.Value;
    }

    private Task<T> P<T>(Func<IPromotionService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IPromotionService>(sp))); }
    private Task<T> I<T>(Func<IPipService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IPipService>(sp))); }

    private PromotionInput Promote(int emp, DateOnly on, decimal? gross = 720000) => new()
    {
        EmployeeId = emp, ToDesignationId = _senior, ProposedAnnualGross = gross, EffectiveOn = on,
        Justification = "Led the payments migration and mentors two new engineers."
    };

    // ------------------------------------------------------------------ promotions

    [Fact]
    public async Task Manager_proposes_hr_approves_and_title_and_salary_change_on_the_date()
    {
        Assert.False((await P(s => s.ProposeAsync(_chen, false, Promote(_arjun, new DateOnly(2026, 7, 1))))).Succeeded); // not their team
        Assert.False((await P(s => s.ProposeAsync(_maya, false, new PromotionInput { EmployeeId = _arjun, ToDesignationId = _junior, EffectiveOn = new DateOnly(2026, 7, 1), Justification = "No change at all, really nothing." }))).Succeeded);
        Assert.Contains("no salary", (await P(s => s.ProposeAsync(_maya, false, Promote(_bela, new DateOnly(2026, 7, 1))))).Error); // Bela has no salary

        var id = await P(s => s.ProposeAsync(_maya, false, Promote(_arjun, new DateOnly(2026, 7, 1))));
        Assert.True(id.Succeeded, id.Error);
        Assert.False((await P(s => s.ProposeAsync(_maya, false, Promote(_arjun, new DateOnly(2026, 8, 1))))).Succeeded); // one open case at a time

        var view = (await P(s => s.GetAsync(id.Value, _maya, false)))!;
        Assert.Equal(20, view.Row.RaisePercent);
        Assert.Equal("Engineer", view.Row.FromTitle);
        Assert.False(view.CanDecide);
        Assert.Null(await P(s => s.GetAsync(id.Value, _bela, false))); // peers can't see it

        Assert.False((await P(s => s.DecideAsync(id.Value, false, null))).Succeeded); // reason required to reject
        Assert.True((await P(s => s.DecideAsync(id.Value, true, "Well deserved"))).Succeeded);
        Assert.Equal(PromotionStatus.Approved, (await P(s => s.GetAsync(id.Value, null, true)))!.Case.Status); // future date: not applied yet
        Assert.Equal(0, await P(s => s.ApplyDueAsync()));

        _host.Clock.UtcNow = new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, await P(s => s.ApplyDueAsync()));
        var emp = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.AsNoTracking().SingleAsync(e => e.Id == _arjun));
        Assert.Equal(_senior, emp.DesignationId);
        var salary = await _host.RunAsync(sp => S<IPayrollSetupService>(sp).SalaryAsOfAsync(_arjun, new DateOnly(2026, 7, 1)));
        Assert.Equal(720000, salary!.AnnualGross);
        Assert.Equal(new DateOnly(2026, 7, 1), salary.EffectiveFrom);
        Assert.Equal(PromotionStatus.Applied, (await P(s => s.GetAsync(id.Value, null, true)))!.Case.Status);
    }

    [Fact]
    public async Task Hr_promotion_effective_today_applies_on_approval_and_title_only_works_without_salary()
    {
        var id = (await P(s => s.ProposeAsync(null, true, Promote(_bela, new DateOnly(2026, 6, 15), gross: null)))).Value;
        Assert.True((await P(s => s.DecideAsync(id, true, null))).Succeeded);
        Assert.Equal(PromotionStatus.Applied, (await P(s => s.GetAsync(id, null, true)))!.Case.Status);
        Assert.Equal(_senior, await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == _bela).Select(e => e.DesignationId).SingleAsync()));

        // Withdrawal by the proposer only, while undecided.
        var other = (await P(s => s.ProposeAsync(_maya, false, Promote(_arjun, new DateOnly(2026, 9, 1))))).Value;
        Assert.False((await P(s => s.WithdrawAsync(other, _chen, false))).Succeeded);
        Assert.True((await P(s => s.WithdrawAsync(other, _maya, false))).Succeeded);
        Assert.Equal(2, (await P(s => s.ListAsync(null))).Count);
        Assert.Single(await P(s => s.ListAsync(PromotionStatus.Withdrawn)));
    }

    // ------------------------------------------------------------------ improvement plans

    private static PipInput Plan(int emp) => new()
    {
        EmployeeId = emp, StartsOn = new DateOnly(2026, 6, 15), EndsOn = new DateOnly(2026, 8, 15),
        Concerns = "Missed four of the last six sprint commitments and two customer escalations were not followed up.",
        Support = "Weekly coaching with Maya; time-management course.",
        Objectives = [new() { Description = "Deliver sprint commitments", Measure = "At least 5 of the next 6 sprints delivered" }, new() { Description = "Follow up escalations", Measure = "Every escalation answered within 1 working day" }]
    };

    [Fact]
    public async Task Plan_runs_from_draft_to_outcome_with_the_right_people_involved()
    {
        Assert.False((await I(s => s.CreateAsync(_bela, false, Plan(_arjun)))).Succeeded); // peer
        var bad = Plan(_arjun); bad.EndsOn = new DateOnly(2026, 6, 20);
        Assert.Contains("14", (await I(s => s.CreateAsync(_maya, false, bad))).Error);
        var noMeasure = Plan(_arjun); noMeasure.Objectives[0].Measure = null;
        Assert.False((await I(s => s.CreateAsync(_maya, false, noMeasure))).Succeeded);

        var id = await I(s => s.CreateAsync(_maya, false, Plan(_arjun)));
        Assert.True(id.Succeeded, id.Error);
        Assert.False((await I(s => s.CreateAsync(_maya, false, Plan(_arjun)))).Succeeded); // already open

        // Draft: the employee can't see it yet; the manager can edit it; only HR starts it.
        Assert.Null(await I(s => s.GetAsync(id.Value, _arjun, false)));
        Assert.True((await I(s => s.GetAsync(id.Value, _maya, false)))!.CanEditDraft);
        Assert.Null(await I(s => s.GetAsync(id.Value, _chen, false)));
        Assert.True((await I(s => s.StartAsync(id.Value))).Succeeded);
        Assert.False((await I(s => s.StartAsync(id.Value))).Succeeded);

        var asEmployee = (await I(s => s.GetAsync(id.Value, _arjun, false)))!;
        Assert.True(asEmployee.CanAcknowledge);
        Assert.True((await I(s => s.AcknowledgeAsync(id.Value, _arjun, "I understand and will work on it."))).Succeeded);
        Assert.False((await I(s => s.AcknowledgeAsync(id.Value, _arjun, null))).Succeeded);
        Assert.Single(await I(s => s.MineAsync(_arjun)));
        Assert.Single(await I(s => s.ForManagerAsync(_maya)));

        // Check-ins and objectives by the manager; not by the employee.
        Assert.True((await I(s => s.CheckInAsync(id.Value, _maya, false, PipProgress.OnTrack, "Delivered sprint 1 in full, good progress."))).Succeeded);
        Assert.False((await I(s => s.CheckInAsync(id.Value, _arjun, false, PipProgress.OnTrack, "I think it went great"))).Succeeded);
        var objective = asEmployee.Plan.Objectives.First().Id;
        Assert.True((await I(s => s.SetObjectiveAsync(objective, _maya, false, PipObjectiveStatus.Met))).Succeeded);

        // Extension rules, then HR records the outcome (managers can't).
        Assert.False((await I(s => s.ExtendAsync(id.Value, new DateOnly(2026, 8, 1), "earlier"))).Succeeded);
        Assert.True((await I(s => s.ExtendAsync(id.Value, new DateOnly(2026, 9, 15), "More time for the escalation objective"))).Succeeded);
        Assert.True((await I(s => s.ExtendAsync(id.Value, new DateOnly(2026, 9, 30), "Holiday season"))).Succeeded);
        Assert.Contains("at most", (await I(s => s.ExtendAsync(id.Value, new DateOnly(2026, 10, 15), "Again"))).Error);
        Assert.False((await I(s => s.CloseAsync(id.Value, _maya, false, PipStatus.Passed, "Done"))).Succeeded);
        Assert.True((await I(s => s.CloseAsync(id.Value, null, true, PipStatus.Passed, "Both objectives met consistently."))).Succeeded);

        var closed = (await I(s => s.GetAsync(id.Value, _arjun, false)))!;
        Assert.Equal(PipStatus.Passed, closed.Plan.Status);
        Assert.Equal(2, closed.Plan.Extensions);
        Assert.Equal(3, closed.Plan.CheckIns.Count); // one check-in + two extension notes
    }

    [Fact]
    public async Task Manager_can_cancel_a_draft_but_not_an_active_plan()
    {
        var draft = (await I(s => s.CreateAsync(_maya, false, Plan(_bela)))).Value;
        Assert.True((await I(s => s.CloseAsync(draft, _maya, false, PipStatus.Cancelled, "Spoke and resolved informally."))).Succeeded);
        var active = (await I(s => s.CreateAsync(null, true, Plan(_bela)))).Value;
        Assert.True((await I(s => s.StartAsync(active))).Succeeded);
        Assert.False((await I(s => s.CloseAsync(active, _maya, false, PipStatus.Cancelled, "Changed my mind"))).Succeeded);
        Assert.False((await I(s => s.CloseAsync(active, null, true, PipStatus.Draft, "Not an outcome"))).Succeeded);
    }
}
