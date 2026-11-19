using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Benefits;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class WorkflowActionInput
{
    public WorkflowActionType Type { get; set; } = WorkflowActionType.Notify;
    public WorkflowRecipient Recipient { get; set; } = WorkflowRecipient.Manager;
    public int? UserId { get; set; }
    [StringLength(200)] public string? Title { get; set; }
    [StringLength(1000)] public string? Message { get; set; }
    [Range(0, 365)] public int DueInDays { get; set; }
    public int? TargetId { get; set; }
    /// <summary>Blank rows in the form are ignored.</summary>
    public bool Use { get; set; }
}

public sealed class WorkflowRuleInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Display(Name = "When")] public WorkflowTrigger Trigger { get; set; } = WorkflowTrigger.EmployeeAdded;
    [Range(0, 90), Display(Name = "Days before")] public int DaysOffset { get; set; }
    [Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    [Display(Name = "Employment type")] public EmploymentType? EmploymentType { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    public List<WorkflowActionInput> Actions { get; set; } = [];
}

public sealed record WorkflowRuleRow(int Id, string Name, WorkflowTrigger Trigger, int DaysOffset, bool IsActive, int Actions, int Runs, DateTime? LastRunAtUtc);
public sealed record WorkflowRunRow(int Id, string EmployeeName, DateTime RanAtUtc, bool Succeeded, string? Detail);
public sealed record WorkTaskRow(int Id, string Title, string? Details, DateOnly? DueOn, bool IsDone, int? EmployeeId, string? EmployeeName, DateTime CreatedAtUtc);

public sealed record WorkflowChoices(IReadOnlyList<LookupItem> Users, IReadOnlyList<LookupItem> Templates, IReadOnlyList<LookupItem> BenefitPlans);

public interface IWorkflowService
{
    /// <summary>Options for the rule form: active users, onboarding templates without buddy tasks, active benefit plans.</summary>
    Task<WorkflowChoices> ChoicesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowRuleRow>> RulesAsync(CancellationToken ct = default);
    Task<WorkflowRuleInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, WorkflowRuleInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowRunRow>> RunsAsync(int ruleId, CancellationToken ct = default);
    /// <summary>Evaluates active rules (or one rule) and runs their actions once per subject. Returns the number of firings.</summary>
    Task<int> RunAsync(int? ruleId = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTaskRow>> TasksAsync(int userId, bool includeDone, CancellationToken ct = default);
    Task<Result> ToggleTaskAsync(int taskId, int userId, CancellationToken ct = default);
}

public sealed class WorkflowService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications,
    IOnboardingService onboarding, IBenefitService benefits) : IWorkflowService
{
    public const int MaxActions = 10;
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    public async Task<WorkflowChoices> ChoicesAsync(CancellationToken ct = default) => new(
        await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName).Select(u => new LookupItem(u.Id, u.FullName + " (" + u.Email + ")")).ToListAsync(ct),
        await db.OnboardingTemplates.AsNoTracking().Where(t => t.IsActive && !t.Tasks.Any(x => x.Assignee == Domain.Strength.OnboardingAssignee.Buddy && !x.IsDeleted))
            .OrderBy(t => t.Name).Select(t => new LookupItem(t.Id, t.Name)).ToListAsync(ct),
        await db.BenefitPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).Select(p => new LookupItem(p.Id, p.Name)).ToListAsync(ct));

    public static bool UsesDays(WorkflowTrigger t) => t is WorkflowTrigger.BeforeJoining or WorkflowTrigger.BeforeProbationEnds or WorkflowTrigger.BeforeLastDay;

    public async Task<IReadOnlyList<WorkflowRuleRow>> RulesAsync(CancellationToken ct = default)
    {
        var runs = await db.WorkflowRuns.AsNoTracking().GroupBy(r => r.WorkflowRuleId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        return (await db.WorkflowRules.AsNoTracking().Include(r => r.Actions).OrderBy(r => r.Name).ToListAsync(ct))
            .Select(r => new WorkflowRuleRow(r.Id, r.Name, r.Trigger, r.DaysOffset, r.IsActive, r.Actions.Count(a => !a.IsDeleted), runs.GetValueOrDefault(r.Id), r.LastRunAtUtc)).ToList();
    }

    public async Task<WorkflowRuleInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var r = await db.WorkflowRules.AsNoTracking().Include(x => x.Actions).FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? null : new WorkflowRuleInput
        {
            Name = r.Name, Trigger = r.Trigger, DaysOffset = r.DaysOffset, DepartmentId = r.DepartmentId, BranchId = r.BranchId, EmploymentType = r.EmploymentType, IsActive = r.IsActive,
            Actions = r.Actions.Where(a => !a.IsDeleted).OrderBy(a => a.SortOrder).Select(a => new WorkflowActionInput
            {
                Use = true, Type = a.Type, Recipient = a.Recipient, UserId = a.UserId, Title = a.Title, Message = a.Message, DueInDays = a.DueInDays, TargetId = a.TargetId
            }).ToList()
        };
    }

    public async Task<Result<int>> SaveAsync(int? id, WorkflowRuleInput i, CancellationToken ct = default)
    {
        var actions = i.Actions.Where(a => a.Use).ToList();
        if (actions.Count == 0) return Result<int>.Fail("Add at least one action.");
        if (actions.Count > MaxActions) return Result<int>.Fail($"A rule can have at most {MaxActions} actions.");
        foreach (var a in actions)
        {
            if (a.Type is WorkflowActionType.Notify or WorkflowActionType.CreateTask && string.IsNullOrWhiteSpace(a.Title)) return Result<int>.Fail("Give each notification and task a title.");
            if (a.Type is WorkflowActionType.Notify or WorkflowActionType.CreateTask && a.Recipient == WorkflowRecipient.SpecificUser
                && (a.UserId is not int u || !await db.Users.AnyAsync(x => x.Id == u && x.IsActive, ct))) return Result<int>.Fail("Choose the user for 'specific person'.");
            if (a.Type == WorkflowActionType.StartOnboarding && (a.TargetId is not int t || !await db.OnboardingTemplates.AnyAsync(x => x.Id == t && x.IsActive, ct)))
                return Result<int>.Fail("Choose an active onboarding template.");
            // A buddy is chosen per person, which an automatic rule can't do.
            if (a.Type == WorkflowActionType.StartOnboarding && await db.OnboardingTemplateTasks.AnyAsync(x => x.TemplateId == a.TargetId && x.Assignee == Domain.Strength.OnboardingAssignee.Buddy, ct))
                return Result<int>.Fail("That onboarding template has buddy tasks, and a buddy has to be chosen for each person. Use a template without buddy tasks, or start onboarding by hand.");
            if (a.Type == WorkflowActionType.EnrolBenefit && (a.TargetId is not int b || !await db.BenefitPlans.AnyAsync(x => x.Id == b && x.IsActive, ct)))
                return Result<int>.Fail("Choose an active benefit plan.");
        }
        if ((i.Trigger == WorkflowTrigger.PromotionApplied || i.Trigger == WorkflowTrigger.EmployeeAdded) && i.DaysOffset != 0) i.DaysOffset = 0;

        WorkflowRule r;
        if (id is int existing)
        {
            var found = await db.WorkflowRules.Include(x => x.Actions).FirstOrDefaultAsync(x => x.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Rule not found.");
            r = found;
            db.WorkflowActions.RemoveRange(r.Actions);
            r.Actions.Clear();
        }
        else
        {
            r = new WorkflowRule();
            db.WorkflowRules.Add(r);
        }
        r.Name = i.Name.Trim();
        r.Trigger = i.Trigger;
        r.DaysOffset = i.DaysOffset;
        r.DepartmentId = i.DepartmentId;
        r.BranchId = i.BranchId;
        r.EmploymentType = i.EmploymentType;
        r.IsActive = i.IsActive;
        var order = 0;
        foreach (var a in actions)
            r.Actions.Add(new WorkflowAction
            {
                Type = a.Type, Recipient = a.Recipient, UserId = a.Recipient == WorkflowRecipient.SpecificUser ? a.UserId : null,
                Title = a.Title?.Trim(), Message = string.IsNullOrWhiteSpace(a.Message) ? null : a.Message.Trim(), DueInDays = a.DueInDays,
                TargetId = a.Type is WorkflowActionType.StartOnboarding or WorkflowActionType.EnrolBenefit ? a.TargetId : null, SortOrder = order += 10
            });
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(r.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var r = await db.WorkflowRules.Include(x => x.Actions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Rule not found.");
        db.WorkflowActions.RemoveRange(r.Actions);
        db.WorkflowRules.Remove(r);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<WorkflowRunRow>> RunsAsync(int ruleId, CancellationToken ct = default) =>
        (await (from r in db.WorkflowRuns.AsNoTracking().Where(r => r.WorkflowRuleId == ruleId)
                join e in db.Employees on r.EmployeeId equals e.Id
                orderby r.Id descending
                select new WorkflowRunRow(r.Id, e.FirstName + " " + e.LastName, r.RanAtUtc, r.Succeeded, r.Detail)).Take(100).ToListAsync(ct));

    private sealed record Subject(string Key, Employee Employee, DateOnly? Date);

    private async Task<List<Subject>> SubjectsAsync(WorkflowRule r, DateOnly today, CancellationToken ct)
    {
        var q = db.Employees.AsNoTracking().Include(e => e.Department).Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (r.DepartmentId is int d) q = q.Where(e => e.DepartmentId == d);
        if (r.BranchId is int b) q = q.Where(e => e.BranchId == b);
        if (r.EmploymentType is { } t) q = q.Where(e => e.EmploymentType == t);
        var until = today.AddDays(r.DaysOffset);
        switch (r.Trigger)
        {
            case WorkflowTrigger.EmployeeAdded:
                return (await q.Where(e => e.CreatedAtUtc >= r.CreatedAtUtc).ToListAsync(ct)).Select(e => new Subject($"emp:{e.Id}", e, e.DateOfJoining)).ToList();
            case WorkflowTrigger.BeforeJoining:
                return (await q.Where(e => e.DateOfJoining >= today && e.DateOfJoining <= until).ToListAsync(ct)).Select(e => new Subject($"join:{e.Id}:{e.DateOfJoining:yyyyMMdd}", e, e.DateOfJoining)).ToList();
            case WorkflowTrigger.BeforeProbationEnds:
                return (await q.Where(e => e.Status == EmployeeStatus.Probation && e.ProbationEndDate >= today && e.ProbationEndDate <= until).ToListAsync(ct))
                    .Select(e => new Subject($"prob:{e.Id}:{e.ProbationEndDate:yyyyMMdd}", e, e.ProbationEndDate)).ToList();
            case WorkflowTrigger.BeforeLastDay:
                return (await q.Where(e => e.DateOfExit >= today && e.DateOfExit <= until).ToListAsync(ct)).Select(e => new Subject($"exit:{e.Id}:{e.DateOfExit:yyyyMMdd}", e, e.DateOfExit)).ToList();
            case WorkflowTrigger.PromotionApplied:
                var promos = await db.PromotionCases.AsNoTracking().Where(p => p.Status == PromotionStatus.Applied && p.AppliedAtUtc >= r.CreatedAtUtc).Select(p => new { p.Id, p.EmployeeId, p.EffectiveOn }).ToListAsync(ct);
                var ids = promos.Select(p => p.EmployeeId).ToList();
                var people = await q.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
                return promos.Where(p => people.ContainsKey(p.EmployeeId)).Select(p => new Subject($"promo:{p.Id}", people[p.EmployeeId], p.EffectiveOn)).ToList();
            default:
                return [];
        }
    }

    public async Task<int> RunAsync(int? ruleId = null, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var rules = await db.WorkflowRules.Include(r => r.Actions).Where(r => r.IsActive && (ruleId == null || r.Id == ruleId)).ToListAsync(ct);
        var fired = 0;
        foreach (var rule in rules)
        {
            var done = (await db.WorkflowRuns.AsNoTracking().Where(x => x.WorkflowRuleId == rule.Id).Select(x => x.SubjectKey).ToListAsync(ct)).ToHashSet();
            foreach (var s in (await SubjectsAsync(rule, today, ct)).Where(s => !done.Contains(s.Key)))
            {
                var problems = new List<string>();
                foreach (var a in rule.Actions.Where(a => !a.IsDeleted).OrderBy(a => a.SortOrder))
                {
                    try { if (await ExecuteAsync(rule, a, s, today, ct) is { } problem) problems.Add(problem); }
                    catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException) { problems.Add($"{a.Type}: {ex.Message}"); }
                }
                db.WorkflowRuns.Add(new WorkflowRun
                {
                    WorkflowRuleId = rule.Id, SubjectKey = s.Key, EmployeeId = s.Employee.Id, RanAtUtc = clock.UtcNow, Succeeded = problems.Count == 0,
                    Detail = problems.Count == 0 ? null : string.Join(" ", problems) is { Length: > 1000 } long_ ? long_[..1000] : string.Join(" ", problems)
                });
                fired++;
                await db.SaveChangesAsync(ct);
            }
            rule.LastRunAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return fired;
    }

    private async Task<List<int>> RecipientsAsync(WorkflowAction a, Employee e, CancellationToken ct)
    {
        switch (a.Recipient)
        {
            case WorkflowRecipient.Employee: return e.UserId is int u ? [u] : [];
            case WorkflowRecipient.Manager:
                return e.ReportingManagerId is int m && await db.Employees.Where(x => x.Id == m).Select(x => x.UserId).FirstOrDefaultAsync(ct) is int mu ? [mu] : [];
            case WorkflowRecipient.HrTeam: return (await notifications.UsersWithPermissionAsync(TenantPermissions.EmployeesManage, ct)).ToList();
            default: return a.UserId is int id && await db.Users.AnyAsync(x => x.Id == id && x.IsActive, ct) ? [id] : [];
        }
    }

    private static string Fill(string? text, Subject s, string? manager) => (text ?? "")
        .Replace("{Employee}", s.Employee.FullName).Replace("{Date}", s.Date?.ToString("dd MMM yyyy") ?? "")
        .Replace("{Department}", s.Employee.Department?.Name ?? "no department").Replace("{Manager}", manager ?? "no manager");

    /// <returns>A problem description, or null when the action succeeded.</returns>
    private async Task<string?> ExecuteAsync(WorkflowRule rule, WorkflowAction a, Subject s, DateOnly today, CancellationToken ct)
    {
        var e = s.Employee;
        switch (a.Type)
        {
            case WorkflowActionType.StartOnboarding:
                var o = await onboarding.StartAsync(e.Id, a.TargetId!.Value, null, ct);
                return o.Succeeded ? null : $"Onboarding: {o.Error}";
            case WorkflowActionType.EnrolBenefit:
                var b = await benefits.EnrollAsync(a.TargetId!.Value, e.Id, today, ct);
                return b.Succeeded ? null : $"Benefit: {b.Error}";
        }
        var users = await RecipientsAsync(a, e, ct);
        if (users.Count == 0) return $"{a.Type}: nobody to send it to ({a.Recipient}).";
        var manager = e.ReportingManagerId is int m ? await db.Employees.Where(x => x.Id == m).Select(x => x.FirstName + " " + x.LastName).FirstOrDefaultAsync(ct) : null;
        var title = Fill(a.Title, s, manager);
        var message = Fill(a.Message, s, manager);
        if (a.Type == WorkflowActionType.Notify)
        {
            notifications.Queue(users, title.Length > 150 ? title[..150] : title, string.IsNullOrEmpty(message) ? rule.Name : message,
                a.Recipient == WorkflowRecipient.Employee ? "/me" : $"/org/employees/details/{e.Id}");
        }
        else
        {
            foreach (var u in users)
                db.WorkTasks.Add(new WorkTask { AssigneeUserId = u, EmployeeId = e.Id, Title = title.Length > 200 ? title[..200] : title, Details = string.IsNullOrEmpty(message) ? null : message, DueOn = today.AddDays(a.DueInDays), WorkflowRuleId = rule.Id });
            notifications.Queue(users, "New task", title, "/me/tasks");
        }
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<IReadOnlyList<WorkTaskRow>> TasksAsync(int userId, bool includeDone, CancellationToken ct = default) =>
        (await (from t in db.WorkTasks.AsNoTracking().Where(t => t.AssigneeUserId == userId && (includeDone || !t.IsDone))
                join e in db.Employees on t.EmployeeId equals e.Id into es
                from emp in es.DefaultIfEmpty()
                select new WorkTaskRow(t.Id, t.Title, t.Details, t.DueOn, t.IsDone, t.EmployeeId, emp == null ? null : emp.FirstName + " " + emp.LastName, t.CreatedAtUtc))
            .ToListAsync(ct)).OrderBy(t => t.IsDone).ThenBy(t => t.DueOn ?? DateOnly.MaxValue).Take(200).ToList();

    public async Task<Result> ToggleTaskAsync(int taskId, int userId, CancellationToken ct = default)
    {
        var t = await db.WorkTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.AssigneeUserId == userId, ct);
        if (t is null) return Result.Fail("Task not found.");
        t.IsDone = !t.IsDone;
        t.DoneAtUtc = t.IsDone ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
