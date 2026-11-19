using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class GoalInput : IValidatableObject
{
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public GoalLevel Level { get; set; } = GoalLevel.Individual;
    [Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Owner")] public int? OwnerEmployeeId { get; set; }
    [Display(Name = "Supports (parent goal)")] public int? ParentGoalId { get; set; }
    [Display(Name = "Start")] public DateOnly StartsOn { get; set; }
    [Display(Name = "Due")] public DateOnly DueOn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueOn < StartsOn) yield return new ValidationResult("The due date is before the start date.", [nameof(DueOn)]);
        if (DueOn.DayNumber - StartsOn.DayNumber > 3 * 366) yield return new ValidationResult("A goal can span at most three years.", [nameof(DueOn)]);
    }
}

public sealed class KeyResultInput
{
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    public KeyResultType Type { get; set; } = KeyResultType.Number;
    [Display(Name = "Start value")] public decimal StartValue { get; set; }
    [Display(Name = "Target")] public decimal TargetValue { get; set; } = 100;
    [StringLength(20)] public string? Unit { get; set; }
}

public sealed class CheckInInput
{
    /// <summary>New current values by key-result id.</summary>
    public Dictionary<int, decimal> Values { get; set; } = [];
    public GoalStatus Status { get; set; } = GoalStatus.OnTrack;
    /// <summary>Used only for goals without key results (0–100).</summary>
    [Range(0, 100)] public decimal? Progress { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

/// <summary>Who is looking: their employee record, HR rights, and everyone below them in the reporting line.</summary>
public sealed record GoalViewer(int? EmployeeId, bool IsHr, IReadOnlySet<int> Reports)
{
    public bool CanSeeIndividual(int ownerId) => IsHr || ownerId == EmployeeId || Reports.Contains(ownerId);
}

public sealed record GoalRow(int Id, string Title, GoalLevel Level, int? OwnerEmployeeId, string? OwnerName, string? DepartmentName,
    DateOnly StartsOn, DateOnly DueOn, GoalStatus Status, decimal Progress, int? ParentGoalId, string? ParentTitle, int KeyResults);
public sealed record KeyResultView(KeyResult KeyResult, decimal Progress);
public sealed record GoalView(Goal Goal, string? OwnerName, string? DepartmentName, GoalRow? Parent, IReadOnlyList<GoalRow> Children,
    IReadOnlyList<KeyResultView> KeyResults, IReadOnlyList<GoalCheckIn> CheckIns, bool CanEdit);

public interface IGoalService
{
    Task<GoalViewer> ViewerAsync(bool isHr, CancellationToken ct = default);
    /// <summary>Company and department goals (visible to everyone) for goals overlapping the period.</summary>
    Task<IReadOnlyList<GoalRow>> SharedAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<GoalRow>> IndividualAsync(int employeeId, GoalViewer viewer, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
    /// <summary>Individual goals of everyone in the viewer's reporting line (or everyone, for HR).</summary>
    Task<IReadOnlyList<GoalRow>> TeamAsync(GoalViewer viewer, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<GoalView?> GetAsync(int id, GoalViewer viewer, CancellationToken ct = default);
    Task<GoalInput?> GetForEditAsync(int id, GoalViewer viewer, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, GoalInput input, GoalViewer viewer, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, GoalViewer viewer, CancellationToken ct = default);
    Task<Result> AddKeyResultAsync(int goalId, KeyResultInput input, GoalViewer viewer, CancellationToken ct = default);
    Task<Result> RemoveKeyResultAsync(int keyResultId, GoalViewer viewer, CancellationToken ct = default);
    Task<Result> CheckInAsync(int goalId, CheckInInput input, GoalViewer viewer, CancellationToken ct = default);
    /// <summary>Goals that can be chosen as a parent: company and department goals that are still open.</summary>
    Task<IReadOnlyList<GoalRow>> ParentOptionsAsync(CancellationToken ct = default);
}

public sealed class GoalService(IAppDbContext db, ICurrentContext context, IClock clock, IEmployeeService employees, INotificationService notifications) : IGoalService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public static decimal KeyResultProgress(KeyResult kr)
    {
        if (kr.Type == KeyResultType.Milestone) return kr.CurrentValue >= 1 ? 100 : 0;
        if (kr.TargetValue == kr.StartValue) return kr.CurrentValue >= kr.TargetValue ? 100 : 0;
        // Works for "reduce from 40 to 10" as well as "grow from 0 to 100".
        var p = (kr.CurrentValue - kr.StartValue) / (kr.TargetValue - kr.StartValue) * 100m;
        return Math.Round(Math.Clamp(p, 0, 100), 1);
    }

    public async Task<GoalViewer> ViewerAsync(bool isHr, CancellationToken ct = default)
    {
        var me = await employees.GetMyEmployeeIdAsync(ct);
        var reports = new HashSet<int>();
        if (me is int id)
        {
            var links = await db.Employees.AsNoTracking().Where(e => e.ReportingManagerId != null).Select(e => new { e.Id, e.ReportingManagerId }).ToListAsync(ct);
            var frontier = new Queue<int>([id]);
            while (frontier.Count > 0)
            {
                var m = frontier.Dequeue();
                foreach (var r in links.Where(l => l.ReportingManagerId == m && l.Id != id))
                    if (reports.Add(r.Id)) frontier.Enqueue(r.Id);
            }
        }
        return new GoalViewer(me, isHr, reports);
    }

    private IQueryable<GoalRow> Rows(IQueryable<Goal> q) =>
        from g in q
        join e in db.Employees on g.OwnerEmployeeId equals e.Id into owners
        from o in owners.DefaultIfEmpty()
        join d in db.Departments on g.DepartmentId equals d.Id into depts
        from dp in depts.DefaultIfEmpty()
        join p in db.Goals on g.ParentGoalId equals p.Id into parents
        from pg in parents.DefaultIfEmpty()
        select new GoalRow(g.Id, g.Title, g.Level, g.OwnerEmployeeId, o == null ? null : o.FirstName + " " + o.LastName, dp == null ? null : dp.Name,
            g.StartsOn, g.DueOn, g.Status, g.Progress, g.ParentGoalId, pg == null ? null : pg.Title, g.KeyResults.Count);

    private static List<GoalRow> Sort(IEnumerable<GoalRow> rows) =>
        rows.OrderBy(r => r.Level).ThenBy(r => r.Status is GoalStatus.Completed or GoalStatus.Cancelled).ThenBy(r => r.DueOn).ThenBy(r => r.Title).ToList();

    public async Task<IReadOnlyList<GoalRow>> SharedAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Sort(await Rows(db.Goals.AsNoTracking().Where(g => g.Level != GoalLevel.Individual && g.StartsOn <= to && g.DueOn >= from)).ToListAsync(ct));

    public async Task<IReadOnlyList<GoalRow>> IndividualAsync(int employeeId, GoalViewer viewer, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        if (!viewer.CanSeeIndividual(employeeId)) return [];
        var q = db.Goals.AsNoTracking().Where(g => g.Level == GoalLevel.Individual && g.OwnerEmployeeId == employeeId);
        if (from is { } f) q = q.Where(g => g.DueOn >= f);
        if (to is { } t) q = q.Where(g => g.StartsOn <= t);
        return Sort(await Rows(q).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<GoalRow>> TeamAsync(GoalViewer viewer, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (!viewer.IsHr && viewer.Reports.Count == 0) return [];
        var ids = viewer.Reports.ToList();
        var q = db.Goals.AsNoTracking().Where(g => g.Level == GoalLevel.Individual && g.StartsOn <= to && g.DueOn >= from);
        if (!viewer.IsHr) q = q.Where(g => ids.Contains(g.OwnerEmployeeId!.Value));
        return (await Rows(q).ToListAsync(ct)).OrderBy(r => r.OwnerName).ThenBy(r => r.DueOn).ToList();
    }

    private bool CanView(Goal g, GoalViewer v) => g.Level != GoalLevel.Individual || (g.OwnerEmployeeId is int o && v.CanSeeIndividual(o));
    private static bool CanEdit(Goal g, GoalViewer v) => v.IsHr || (g.Level == GoalLevel.Individual && g.OwnerEmployeeId is int o && (o == v.EmployeeId || v.Reports.Contains(o)));

    public async Task<GoalView?> GetAsync(int id, GoalViewer viewer, CancellationToken ct = default)
    {
        var g = await db.Goals.AsNoTracking().Include(x => x.KeyResults).Include(x => x.CheckIns).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null || !CanView(g, viewer)) return null;
        var row = await Rows(db.Goals.AsNoTracking().Where(x => x.Id == id)).FirstAsync(ct);
        var parent = g.ParentGoalId is int p ? await Rows(db.Goals.AsNoTracking().Where(x => x.Id == p)).FirstOrDefaultAsync(ct) : null;
        var children = (await Rows(db.Goals.AsNoTracking().Where(x => x.ParentGoalId == id)).ToListAsync(ct))
            .Where(c => c.Level != GoalLevel.Individual || (c.OwnerEmployeeId is int o && viewer.CanSeeIndividual(o))).ToList();
        return new GoalView(g, row.OwnerName, row.DepartmentName, parent, Sort(children),
            g.KeyResults.Where(k => !k.IsDeleted).OrderBy(k => k.SortOrder).Select(k => new KeyResultView(k, KeyResultProgress(k))).ToList(),
            g.CheckIns.Where(c => !c.IsDeleted).OrderByDescending(c => c.AtUtc).Take(50).ToList(), CanEdit(g, viewer));
    }

    public async Task<GoalInput?> GetForEditAsync(int id, GoalViewer viewer, CancellationToken ct = default)
    {
        var g = await db.Goals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return g is null || !CanEdit(g, viewer) ? null : new GoalInput
        {
            Title = g.Title, Description = g.Description, Level = g.Level, DepartmentId = g.DepartmentId, OwnerEmployeeId = g.OwnerEmployeeId,
            ParentGoalId = g.ParentGoalId, StartsOn = g.StartsOn, DueOn = g.DueOn
        };
    }

    public async Task<Result<int>> SaveAsync(int? id, GoalInput i, GoalViewer viewer, CancellationToken ct = default)
    {
        // Who may set what.
        if (i.Level != GoalLevel.Individual && !viewer.IsHr) return Result<int>.Fail("Only HR can set company and department goals.");
        if (i.Level == GoalLevel.Individual)
        {
            i.OwnerEmployeeId ??= viewer.EmployeeId;
            if (i.OwnerEmployeeId is not int owner) return Result<int>.Fail("Choose whose goal this is.");
            if (!viewer.IsHr && owner != viewer.EmployeeId && !viewer.Reports.Contains(owner)) return Result<int>.Fail("You can set goals only for yourself and your team.");
            var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == owner, ct);
            if (emp is null || !emp.IsCurrent) return Result<int>.Fail("Goals can be set only for current employees.");
            i.DepartmentId = null;
        }
        else
        {
            i.OwnerEmployeeId = null;
            if (i.Level == GoalLevel.Department && (i.DepartmentId is not int d || !await db.Departments.AnyAsync(x => x.Id == d, ct)))
                return Result<int>.Fail("Choose the department.");
            if (i.Level == GoalLevel.Company) i.DepartmentId = null;
        }
        if (i.ParentGoalId is int parentId)
        {
            var parent = await db.Goals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == parentId, ct);
            if (parent is null || parent.Id == id) return Result<int>.Fail("The parent goal was not found.");
            if (parent.Level == GoalLevel.Individual) return Result<int>.Fail("Goals can support only company or department goals.");
            if (i.Level == GoalLevel.Company && parent.Level != GoalLevel.Company) return Result<int>.Fail("A company goal can support only another company goal.");
            if (i.Level == GoalLevel.Department && parent.Level != GoalLevel.Company) return Result<int>.Fail("A department goal can support only a company goal.");
            // No loops: walk up from the parent.
            var seen = new HashSet<int>();
            for (int? cur = parent.Id; cur is int c; cur = await db.Goals.Where(x => x.Id == c).Select(x => x.ParentGoalId).FirstOrDefaultAsync(ct))
            {
                if (c == id || !seen.Add(c)) return Result<int>.Fail("That would make the goal support itself.");
            }
        }

        Goal goal;
        if (id is int existing)
        {
            goal = await db.Goals.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new InvalidOperationException();
            if (!CanEdit(goal, viewer)) return Result<int>.Fail("You can't edit this goal.");
            if (goal.Level != i.Level && !viewer.IsHr) return Result<int>.Fail("Only HR can change a goal's level.");
        }
        else
        {
            goal = new Goal { CreatedByName = await MyNameAsync(ct) };
            db.Goals.Add(goal);
        }
        goal.Title = i.Title.Trim();
        goal.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
        goal.Level = i.Level;
        goal.DepartmentId = i.DepartmentId;
        goal.OwnerEmployeeId = i.OwnerEmployeeId;
        goal.ParentGoalId = i.ParentGoalId;
        goal.StartsOn = i.StartsOn;
        goal.DueOn = i.DueOn;
        await db.SaveChangesAsync(ct);

        // A manager setting a goal for someone tells them.
        if (id is null && goal.OwnerEmployeeId is int ownerId && ownerId != viewer.EmployeeId)
        {
            var userId = await db.Employees.Where(e => e.Id == ownerId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
            if (userId is int u) { notifications.Queue([u], "New goal for you", $"{goal.Title} (due {goal.DueOn:dd MMM yyyy}).", $"/me/goals/{goal.Id}"); await db.SaveChangesAsync(ct); }
        }
        return Result<int>.Ok(goal.Id);
    }

    public async Task<Result> DeleteAsync(int id, GoalViewer viewer, CancellationToken ct = default)
    {
        var g = await db.Goals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null || !CanEdit(g, viewer)) return Result.Fail("Goal not found.");
        if (await db.Goals.AnyAsync(x => x.ParentGoalId == id, ct)) return Result.Fail("Other goals support this one. Unlink them first, or mark this goal Cancelled.");
        db.Goals.Remove(g);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddKeyResultAsync(int goalId, KeyResultInput i, GoalViewer viewer, CancellationToken ct = default)
    {
        var g = await db.Goals.Include(x => x.KeyResults).FirstOrDefaultAsync(x => x.Id == goalId, ct);
        if (g is null || !CanEdit(g, viewer)) return Result.Fail("Goal not found.");
        if (g.KeyResults.Count(k => !k.IsDeleted) >= 10) return Result.Fail("A goal can have at most 10 key results.");
        var kr = new KeyResult
        {
            GoalId = goalId, Title = i.Title.Trim(), Type = i.Type, Unit = i.Type == KeyResultType.Percentage ? "%" : i.Type == KeyResultType.Currency ? "₹" : string.IsNullOrWhiteSpace(i.Unit) ? null : i.Unit.Trim(),
            StartValue = i.Type == KeyResultType.Milestone ? 0 : i.StartValue, TargetValue = i.Type == KeyResultType.Milestone ? 1 : i.TargetValue,
            SortOrder = g.KeyResults.Count == 0 ? 10 : g.KeyResults.Max(k => k.SortOrder) + 10
        };
        if (kr.Type != KeyResultType.Milestone && kr.TargetValue == kr.StartValue) return Result.Fail("The target must differ from the start value.");
        kr.CurrentValue = kr.StartValue;
        g.KeyResults.Add(kr);
        Recalculate(g);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveKeyResultAsync(int keyResultId, GoalViewer viewer, CancellationToken ct = default)
    {
        var kr = await db.KeyResults.FirstOrDefaultAsync(k => k.Id == keyResultId, ct);
        if (kr is null) return Result.Fail("Key result not found.");
        var g = await db.Goals.Include(x => x.KeyResults).FirstAsync(x => x.Id == kr.GoalId, ct);
        if (!CanEdit(g, viewer)) return Result.Fail("You can't edit this goal.");
        db.KeyResults.Remove(kr);
        g.KeyResults.Remove(kr);
        Recalculate(g);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static void Recalculate(Goal g)
    {
        var krs = g.KeyResults.Where(k => !k.IsDeleted).ToList();
        if (krs.Count > 0) g.Progress = Math.Round(krs.Average(KeyResultProgress), 1);
    }

    public async Task<Result> CheckInAsync(int goalId, CheckInInput i, GoalViewer viewer, CancellationToken ct = default)
    {
        var g = await db.Goals.Include(x => x.KeyResults).FirstOrDefaultAsync(x => x.Id == goalId, ct);
        if (g is null || !CanEdit(g, viewer)) return Result.Fail("Goal not found.");
        var krs = g.KeyResults.Where(k => !k.IsDeleted).ToList();
        foreach (var (krId, value) in i.Values)
        {
            var kr = krs.FirstOrDefault(k => k.Id == krId);
            if (kr is null) return Result.Fail("A key result was not found.");
            if (Math.Abs(value) > 1_000_000_000_000m) return Result.Fail("A value is too large.");
            kr.CurrentValue = kr.Type == KeyResultType.Milestone ? (value >= 1 ? 1 : 0) : value;
        }
        if (krs.Count > 0) Recalculate(g);
        else if (i.Progress is { } p) g.Progress = Math.Round(Math.Clamp(p, 0, 100), 1);
        g.Status = i.Status;
        if (g.Status == GoalStatus.Completed && krs.Count == 0) g.Progress = 100;
        db.GoalCheckIns.Add(new GoalCheckIn
        {
            GoalId = g.Id, Progress = g.Progress, Status = g.Status, Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim(),
            ByName = await MyNameAsync(ct), AtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<GoalRow>> ParentOptionsAsync(CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        return Sort(await Rows(db.Goals.AsNoTracking().Where(g => g.Level != GoalLevel.Individual && g.Status != GoalStatus.Cancelled && g.DueOn >= today.AddDays(-90))).ToListAsync(ct));
    }
}
