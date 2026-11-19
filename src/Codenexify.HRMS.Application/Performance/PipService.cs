using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class PipObjectiveInput
{
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(1000), Display(Name = "How success is measured")] public string? Measure { get; set; }
}

public sealed class PipInput
{
    [Display(Name = "Employee")] public int EmployeeId { get; set; }
    [Display(Name = "Starts")] public DateOnly StartsOn { get; set; }
    [Display(Name = "Ends")] public DateOnly EndsOn { get; set; }
    [Required, StringLength(4000, MinimumLength = 30), Display(Name = "Performance concerns (facts and examples)")] public string Concerns { get; set; } = string.Empty;
    [StringLength(2000), Display(Name = "Support the company will give")] public string? Support { get; set; }
    public List<PipObjectiveInput> Objectives { get; set; } = [];
}

public sealed record PipRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string? ManagerName, DateOnly StartsOn, DateOnly EndsOn, PipStatus Status, int Objectives, int Met);
public sealed record PipView(ImprovementPlan Plan, string EmployeeName, string? ManagerName, bool IsEmployee, bool CanManage, bool IsHr)
{
    public bool CanEditDraft => Plan.Status == PipStatus.Draft && CanManage;
    public bool CanAcknowledge => IsEmployee && Plan.Status == PipStatus.Active && Plan.AcknowledgedAtUtc is null;
}

public interface IPipService
{
    public const int MinDays = 14;
    public const int MaxDays = 180;
    public const int MaxExtensions = 2;

