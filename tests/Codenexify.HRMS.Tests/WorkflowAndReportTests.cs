using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

/// <summary>Workflow automation and the report builder. Clock: 15 June 2026.</summary>
public sealed class WorkflowAndReportTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _admin, _adminUser, _sales;
    private int _maya;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("auto", "GROWTH", trial: false);
        _host.Context.AsTenant(_org);
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.Where(u => u.Email == "admin@auto.test").Select(u => u.Id).SingleAsync());
        _adminUser = _admin;
        _host.Context.AsTenant(_org, _admin);
        _sales = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var d = new Department { OrganizationId = _org, Name = "Sales" };
            db.Departments.Add(d);
            await db.SaveChangesAsync();
            return d.Id;
        });
        _maya = await HireAsync("Maya", null, null, new DateOnly(2024, 1, 1));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();
    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private async Task<int> HireAsync(string name, int? manager, int? department, DateOnly joined, EmploymentType type = EmploymentType.FullTime)
    {
        _host.Context.AsTenant(_org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "W", WorkEmail = $"{name.ToLowerInvariant()}@auto.test", DateOfJoining = joined, ReportingManagerId = manager, DepartmentId = department, EmploymentType = type },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        return r.Value;
    }

    private Task<T> W<T>(Func<IWorkflowService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IWorkflowService>(sp))); }
    private Task<T> B<T>(Func<IReportBuilderService, Task<T>> a) { _host.Context.AsTenant(_org, _admin); return _host.RunAsync(sp => a(S<IReportBuilderService>(sp))); }
    private Task<int> UserOf(int employee) => _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == employee).Select(e => e.UserId!.Value).SingleAsync());

    // ------------------------------------------------------------------ workflows

    [Fact]
    public async Task New_joiner_rule_creates_tasks_and_notifications_once_for_matching_people()
    {
        var rule = await W(s => s.SaveAsync(null, new WorkflowRuleInput
        {
            Name = "Sales joiner kit", Trigger = WorkflowTrigger.EmployeeAdded, DepartmentId = _sales,
            Actions =
            [
                new() { Use = true, Type = WorkflowActionType.CreateTask, Recipient = WorkflowRecipient.Manager, Title = "Order a laptop for {Employee}", DueInDays = 3 },
                new() { Use = true, Type = WorkflowActionType.Notify, Recipient = WorkflowRecipient.Employee, Title = "Welcome to {Department}, {Employee}!" },
                new() { Use = false, Type = WorkflowActionType.Notify, Title = "ignored blank row" },
            ]
        }));
        Assert.True(rule.Succeeded, rule.Error);
        Assert.Equal(2, (await W(s => s.GetForEditAsync(rule.Value)))!.Actions.Count);

        // Two joiners after the rule: one in Sales (matches), one elsewhere (doesn't).
        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(1);
        var ravi = await HireAsync("Ravi", _maya, _sales, new DateOnly(2026, 7, 1));
        await HireAsync("Ola", _maya, null, new DateOnly(2026, 7, 1));

        Assert.Equal(1, await W(s => s.RunAsync()));
        Assert.Equal(0, await W(s => s.RunAsync())); // never twice

        var tasks = await W(async s => s.TasksAsync(await UserOf(_maya), false));
        var task = Assert.Single(await tasks);
        Assert.Equal("Order a laptop for Ravi W", task.Title);
        Assert.Equal(new DateOnly(2026, 6, 18), task.DueOn);
        var raviUser = await UserOf(ravi);
        Assert.True(await _host.RunAsync(sp => S<AppDbContext>(sp).Notifications.IgnoreQueryFilters().AnyAsync(n => n.UserId == raviUser && n.Title == "Welcome to Sales, Ravi W!")));

        // Ticking the task, and the run log.
        var mayaUser = await UserOf(_maya);
        Assert.True((await W(s => s.ToggleTaskAsync(task.Id, mayaUser))).Succeeded);
        Assert.False((await W(s => s.ToggleTaskAsync(task.Id, raviUser))).Succeeded); // not theirs
        Assert.Empty(await W(s => s.TasksAsync(mayaUser, false)));
        Assert.True(Assert.Single(await W(s => s.RunsAsync(rule.Value))).Succeeded);
    }

    [Fact]
    public async Task Date_rules_fire_inside_their_window_and_problems_are_logged()
    {
        var ravi = await HireAsync("Ravi", null, null, new DateOnly(2026, 6, 20)); // no manager
        var rule = (await W(s => s.SaveAsync(null, new WorkflowRuleInput
        {
            Name = "Pre-joining", Trigger = WorkflowTrigger.BeforeJoining, DaysOffset = 3,
            Actions = [new() { Use = true, Type = WorkflowActionType.Notify, Recipient = WorkflowRecipient.Manager, Title = "{Employee} joins on {Date}" }]
        }))).Value;

        Assert.Equal(0, await W(s => s.RunAsync(rule))); // 5 days away
        _host.Clock.UtcNow = new DateTime(2026, 6, 17, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, await W(s => s.RunAsync(rule)));
        var run = Assert.Single(await W(s => s.RunsAsync(rule)));
        Assert.False(run.Succeeded);
        Assert.Contains("nobody to send it to", run.Detail);

        // Validation.
        Assert.False((await W(s => s.SaveAsync(null, new WorkflowRuleInput { Name = "Empty" }))).Succeeded);
        Assert.False((await W(s => s.SaveAsync(null, new WorkflowRuleInput { Name = "No title", Actions = [new() { Use = true, Type = WorkflowActionType.CreateTask }] }))).Succeeded);
        Assert.False((await W(s => s.SaveAsync(null, new WorkflowRuleInput { Name = "Bad plan", Actions = [new() { Use = true, Type = WorkflowActionType.EnrolBenefit, TargetId = 999 }] }))).Succeeded);
        Assert.False((await W(s => s.SaveAsync(null, new WorkflowRuleInput { Name = "Who", Actions = [new() { Use = true, Type = WorkflowActionType.Notify, Title = "x", Recipient = WorkflowRecipient.SpecificUser }] }))).Succeeded);
    }

    [Fact]
    public async Task Onboarding_action_needs_a_template_without_buddy_tasks_and_starts_it()
    {
        var standard = await _host.RunAsync(sp => S<AppDbContext>(sp).OnboardingTemplates.Where(t => t.OrganizationId == _org).Select(t => t.Id).FirstAsync());
        Assert.Contains("buddy", (await W(s => s.SaveAsync(null, new WorkflowRuleInput
        {
            Name = "Standard", Trigger = WorkflowTrigger.EmployeeAdded, Actions = [new() { Use = true, Type = WorkflowActionType.StartOnboarding, TargetId = standard }]
        }))).Error);

        _host.Context.AsTenant(_org, _admin);
        var template = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var t = new Domain.Strength.OnboardingTemplate { OrganizationId = _org, Name = "Simple" };
            t.Tasks.Add(new Domain.Strength.OnboardingTemplateTask { Title = "Sign the handbook", Assignee = Domain.Strength.OnboardingAssignee.Employee, DueOffsetDays = 1 });
            t.Tasks.Add(new Domain.Strength.OnboardingTemplateTask { Title = "Set up email", Assignee = Domain.Strength.OnboardingAssignee.Hr, DueOffsetDays = -1 });
            db.OnboardingTemplates.Add(t);
            await db.SaveChangesAsync();
            return t.Id;
        });
        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(1); // Maya was added before the rule existed
        var rule = (await W(s => s.SaveAsync(null, new WorkflowRuleInput
        {
            Name = "Auto onboarding", Trigger = WorkflowTrigger.EmployeeAdded,
            Actions = [new() { Use = true, Type = WorkflowActionType.StartOnboarding, TargetId = template }]
        }))).Value;
        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(1);
        var ravi = await HireAsync("Ravi", _maya, null, new DateOnly(2026, 7, 1));
        Assert.Equal(1, await W(s => s.RunAsync(rule)));
        var run = Assert.Single(await W(s => s.RunsAsync(rule)));
        Assert.True(run.Succeeded, run.Detail);
        Assert.True(await _host.RunAsync(sp => S<AppDbContext>(sp).OnboardingPlans.IgnoreQueryFilters().AnyAsync(p => p.EmployeeId == ravi)));
    }

    // ------------------------------------------------------------------ report builder

    [Fact]
    public async Task Employee_report_filters_groups_sorts_and_saves()
    {
        await HireAsync("Ravi", _maya, _sales, new DateOnly(2025, 6, 1));
        await HireAsync("Sita", _maya, _sales, new DateOnly(2025, 9, 1), EmploymentType.Contract);
        await HireAsync("Ola", null, null, new DateOnly(2026, 1, 1));

        var all = await B(s => s.RunAsync(ReportSource.Employees, new ReportDefinition { Columns = ["name", "department", "joined", "tenure", "nonsense"], SortBy = "joined", Descending = true }, false));
        Assert.True(all.Succeeded, all.Error);
        Assert.Equal(4, all.Value!.Columns.Count); // unknown column dropped
        Assert.Equal(4, all.Value.Rows.Count);
        Assert.Equal("Ola W", all.Value.Rows[0][0]); // newest first

        var sales = await B(s => s.RunAsync(ReportSource.Employees, new ReportDefinition { Columns = ["name"], DepartmentId = _sales, EmploymentType = EmploymentType.FullTime }, false));
        Assert.Equal(["Ravi W"], sales.Value!.Rows.Select(r => r[0]));

        var byDept = await B(s => s.RunAsync(ReportSource.Employees, new ReportDefinition { Columns = ["department", "headcount"], GroupBy = "department", SortBy = "headcount", Descending = true }, false));
        Assert.Equal([2m, 2m], byDept.Value!.Rows.Single(r => (string?)r[0] == "Sales").Skip(1));
        Assert.Equal(2, byDept.Value.Rows.Count); // Sales and "—" (no department)
        Assert.Equal(4m, byDept.Value.Totals["headcount"]);

        Assert.False((await B(s => s.RunAsync(ReportSource.Employees, new ReportDefinition { Columns = [] }, false))).Succeeded);
        Assert.False((await B(s => s.RunAsync(ReportSource.Payroll, new ReportDefinition { Columns = ["net"] }, false))).Succeeded); // no payroll access
        Assert.True((await B(s => s.RunAsync(ReportSource.Payroll, new ReportDefinition { Columns = ["net"] }, true))).Succeeded);

        // Saved reports: owner edits; shared ones are visible to others but read-only.
        var id = (await B(s => s.SaveAsync(null, _adminUser, new SavedReportInput { Name = "Headcount by department", Source = ReportSource.Employees, IsShared = true, Definition = new ReportDefinition { Columns = ["department", "headcount", "bogus"], GroupBy = "department" } }))).Value;
        var mayaUser = await UserOf(_maya);
        var seen = await B(s => s.GetAsync(id, mayaUser));
        Assert.NotNull(seen);
        Assert.False(seen!.Value.IsMine);
        Assert.Equal(["department", "headcount"], seen.Value.Input.Definition.Columns);
        Assert.False((await B(s => s.SaveAsync(id, mayaUser, seen.Value.Input))).Succeeded);
        Assert.False((await B(s => s.DeleteAsync(id, mayaUser))).Succeeded);
        Assert.Single(await B(s => s.SavedAsync(mayaUser)));
        Assert.NotNull(await B(s => s.ExportAsync(byDept.Value, "Headcount")));
    }
}
