using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

public sealed class OnboardingTaskInput
{
    [StringLength(200)] public string? Title { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public OnboardingAssignee Assignee { get; set; } = OnboardingAssignee.Hr;
    [Range(-60, 180)] public int DueOffsetDays { get; set; }
}

public sealed class OnboardingTemplateInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    public List<OnboardingTaskInput> Tasks { get; set; } = [];
}

public sealed record OnboardingTemplateRow(int Id, string Name, string? Description, bool IsActive, int TaskCount);
public sealed record OnboardingCandidate(int EmployeeId, string Name, string Code, DateOnly DateOfJoining, string? Department);
public sealed record OnboardingPlanRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, DateOnly DateOfJoining, string TemplateName,
    OnboardingStatus Status, int Total, int Done, int Overdue);
public sealed record OnboardingTaskRow(int Id, string Title, string? Description, OnboardingAssignee Assignee, DateOnly DueOn, DateTime? CompletedAtUtc,
    string? CompletedBy, string? Note, bool CanComplete);
public sealed record OnboardingPlanView(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, DateOnly DateOfJoining, string TemplateName,
    OnboardingStatus Status, string? Manager, string? Buddy, IReadOnlyList<OnboardingTaskRow> Tasks);
public sealed record MyOnboardingTask(int TaskId, int PlanId, string For, bool ForMe, string Title, string? Description, OnboardingAssignee Assignee,
    DateOnly DueOn, bool Overdue);

public interface IOnboardingService
{
    Task<IReadOnlyList<OnboardingTemplateRow>> TemplatesAsync(CancellationToken ct = default);
    Task<OnboardingTemplateInput?> GetTemplateAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveTemplateAsync(int? id, OnboardingTemplateInput input, CancellationToken ct = default);
    Task<Result> DeleteTemplateAsync(int id, CancellationToken ct = default);

    /// <summary>Current employees who joined in the last 90 days (or will join) and have no live onboarding.</summary>
    Task<IReadOnlyList<OnboardingCandidate>> CandidatesAsync(CancellationToken ct = default);
    Task<Result<int>> StartAsync(int employeeId, int templateId, int? buddyEmployeeId, CancellationToken ct = default);
    Task<IReadOnlyList<OnboardingPlanRow>> PlansAsync(bool activeOnly, CancellationToken ct = default);
    /// <param name="asHr">HR (onboarding permission) may complete any task; others only tasks assigned to them.</param>
    Task<OnboardingPlanView?> PlanAsync(int id, bool asHr, CancellationToken ct = default);
    Task<Result> CompleteTaskAsync(int taskId, string? note, bool asHr, CancellationToken ct = default);
    Task<Result> ReopenTaskAsync(int taskId, CancellationToken ct = default);
    Task<Result> AddTaskAsync(int planId, OnboardingTaskInput input, DateOnly dueOn, CancellationToken ct = default);
    Task<Result> CancelPlanAsync(int planId, CancellationToken ct = default);
    /// <summary>Open tasks the signed-in user must do: their own as a new joiner, as manager, or as buddy.</summary>
    Task<IReadOnlyList<MyOnboardingTask>> MyTasksAsync(CancellationToken ct = default);
}