    Task<Result<int>> CreateAsync(int? me, bool isHr, PipInput input, CancellationToken ct = default);
    Task<Result> SaveDraftAsync(int id, int? me, bool isHr, PipInput input, CancellationToken ct = default);
    Task<PipInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PipRow>> ListAsync(PipStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<PipRow>> ForManagerAsync(int me, CancellationToken ct = default);
    Task<IReadOnlyList<PipRow>> MineAsync(int employeeId, CancellationToken ct = default);
    Task<PipView?> GetAsync(int id, int? me, bool isHr, CancellationToken ct = default);
    Task<Result> StartAsync(int id, CancellationToken ct = default);
    Task<Result> AcknowledgeAsync(int id, int me, string? comment, CancellationToken ct = default);
    Task<Result> CheckInAsync(int id, int? me, bool isHr, PipProgress progress, string note, CancellationToken ct = default);
    Task<Result> SetObjectiveAsync(int objectiveId, int? me, bool isHr, PipObjectiveStatus status, CancellationToken ct = default);
    Task<Result> ExtendAsync(int id, DateOnly newEnd, string reason, CancellationToken ct = default);
    /// <param name="outcome">Passed, NotPassed or Cancelled.</param>
    Task<Result> CloseAsync(int id, int? me, bool isHr, PipStatus outcome, string note, CancellationToken ct = default);
}

public sealed class PipService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications, IAuditWriter audit) : IPipService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private IQueryable<PipRow> Rows(IQueryable<ImprovementPlan> q) =>
        from p in q
        join e in db.Employees on p.EmployeeId equals e.Id
        join m in db.Employees on p.ManagerEmployeeId equals m.Id into ms
        from mg in ms.DefaultIfEmpty()
        select new PipRow(p.Id, e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode, mg == null ? null : mg.FirstName + " " + mg.LastName,
            p.StartsOn, p.EndsOn, p.Status, p.Objectives.Count, p.Objectives.Count(o => o.Status == PipObjectiveStatus.Met));

    private static string? Validate(PipInput i, DateOnly today, bool isNew)
    {
        var days = i.EndsOn.DayNumber - i.StartsOn.DayNumber;
        if (days < IPipService.MinDays || days > IPipService.MaxDays) return $"A plan should run {IPipService.MinDays}–{IPipService.MaxDays} days, giving a fair chance to improve.";
        if (isNew && i.StartsOn < today.AddDays(-7)) return "The plan can't start more than a week in the past.";
        var objectives = i.Objectives.Where(o => !string.IsNullOrWhiteSpace(o.Description)).ToList();
        if (objectives.Count == 0) return "Add at least one objective.";
        if (objectives.Count > 10) return "Keep it to at most 10 objectives.";
        if (objectives.Any(o => string.IsNullOrWhiteSpace(o.Measure))) return "Say how success will be measured for each objective.";
        return null;
    }

    private static void SetObjectives(ImprovementPlan p, PipInput i)
    {
        p.Objectives.Clear();
        var order = 0;
        foreach (var o in i.Objectives.Where(o => !string.IsNullOrWhiteSpace(o.Description)))
            p.Objectives.Add(new PipObjective { Description = o.Description!.Trim(), Measure = o.Measure!.Trim(), SortOrder = order += 10 });
    }

    private async Task<bool> CanManageAsync(int employeeId, int? me, bool isHr, CancellationToken ct) =>
        isHr || (me is int m && m != employeeId && (await ReportingLine.LoadAsync(db, ct)).IsAbove(m, employeeId));

    public async Task<Result<int>> CreateAsync(int? me, bool isHr, PipInput i, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == i.EmployeeId, ct);
        if (emp is not { IsCurrent: true }) return Result<int>.Fail("Choose a current employee.");
        if (!await CanManageAsync(emp.Id, me, isHr, ct)) return Result<int>.Fail("Only the employee's managers or HR can create a plan.");
        if (Validate(i, await TodayAsync(ct), true) is { } bad) return Result<int>.Fail(bad);
        if (await db.ImprovementPlans.AnyAsync(p => p.EmployeeId == emp.Id && (p.Status == PipStatus.Draft || p.Status == PipStatus.Active), ct))
            return Result<int>.Fail("This employee already has an open plan.");
        var plan = new ImprovementPlan
        {
            EmployeeId = emp.Id, ManagerEmployeeId = !isHr && me is int m ? m : emp.ReportingManagerId, CreatedByName = await MyNameAsync(ct),
            StartsOn = i.StartsOn, EndsOn = i.EndsOn, Concerns = i.Concerns.Trim(), Support = string.IsNullOrWhiteSpace(i.Support) ? null : i.Support.Trim()
        };
        SetObjectives(plan, i);
        db.ImprovementPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        if (!isHr)
        {
            var hr = await notifications.UsersWithPermissionAsync(TenantPermissions.PipManage, ct);
            if (hr.Count > 0) notifications.Queue(hr, "Improvement plan to review", $"{plan.CreatedByName} drafted a plan for {emp.FullName}. HR starts it.", $"/me/pips/{plan.Id}");
            await db.SaveChangesAsync(ct);
        }
        return Result<int>.Ok(plan.Id);
    }

    public async Task<Result> SaveDraftAsync(int id, int? me, bool isHr, PipInput i, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.Include(x => x.Objectives).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || p.Status != PipStatus.Draft || !await CanManageAsync(p.EmployeeId, me, isHr, ct)) return Result.Fail("Only a draft plan can be edited.");
        if (Validate(i, await TodayAsync(ct), false) is { } bad) return Result.Fail(bad);
        p.StartsOn = i.StartsOn;
        p.EndsOn = i.EndsOn;
        p.Concerns = i.Concerns.Trim();
        p.Support = string.IsNullOrWhiteSpace(i.Support) ? null : i.Support.Trim();
        db.PipObjectives.RemoveRange(p.Objectives);
        SetObjectives(p, i);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<PipInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.AsNoTracking().Include(x => x.Objectives).FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new PipInput
        {
            EmployeeId = p.EmployeeId, StartsOn = p.StartsOn, EndsOn = p.EndsOn, Concerns = p.Concerns, Support = p.Support,
            Objectives = p.Objectives.Where(o => !o.IsDeleted).OrderBy(o => o.SortOrder).Select(o => new PipObjectiveInput { Description = o.Description, Measure = o.Measure }).ToList()
        };
    }

    public async Task<IReadOnlyList<PipRow>> ListAsync(PipStatus? status, CancellationToken ct = default)
    {
        var q = db.ImprovementPlans.AsNoTracking();
        if (status is { } s) q = q.Where(p => p.Status == s);
        return (await Rows(q).ToListAsync(ct)).OrderBy(r => r.Status).ThenBy(r => r.EndsOn).ToList();
    }

    public async Task<IReadOnlyList<PipRow>> ForManagerAsync(int me, CancellationToken ct = default)
    {
        var team = (await ReportingLine.LoadAsync(db, ct)).ReportsOf(me).ToList();
        if (team.Count == 0) return [];
        return (await Rows(db.ImprovementPlans.AsNoTracking().Where(p => team.Contains(p.EmployeeId))).ToListAsync(ct)).OrderBy(r => r.Status).ThenBy(r => r.EndsOn).ToList();
    }

    public async Task<IReadOnlyList<PipRow>> MineAsync(int employeeId, CancellationToken ct = default) =>
        (await Rows(db.ImprovementPlans.AsNoTracking().Where(p => p.EmployeeId == employeeId && p.Status != PipStatus.Draft)).ToListAsync(ct)).OrderByDescending(r => r.StartsOn).ToList();

    public async Task<PipView?> GetAsync(int id, int? me, bool isHr, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.AsNoTracking().Include(x => x.Objectives).Include(x => x.CheckIns).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        var isEmployee = me == p.EmployeeId;
        var canManage = await CanManageAsync(p.EmployeeId, me, isHr, ct);
        // The employee sees the plan only once HR has started it; peers never.
        if (!(canManage || (isEmployee && p.Status != PipStatus.Draft))) return null;
        var names = await db.Employees.AsNoTracking().Where(e => e.Id == p.EmployeeId || e.Id == p.ManagerEmployeeId)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return new PipView(p, names[p.EmployeeId], p.ManagerEmployeeId is int m ? names.GetValueOrDefault(m) : null, isEmployee, canManage, isHr);
    }

    private async Task NotifyAsync(ImprovementPlan p, bool employee, bool manager, string title, string body, CancellationToken ct)
    {
        var ids = new List<int>();
        if (employee) ids.Add(p.EmployeeId);
        if (manager && p.ManagerEmployeeId is int m) ids.Add(m);
        var users = await db.Employees.Where(e => ids.Contains(e.Id) && e.UserId != null).Select(e => e.UserId!.Value).ToListAsync(ct);
        if (users.Count > 0) notifications.Queue(users, title, body, $"/me/pips/{p.Id}");
    }

    public async Task<Result> StartAsync(int id, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || p.Status != PipStatus.Draft) return Result.Fail("Only a draft plan can be started.");
        p.Status = PipStatus.Active;
        p.StartedAtUtc = clock.UtcNow;
        audit.Add("Pip.Started", nameof(ImprovementPlan), p.Id.ToString(), $"{p.StartsOn:dd MMM yyyy} – {p.EndsOn:dd MMM yyyy}");
        await db.SaveChangesAsync(ct);
        await NotifyAsync(p, true, true, "Performance improvement plan", $"A plan running {p.StartsOn:dd MMM} – {p.EndsOn:dd MMM yyyy} has been shared with you. Please read and acknowledge it.", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AcknowledgeAsync(int id, int me, string? comment, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == me, ct);
        if (p is null || p.Status != PipStatus.Active || p.AcknowledgedAtUtc is not null) return Result.Fail("This plan isn't waiting for your acknowledgement.");
        p.AcknowledgedAtUtc = clock.UtcNow;
        p.EmployeeComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim() is { Length: > 2000 } c ? c[..2000] : comment.Trim();
        await db.SaveChangesAsync(ct);
        await NotifyAsync(p, false, true, "Plan acknowledged", "The employee has read the improvement plan.", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CheckInAsync(int id, int? me, bool isHr, PipProgress progress, string note, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || p.Status != PipStatus.Active || !await CanManageAsync(p.EmployeeId, me, isHr, ct)) return Result.Fail("Check-ins can be added to an active plan by the manager or HR.");
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length < 10) return Result.Fail("Describe what was discussed.");
        db.PipCheckIns.Add(new PipCheckIn { ImprovementPlanId = p.Id, OnDate = await TodayAsync(ct), Progress = progress, Note = note.Trim()[..Math.Min(2000, note.Trim().Length)], ByName = await MyNameAsync(ct), AtUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        await NotifyAsync(p, true, false, "Improvement plan check-in", "A check-in was added to your plan.", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetObjectiveAsync(int objectiveId, int? me, bool isHr, PipObjectiveStatus status, CancellationToken ct = default)
    {
        var o = await db.PipObjectives.FirstOrDefaultAsync(x => x.Id == objectiveId, ct);
        if (o is null) return Result.Fail("Objective not found.");
        var p = await db.ImprovementPlans.AsNoTracking().FirstAsync(x => x.Id == o.ImprovementPlanId, ct);
        if (p.Status != PipStatus.Active || !await CanManageAsync(p.EmployeeId, me, isHr, ct)) return Result.Fail("Objectives can be updated on an active plan by the manager or HR.");
        o.Status = status;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ExtendAsync(int id, DateOnly newEnd, string reason, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || p.Status != PipStatus.Active) return Result.Fail("Only an active plan can be extended.");
        if (p.Extensions >= IPipService.MaxExtensions) return Result.Fail($"A plan can be extended at most {IPipService.MaxExtensions} times.");
        if (newEnd <= p.EndsOn) return Result.Fail("The new end date must be after the current one.");
        if (newEnd.DayNumber - p.StartsOn.DayNumber > IPipService.MaxDays + 90) return Result.Fail("That would make the plan too long.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for the extension.");
        var old = p.EndsOn;
        p.EndsOn = newEnd;
        p.Extensions++;
        db.PipCheckIns.Add(new PipCheckIn { ImprovementPlanId = p.Id, OnDate = await TodayAsync(ct), Progress = PipProgress.SomeConcerns, Note = $"Extended from {old:dd MMM yyyy} to {newEnd:dd MMM yyyy}: {reason.Trim()}", ByName = await MyNameAsync(ct), AtUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        await NotifyAsync(p, true, true, "Improvement plan extended", $"Now ends {newEnd:dd MMM yyyy}.", ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CloseAsync(int id, int? me, bool isHr, PipStatus outcome, string note, CancellationToken ct = default)
    {
        var p = await db.ImprovementPlans.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Fail("Plan not found.");
        if (outcome is not (PipStatus.Passed or PipStatus.NotPassed or PipStatus.Cancelled)) return Result.Fail("Choose an outcome.");
        // HR decides outcomes; whoever manages a draft may cancel it.
        var allowed = isHr ? p.Status is PipStatus.Active or PipStatus.Draft
            : outcome == PipStatus.Cancelled && p.Status == PipStatus.Draft && await CanManageAsync(p.EmployeeId, me, false, ct);
        if (!allowed) return Result.Fail("Only HR can record the outcome of a plan.");
        if (outcome != PipStatus.Cancelled && p.Status != PipStatus.Active) return Result.Fail("Only an active plan has an outcome.");
        if (string.IsNullOrWhiteSpace(note)) return Result.Fail("Summarise the outcome.");
        var wasActive = p.Status == PipStatus.Active;
        p.Status = outcome;
        p.OutcomeNote = note.Trim()[..Math.Min(2000, note.Trim().Length)];
        p.ClosedByName = await MyNameAsync(ct);
        p.ClosedAtUtc = clock.UtcNow;
        audit.Add("Pip.Closed", nameof(ImprovementPlan), p.Id.ToString(), outcome.ToString());
        await db.SaveChangesAsync(ct);
        if (wasActive)
        {
            await NotifyAsync(p, true, true, "Improvement plan closed", outcome switch
            {
                PipStatus.Passed => "The plan was completed successfully. Well done.",
                PipStatus.NotPassed => "The plan has ended. HR will discuss next steps with you.",
                _ => "The plan was cancelled."
            }, ct);
            await db.SaveChangesAsync(ct);
        }
        return Result.Ok();
    }
}