public sealed class OnboardingService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IOnboardingService
{
    private const int MaxTasks = 40;

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private static IQueryable<Employee> Current(IQueryable<Employee> q) =>
        q.Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);

    // ------------------------------------------------------------------ templates

    public async Task<IReadOnlyList<OnboardingTemplateRow>> TemplatesAsync(CancellationToken ct = default) =>
        await db.OnboardingTemplates.AsNoTracking().OrderByDescending(t => t.IsActive).ThenBy(t => t.Name)
            .Select(t => new OnboardingTemplateRow(t.Id, t.Name, t.Description, t.IsActive, t.Tasks.Count)).ToListAsync(ct);

    public async Task<OnboardingTemplateInput?> GetTemplateAsync(int id, CancellationToken ct = default)
    {
        var t = await db.OnboardingTemplates.AsNoTracking().Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : new OnboardingTemplateInput
        {
            Name = t.Name, Description = t.Description, IsActive = t.IsActive,
            Tasks = t.Tasks.OrderBy(x => x.SortOrder).Select(x => new OnboardingTaskInput { Title = x.Title, Description = x.Description, Assignee = x.Assignee, DueOffsetDays = x.DueOffsetDays }).ToList()
        };
    }

    public async Task<Result<int>> SaveTemplateAsync(int? id, OnboardingTemplateInput i, CancellationToken ct = default)
    {
        var tasks = i.Tasks.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
        if (tasks.Count == 0) return Result<int>.Fail("Add at least one task.");
        if (tasks.Count > MaxTasks) return Result<int>.Fail($"A template can have at most {MaxTasks} tasks.");
        if (tasks.Any(x => x.DueOffsetDays is < -60 or > 180)) return Result<int>.Fail("Due days must be between 60 days before and 180 days after joining.");
        var name = i.Name.Trim();
        if (await db.OnboardingTemplates.AnyAsync(t => t.Name == name && t.Id != id, ct)) return Result<int>.Fail("A template with this name already exists.");

        OnboardingTemplate? template;
        if (id is null) template = db.OnboardingTemplates.Add(new OnboardingTemplate()).Entity;
        else
        {
            template = await db.OnboardingTemplates.Include(t => t.Tasks).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template is null) return Result<int>.Fail("Template not found.");
            db.OnboardingTemplateTasks.RemoveRange(template.Tasks);
            template.Tasks.Clear();
        }
        template.Name = name;
        template.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
        template.IsActive = i.IsActive;
        foreach (var (x, n) in tasks.Select((x, n) => (x, n + 1)))
            template.Tasks.Add(new OnboardingTemplateTask { Title = x.Title!.Trim(), Description = string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(), Assignee = x.Assignee, DueOffsetDays = x.DueOffsetDays, SortOrder = n });
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(template.Id);
    }

    public async Task<Result> DeleteTemplateAsync(int id, CancellationToken ct = default)
    {
        var t = await db.OnboardingTemplates.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Template not found.");
        db.OnboardingTemplateTasks.RemoveRange(t.Tasks);
        db.OnboardingTemplates.Remove(t);
        await db.SaveChangesAsync(ct);
        return Result.Ok(); // plans already started keep their own copy of the tasks
    }

    // ------------------------------------------------------------------ plans

    private IQueryable<int> LivePlanEmployees => db.OnboardingPlans.Where(p => p.Status != OnboardingStatus.Cancelled).Select(p => p.EmployeeId);

    public async Task<IReadOnlyList<OnboardingCandidate>> CandidatesAsync(CancellationToken ct = default)
    {
        var since = (await TodayAsync(ct)).AddDays(-90);
        return await Current(db.Employees.AsNoTracking()).Where(e => e.DateOfJoining >= since && !LivePlanEmployees.Contains(e.Id))
            .OrderBy(e => e.DateOfJoining)
            .Select(e => new OnboardingCandidate(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode, e.DateOfJoining,
                db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<Result<int>> StartAsync(int employeeId, int templateId, int? buddyEmployeeId, CancellationToken ct = default)
    {
        var emp = await Current(db.Employees.AsNoTracking()).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null) return Result<int>.Fail("Choose a current employee.");
        if (await LivePlanEmployees.AnyAsync(x => x == employeeId, ct)) return Result<int>.Fail("This employee already has an onboarding plan.");
        var template = await db.OnboardingTemplates.AsNoTracking().Include(t => t.Tasks).FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive, ct);
        if (template is null) return Result<int>.Fail("Choose an active template.");
        Employee? buddy = null;
        if (buddyEmployeeId is int b)
        {
            if (b == employeeId) return Result<int>.Fail("A new joiner can't be their own buddy.");
            buddy = await Current(db.Employees.AsNoTracking()).FirstOrDefaultAsync(e => e.Id == b, ct);
            if (buddy is null) return Result<int>.Fail("Choose a current employee as buddy.");
        }
        if (template.Tasks.Any(t => t.Assignee == OnboardingAssignee.Buddy) && buddy is null)
            return Result<int>.Fail("This template has buddy tasks — choose a buddy.");

        var plan = new OnboardingPlan
        {
            EmployeeId = employeeId, TemplateName = template.Name, StartedOn = await TodayAsync(ct), BuddyEmployeeId = buddy?.Id,
            Tasks = template.Tasks.OrderBy(t => t.SortOrder).Select(t => new OnboardingTask
            {
                Title = t.Title, Description = t.Description, Assignee = t.Assignee, DueOn = emp.DateOfJoining.AddDays(t.DueOffsetDays), SortOrder = t.SortOrder
            }).ToList()
        };
        db.OnboardingPlans.Add(plan);

        var name = $"{emp.FirstName} {emp.LastName}";
        var manager = emp.ReportingManagerId is int m ? await db.Employees.Where(e => e.Id == m).Select(e => e.UserId).FirstOrDefaultAsync(ct) : null;
        if (plan.Tasks.Any(t => t.Assignee == OnboardingAssignee.Manager) && manager is int mu)
            notifications.Queue([mu], "Onboarding tasks", $"You have onboarding tasks for {name}, joining {emp.DateOfJoining:dd MMM}.", "/me/onboarding");
        if (buddy?.UserId is int bu)
            notifications.Queue([bu], "You're an onboarding buddy", $"You've been chosen as {name}'s buddy. See your tasks.", "/me/onboarding");
        if (plan.Tasks.Any(t => t.Assignee == OnboardingAssignee.Employee) && emp.UserId is int eu)
            notifications.Queue([eu], "Welcome aboard", "Your onboarding checklist is ready.", "/me/onboarding");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(plan.Id);
    }

    public async Task<IReadOnlyList<OnboardingPlanRow>> PlansAsync(bool activeOnly, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        return await db.OnboardingPlans.AsNoTracking()
            .Where(p => activeOnly ? p.Status == OnboardingStatus.InProgress : p.Status != OnboardingStatus.Cancelled)
            .Join(db.Employees, p => p.EmployeeId, e => e.Id, (p, e) => new { p, e })
            .OrderBy(x => x.e.DateOfJoining)
            .Select(x => new OnboardingPlanRow(x.p.Id, x.e.Id, x.e.FirstName + " " + x.e.LastName, x.e.EmployeeCode, x.e.DateOfJoining, x.p.TemplateName, x.p.Status,
                x.p.Tasks.Count, x.p.Tasks.Count(t => t.CompletedAtUtc != null), x.p.Tasks.Count(t => t.CompletedAtUtc == null && t.DueOn < today)))
            .Take(300).ToListAsync(ct);
    }

    /// <summary>Which assignee roles the signed-in user holds for a plan.</summary>
    private async Task<HashSet<OnboardingAssignee>> MyRolesAsync(OnboardingPlan plan, CancellationToken ct)
    {
        var me = context.UserId;
        var roles = new HashSet<OnboardingAssignee>();
        if (me is null) return roles;
        var emp = await db.Employees.AsNoTracking().Where(e => e.Id == plan.EmployeeId).Select(e => new { e.UserId, e.ReportingManagerId }).FirstAsync(ct);
        if (emp.UserId == me) roles.Add(OnboardingAssignee.Employee);
        if (emp.ReportingManagerId is int m && await db.Employees.AnyAsync(e => e.Id == m && e.UserId == me, ct)) roles.Add(OnboardingAssignee.Manager);
        if (plan.BuddyEmployeeId is int b && await db.Employees.AnyAsync(e => e.Id == b && e.UserId == me, ct)) roles.Add(OnboardingAssignee.Buddy);
        return roles;
    }

    public async Task<OnboardingPlanView?> PlanAsync(int id, bool asHr, CancellationToken ct = default)
    {
        var plan = await db.OnboardingPlans.AsNoTracking().Include(p => p.Tasks).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return null;
        var roles = await MyRolesAsync(plan, ct);
        if (!asHr && roles.Count == 0) return null;
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == plan.EmployeeId, ct);
        async Task<string?> NameOf(int? employeeId) => employeeId is null ? null
            : await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct);
        var live = plan.Status == OnboardingStatus.InProgress;
        return new OnboardingPlanView(plan.Id, emp.Id, emp.FullName, emp.EmployeeCode, emp.DateOfJoining, plan.TemplateName, plan.Status,
            await NameOf(emp.ReportingManagerId), await NameOf(plan.BuddyEmployeeId),
            plan.Tasks.OrderBy(t => t.DueOn).ThenBy(t => t.SortOrder)
                .Select(t => new OnboardingTaskRow(t.Id, t.Title, t.Description, t.Assignee, t.DueOn, t.CompletedAtUtc, t.CompletedBy, t.Note,
                    live && t.CompletedAtUtc is null && (asHr || roles.Contains(t.Assignee)))).ToList());
    }

    public async Task<Result> CompleteTaskAsync(int taskId, string? note, bool asHr, CancellationToken ct = default)
    {
        var task = await db.OnboardingTasks.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return Result.Fail("Task not found.");
        if (task.Plan.Status != OnboardingStatus.InProgress) return Result.Fail("This onboarding is no longer in progress.");
        if (task.CompletedAtUtc is not null) return Result.Fail("Already done.");
        if (!asHr && !(await MyRolesAsync(task.Plan, ct)).Contains(task.Assignee)) return Result.Fail("This task isn't assigned to you.");
        if (note is { Length: > 500 }) return Result.Fail("Notes can be at most 500 characters.");
        task.CompletedAtUtc = clock.UtcNow;
        task.CompletedBy = await MyNameAsync(ct);
        task.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var open = await db.OnboardingTasks.CountAsync(t => t.PlanId == task.PlanId && t.CompletedAtUtc == null && t.Id != task.Id, ct);
        if (open == 0)
        {
            task.Plan.Status = OnboardingStatus.Completed;
            task.Plan.CompletedAtUtc = clock.UtcNow;
            var name = await db.Employees.Where(e => e.Id == task.Plan.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
            notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.OnboardingManage, ct), "Onboarding complete",
                $"All onboarding tasks for {name} are done.", $"/org/onboarding/{task.PlanId}");
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReopenTaskAsync(int taskId, CancellationToken ct = default)
    {
        var task = await db.OnboardingTasks.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return Result.Fail("Task not found.");
        if (task.Plan.Status == OnboardingStatus.Cancelled) return Result.Fail("This onboarding was cancelled.");
        if (task.CompletedAtUtc is null) return Result.Fail("The task is still open.");
        task.CompletedAtUtc = null;
        task.CompletedBy = null;
        task.Plan.Status = OnboardingStatus.InProgress;
        task.Plan.CompletedAtUtc = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddTaskAsync(int planId, OnboardingTaskInput i, DateOnly dueOn, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(i.Title)) return Result.Fail("Give the task a title.");
        var plan = await db.OnboardingPlans.Include(p => p.Tasks).FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result.Fail("Onboarding not found.");
        if (plan.Status == OnboardingStatus.Cancelled) return Result.Fail("This onboarding was cancelled.");
        if (plan.Tasks.Count >= MaxTasks + 20) return Result.Fail("This onboarding already has too many tasks.");
        if (i.Assignee == OnboardingAssignee.Buddy && plan.BuddyEmployeeId is null) return Result.Fail("This onboarding has no buddy.");
        plan.Tasks.Add(new OnboardingTask
        {
            Title = i.Title.Trim(), Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), Assignee = i.Assignee,
            DueOn = dueOn, SortOrder = plan.Tasks.Select(t => t.SortOrder).DefaultIfEmpty(0).Max() + 1
        });
        plan.Status = OnboardingStatus.InProgress;
        plan.CompletedAtUtc = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CancelPlanAsync(int planId, CancellationToken ct = default)
    {
        var plan = await db.OnboardingPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result.Fail("Onboarding not found.");
        if (plan.Status != OnboardingStatus.InProgress) return Result.Fail("Only onboarding in progress can be cancelled.");
        plan.Status = OnboardingStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<MyOnboardingTask>> MyTasksAsync(CancellationToken ct = default)
    {
        var me = context.UserId;
        if (me is null) return [];
        var today = await TodayAsync(ct);
        var myEmployee = await db.Employees.Where(e => e.UserId == me).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        if (myEmployee is null) return [];
        var rows = await db.OnboardingTasks.AsNoTracking()
            .Where(t => t.CompletedAtUtc == null && t.Plan.Status == OnboardingStatus.InProgress)
            .Join(db.Employees, t => t.Plan.EmployeeId, e => e.Id, (t, e) => new { t, e, t.Plan.BuddyEmployeeId })
            .Where(x => (x.t.Assignee == OnboardingAssignee.Employee && x.e.Id == myEmployee)
                     || (x.t.Assignee == OnboardingAssignee.Manager && x.e.ReportingManagerId == myEmployee)
                     || (x.t.Assignee == OnboardingAssignee.Buddy && x.BuddyEmployeeId == myEmployee))
            .OrderBy(x => x.t.DueOn)
            .Select(x => new { x.t.Id, x.t.PlanId, x.e.FirstName, x.e.LastName, Self = x.e.Id == myEmployee, x.t.Title, x.t.Description, x.t.Assignee, x.t.DueOn })
            .Take(200).ToListAsync(ct);
        return rows.Select(r => new MyOnboardingTask(r.Id, r.PlanId, $"{r.FirstName} {r.LastName}", r.Self, r.Title, r.Description, r.Assignee, r.DueOn, r.DueOn < today)).ToList();
    }
}

/// <summary>Starting onboarding template for every organization (general wording; HR edits it).</summary>
public static class OnboardingDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        (string Title, OnboardingAssignee Who, int Days)[] tasks =
        [
            ("Send the offer and appointment letters", OnboardingAssignee.Hr, -7),
            ("Prepare laptop, email account and system access", OnboardingAssignee.Hr, -2),
            ("Share the first-week plan with the new joiner", OnboardingAssignee.Manager, -1),
            ("Upload PAN, Aadhaar (consented) and bank details", OnboardingAssignee.Employee, 0),
            ("Workplace tour and introductions", OnboardingAssignee.Buddy, 0),
            ("Read the HR policies and code of conduct", OnboardingAssignee.Employee, 3),
            ("Set goals for the probation period", OnboardingAssignee.Manager, 7),
            ("Check-in: how is the first week going?", OnboardingAssignee.Buddy, 7),
            ("30-day review with the manager", OnboardingAssignee.Manager, 30),
        ];
        db.OnboardingTemplates.Add(new OnboardingTemplate
        {
            OrganizationId = organizationId, Name = "Standard onboarding", Description = "General checklist for new joiners. Edit it to match your process.",
            Tasks = tasks.Select((t, n) => new OnboardingTemplateTask { OrganizationId = organizationId, Title = t.Title, Assignee = t.Who, DueOffsetDays = t.Days, SortOrder = n + 1 }).ToList()
        });
    }
}
