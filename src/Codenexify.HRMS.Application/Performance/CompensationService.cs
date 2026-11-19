using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class GuidelineInput
{
    public int Rating { get; set; }
    [Range(0, 100)] public decimal MinPercent { get; set; }
    [Range(0, 100)] public decimal MaxPercent { get; set; }
}

public sealed class CompensationCycleInput : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Display(Name = "Effective from")] public DateOnly EffectiveOn { get; set; }
    [Range(0, 50), Display(Name = "Budget (% of current annual gross)")] public decimal BudgetPercent { get; set; }
    [Display(Name = "Use ratings from review cycle")] public int? ReviewCycleId { get; set; }
    [Display(Name = "Only employees who joined on or before")] public DateOnly? JoinedOnOrBefore { get; set; }
    [Display(Name = "Only this department")] public int? DepartmentId { get; set; }
    [Display(Name = "Managers finish by")] public DateOnly? PlanningDueOn { get; set; }
    /// <summary>Rows for ratings 1–5 and 0 (no rating).</summary>
    public List<GuidelineInput> Guidelines { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Guidelines.Any(g => g.MaxPercent < g.MinPercent)) yield return new ValidationResult("In each guideline the maximum must be at least the minimum.", [nameof(Guidelines)]);
        if (Guidelines.Any(g => g.Rating is < 0 or > 5)) yield return new ValidationResult("Guidelines are for ratings 1–5 and 'no rating'.", [nameof(Guidelines)]);
    }
}

public sealed record CompensationCycleRow(int Id, string Name, DateOnly EffectiveOn, CompensationCycleStatus Status, int People, decimal Budget, decimal Planned);
public sealed record CompensationLineRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string? Department, int? PlannerId, string? PlannerName,
    decimal CurrentGross, int? Rating, decimal? Percent, decimal? NewGross, string? Note, bool OutsideGuideline, string? ApplyError, bool Applied)
{
    public decimal Increase => NewGross is decimal n ? n - CurrentGross : 0;
}
public sealed record PlannerTotal(int? PlannerId, string PlannerName, int People, decimal Budget, decimal Planned, int Pending);
public sealed record CompensationCycleView(CompensationCycle Cycle, string? ReviewCycleName, IReadOnlyList<CompensationLineRow> Lines, decimal Budget, decimal Planned, IReadOnlyList<PlannerTotal> ByPlanner)
{
    public decimal Remaining => Budget - Planned;
    public int Pending => Lines.Count(l => l.Percent is null);
    public int OutsideGuidelines => Lines.Count(l => l.OutsideGuideline);
}

public interface ICompensationService
{
    Task<IReadOnlyList<CompensationCycleRow>> CyclesAsync(CancellationToken ct = default);
    Task<CompensationCycleInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, CompensationCycleInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<int>> LaunchAsync(int id, CancellationToken ct = default);
    /// <param name="plannerId">Null = everything (HR); otherwise only lines that manager plans.</param>
    Task<CompensationCycleView?> ViewAsync(int id, int? plannerId, CancellationToken ct = default);
    /// <summary>Cycles in planning where the manager has lines to fill.</summary>
    Task<IReadOnlyList<CompensationCycleRow>> ForPlannerAsync(int me, CancellationToken ct = default);
    /// <param name="percent">Raise in %, or null to clear.</param>
    Task<Result> SetLineAsync(int lineId, int? me, bool isHr, decimal? percent, string? note, CancellationToken ct = default);
    Task<Result> MoveToReviewAsync(int id, CancellationToken ct = default);
    Task<Result> ReopenAsync(int id, CancellationToken ct = default);
    Task<Result<int>> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result> CloseAsync(int id, CancellationToken ct = default);
    /// <summary>Applies approved raises on/after the effective date. Returns how many were applied.</summary>
    Task<int> ApplyDueAsync(int? cycleId = null, CancellationToken ct = default);
    Task<byte[]?> ExportAsync(int id, CancellationToken ct = default);
}

public sealed class CompensationService(IAppDbContext db, ICurrentContext context, IClock clock, IPayrollSetupService payroll, INotificationService notifications,
    IExcelExporter excel, IAuditWriter audit) : ICompensationService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public static IReadOnlyList<int> GuidelineRatings => [5, 4, 3, 2, 1, 0];

    public static bool Outside(CompensationLine l, IReadOnlyCollection<CompensationGuideline> guidelines)
    {
        if (l.ProposedPercent is not decimal p) return false;
        var g = guidelines.FirstOrDefault(x => x.Rating == (l.Rating ?? 0) && !x.IsDeleted);
        return g is not null && (p < g.MinPercent || p > g.MaxPercent);
    }

    public async Task<IReadOnlyList<CompensationCycleRow>> CyclesAsync(CancellationToken ct = default)
    {
        var cycles = await db.CompensationCycles.AsNoTracking().OrderByDescending(c => c.EffectiveOn).ToListAsync(ct);
        var sums = await db.CompensationLines.AsNoTracking().GroupBy(l => l.CompensationCycleId)
            .Select(g => new { g.Key, N = g.Count(), Current = g.Sum(l => l.CurrentAnnualGross), New = g.Sum(l => l.ProposedAnnualGross ?? l.CurrentAnnualGross) })
            .ToDictionaryAsync(g => g.Key, ct);
        return cycles.Select(c =>
        {
            var s = sums.GetValueOrDefault(c.Id);
            return new CompensationCycleRow(c.Id, c.Name, c.EffectiveOn, c.Status, s?.N ?? 0, Math.Round((s?.Current ?? 0) * c.BudgetPercent / 100), (s?.New ?? 0) - (s?.Current ?? 0));
        }).ToList();
    }

    public async Task<CompensationCycleInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CompensationCycles.AsNoTracking().Include(x => x.Guidelines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        return new CompensationCycleInput
        {
            Name = c.Name, EffectiveOn = c.EffectiveOn, BudgetPercent = c.BudgetPercent, ReviewCycleId = c.ReviewCycleId, JoinedOnOrBefore = c.JoinedOnOrBefore,
            DepartmentId = c.DepartmentId, PlanningDueOn = c.PlanningDueOn,
            Guidelines = GuidelineRatings.Select(r => c.Guidelines.FirstOrDefault(g => g.Rating == r && !g.IsDeleted) is { } g
                ? new GuidelineInput { Rating = r, MinPercent = g.MinPercent, MaxPercent = g.MaxPercent } : new GuidelineInput { Rating = r }).ToList()
        };
    }

    public async Task<Result<int>> SaveAsync(int? id, CompensationCycleInput i, CancellationToken ct = default)
    {
        if (i.ReviewCycleId is int rc && !await db.ReviewCycles.AnyAsync(x => x.Id == rc, ct)) return Result<int>.Fail("Review cycle not found.");
        if (i.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Department not found.");
        CompensationCycle c;
        if (id is int existing)
        {
            c = await db.CompensationCycles.Include(x => x.Guidelines).FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new InvalidOperationException();
            if (c.Status is CompensationCycleStatus.Approved or CompensationCycleStatus.Closed) return Result<int>.Fail("An approved cycle can't be changed.");
            if (c.Status != CompensationCycleStatus.Draft && (c.ReviewCycleId != i.ReviewCycleId || c.JoinedOnOrBefore != i.JoinedOnOrBefore || c.DepartmentId != i.DepartmentId))
                return Result<int>.Fail("Who is included is fixed at launch.");
        }
        else
        {
            c = new CompensationCycle();
            db.CompensationCycles.Add(c);
        }
        c.Name = i.Name.Trim();
        c.EffectiveOn = i.EffectiveOn;
        c.BudgetPercent = i.BudgetPercent;
        c.ReviewCycleId = i.ReviewCycleId;
        c.JoinedOnOrBefore = i.JoinedOnOrBefore;
        c.DepartmentId = i.DepartmentId;
        c.PlanningDueOn = i.PlanningDueOn;
        foreach (var g in i.Guidelines.Where(g => g.Rating is >= 0 and <= 5))
        {
            var row = c.Guidelines.FirstOrDefault(x => x.Rating == g.Rating && !x.IsDeleted);
            if (row is null) c.Guidelines.Add(row = new CompensationGuideline { Rating = g.Rating });
            row.MinPercent = g.MinPercent;
            row.MaxPercent = g.MaxPercent;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(c.Id);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CompensationCycles.Include(x => x.Guidelines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || c.Status != CompensationCycleStatus.Draft) return Result.Fail("Only a draft cycle can be deleted.");
        db.CompensationGuidelines.RemoveRange(c.Guidelines);
        db.CompensationCycles.Remove(c);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> LaunchAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CompensationCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || c.Status != CompensationCycleStatus.Draft) return Result<int>.Fail("Only a draft cycle can be launched.");
        var today = await TodayAsync(ct);
        var q = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (c.JoinedOnOrBefore is { } j) q = q.Where(e => e.DateOfJoining <= j);
        if (c.DepartmentId is int d) q = q.Where(e => e.DepartmentId == d);
        var people = await q.ToListAsync(ct);
        var current = people.Select(p => p.Id).ToHashSet();
        var ratings = c.ReviewCycleId is int rc
            ? await db.PerformanceReviews.AsNoTracking().Where(r => r.ReviewCycleId == rc && r.ManagerSubmittedAtUtc != null)
                .Select(r => new { r.EmployeeId, Rating = r.FinalRating ?? r.OverallRating }).ToDictionaryAsync(r => r.EmployeeId, r => r.Rating, ct)
            : new Dictionary<int, int?>();
        var managers = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive).Select(e => new { e.Id, e.UserId }).ToDictionaryAsync(e => e.Id, ct);
        var added = 0;
        foreach (var e in people)
        {
            var salary = await payroll.SalaryAsOfAsync(e.Id, today, ct);
            if (salary is null) continue; // nothing to revise
            db.CompensationLines.Add(new CompensationLine
            {
                CompensationCycleId = c.Id, EmployeeId = e.Id, CurrentAnnualGross = salary.AnnualGross, Rating = ratings.GetValueOrDefault(e.Id),
                PlannerEmployeeId = e.ReportingManagerId is int m && managers.ContainsKey(m) && m != e.Id ? m : null
            });
            added++;
        }
        if (added == 0) return Result<int>.Fail("No eligible employee has a salary set up.");
        c.Status = CompensationCycleStatus.Planning;
        audit.Add("Compensation.Launched", nameof(CompensationCycle), c.Id.ToString(), $"{c.Name}: {added} employee(s)");
        await db.SaveChangesAsync(ct);

        var plannerUsers = await db.CompensationLines.Where(l => l.CompensationCycleId == c.Id && l.PlannerEmployeeId != null).Select(l => l.PlannerEmployeeId!.Value).Distinct()
            .Join(db.Employees, id => id, e => e.Id, (id, e) => e.UserId).Where(u => u != null).Select(u => u!.Value).ToListAsync(ct);
        if (plannerUsers.Count > 0)
            notifications.Queue(plannerUsers, "Plan your team's salary revisions", $"{c.Name}{(c.PlanningDueOn is { } due ? $": please finish by {due:dd MMM yyyy}" : "")}.", $"/me/compensation/{c.Id}");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(added);
    }

    public async Task<CompensationCycleView?> ViewAsync(int id, int? plannerId, CancellationToken ct = default)
    {
        var c = await db.CompensationCycles.AsNoTracking().Include(x => x.Guidelines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        var lines = await db.CompensationLines.AsNoTracking().Where(l => l.CompensationCycleId == id && (plannerId == null || l.PlannerEmployeeId == plannerId)).ToListAsync(ct);
        if (plannerId is not null && lines.Count == 0) return null;
        var ids = lines.Select(l => l.EmployeeId).Concat(lines.Where(l => l.PlannerEmployeeId != null).Select(l => l.PlannerEmployeeId!.Value)).Distinct().ToList();
        var people = await db.Employees.AsNoTracking().Include(e => e.Department).Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department == null ? null : e.Department.Name }).ToDictionaryAsync(e => e.Id, ct);
        var guidelines = c.Guidelines.Where(g => !g.IsDeleted).ToList();
        var rows = lines.Select(l => new CompensationLineRow(l.Id, l.EmployeeId, people[l.EmployeeId].Name, people[l.EmployeeId].EmployeeCode, people[l.EmployeeId].Dept,
                l.PlannerEmployeeId, l.PlannerEmployeeId is int p ? people.GetValueOrDefault(p)?.Name : null, l.CurrentAnnualGross, l.Rating, l.ProposedPercent, l.ProposedAnnualGross,
                l.Note, Outside(l, guidelines), l.ApplyError, l.AppliedAtUtc is not null))
            .OrderBy(r => r.PlannerName ?? "~").ThenBy(r => r.EmployeeName).ToList();
        decimal Budget(IEnumerable<CompensationLineRow> r) => Math.Round(r.Sum(x => x.CurrentGross) * c.BudgetPercent / 100);
        var byPlanner = rows.GroupBy(r => (r.PlannerId, r.PlannerName))
            .Select(g => new PlannerTotal(g.Key.PlannerId, g.Key.PlannerName ?? "HR (no manager)", g.Count(), Budget(g), g.Sum(x => x.Increase), g.Count(x => x.Percent is null)))
            .OrderBy(p => p.PlannerName).ToList();
        var review = c.ReviewCycleId is int rc ? await db.ReviewCycles.Where(x => x.Id == rc).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        return new CompensationCycleView(c, review, rows, Budget(rows), rows.Sum(r => r.Increase), byPlanner);
    }

    public async Task<IReadOnlyList<CompensationCycleRow>> ForPlannerAsync(int me, CancellationToken ct = default)
    {
        var ids = await db.CompensationLines.AsNoTracking().Where(l => l.PlannerEmployeeId == me).Select(l => l.CompensationCycleId).Distinct().ToListAsync(ct);
        return (await CyclesAsync(ct)).Where(c => ids.Contains(c.Id) && c.Status != CompensationCycleStatus.Draft).ToList();
    }

    public async Task<Result> SetLineAsync(int lineId, int? me, bool isHr, decimal? percent, string? note, CancellationToken ct = default)
    {
        var l = await db.CompensationLines.FirstOrDefaultAsync(x => x.Id == lineId, ct);
        if (l is null) return Result.Fail("Line not found.");
        var c = await db.CompensationCycles.AsNoTracking().Include(x => x.Guidelines).FirstAsync(x => x.Id == l.CompensationCycleId, ct);
        var open = isHr ? c.Status is CompensationCycleStatus.Planning or CompensationCycleStatus.Review : c.Status == CompensationCycleStatus.Planning && l.PlannerEmployeeId == me;
        if (!open) return Result.Fail(isHr ? "The cycle is no longer open for changes." : "Planning for this cycle is closed, or this person isn't yours to plan.");
        if (percent is < 0 or > 100) return Result.Fail("Enter a raise between 0% and 100%.");
        l.ProposedPercent = percent is decimal p ? Math.Round(p, 2) : null;
        l.ProposedAnnualGross = percent is decimal q ? Math.Round(l.CurrentAnnualGross * (1 + q / 100m), 0, MidpointRounding.AwayFromZero) : null;
        l.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() is { Length: > 500 } n ? n[..500] : note.Trim();
        if (Outside(l, c.Guidelines.ToList()) && l.Note is null) return Result.Fail("This raise is outside the guideline for the rating. Add a note explaining why.");
        l.UpdatedByName = await MyNameAsync(ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<Result> MoveAsync(int id, CompensationCycleStatus from, CompensationCycleStatus to, CancellationToken ct)
    {
        var c = await db.CompensationCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || c.Status != from) return Result.Fail($"The cycle isn't in {from}.");
        c.Status = to;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<Result> MoveToReviewAsync(int id, CancellationToken ct = default) => MoveAsync(id, CompensationCycleStatus.Planning, CompensationCycleStatus.Review, ct);
    public Task<Result> ReopenAsync(int id, CancellationToken ct = default) => MoveAsync(id, CompensationCycleStatus.Review, CompensationCycleStatus.Planning, ct);

    public async Task<Result<int>> ApproveAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CompensationCycles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null || c.Status != CompensationCycleStatus.Review) return Result<int>.Fail("Move the cycle to HR review before approving.");
        if (await db.CompensationLines.AnyAsync(l => l.CompensationCycleId == id && l.ProposedPercent == null, ct))
            return Result<int>.Fail("Some employees have no decision yet. Enter 0% where there's no raise.");
        c.Status = CompensationCycleStatus.Approved;
        c.ApprovedByName = await MyNameAsync(ct);
        c.ApprovedAtUtc = clock.UtcNow;
        audit.Add("Compensation.Approved", nameof(CompensationCycle), c.Id.ToString(), c.Name);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(c.EffectiveOn <= await TodayAsync(ct) ? await ApplyDueAsync(id, ct) : 0);
    }

    public Task<Result> CloseAsync(int id, CancellationToken ct = default) => MoveAsync(id, CompensationCycleStatus.Approved, CompensationCycleStatus.Closed, ct);

    public async Task<int> ApplyDueAsync(int? cycleId = null, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var cycles = await db.CompensationCycles.Where(c => c.Status == CompensationCycleStatus.Approved && c.EffectiveOn <= today && (cycleId == null || c.Id == cycleId)).ToListAsync(ct);
        var applied = 0;
        foreach (var c in cycles)
        {
            var lines = await db.CompensationLines.Where(l => l.CompensationCycleId == c.Id && l.AppliedAtUtc == null && (cycleId != null || l.ApplyError == null)).ToListAsync(ct);
            foreach (var l in lines)
            {
                if (l.ProposedAnnualGross is not decimal gross || gross <= l.CurrentAnnualGross) { l.AppliedAtUtc = clock.UtcNow; l.ApplyError = null; continue; } // no raise
                var salary = await payroll.SalaryAsOfAsync(l.EmployeeId, c.EffectiveOn, ct) ?? await payroll.SalaryAsOfAsync(l.EmployeeId, today, ct);
                if (salary is null) { l.ApplyError = "No salary set up."; continue; }
                var r = await payroll.AssignAsync(l.EmployeeId, new SalaryAssignInput
                {
                    SalaryStructureId = salary.SalaryStructureId, AnnualGross = gross, EffectiveFrom = c.EffectiveOn, PfApplicable = salary.PfApplicable,
                    Remarks = $"{c.Name}", PayArrears = c.EffectiveOn < today
                }, ct);
                if (!r.Succeeded) { l.ApplyError = r.Error is { Length: > 500 } e ? e[..500] : r.Error; continue; }
                l.AppliedAtUtc = clock.UtcNow;
                l.ApplyError = null;
                applied++;
                var user = await db.Employees.Where(e => e.Id == l.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
                if (user is int u) notifications.Queue([u], "Your salary has been revised", $"{c.Name}: effective {c.EffectiveOn:dd MMM yyyy}. See your next payslip.", "/me/payslips");
            }
            await db.SaveChangesAsync(ct);
        }
        return applied;
    }

    public async Task<byte[]?> ExportAsync(int id, CancellationToken ct = default)
    {
        var v = await ViewAsync(id, null, ct);
        if (v is null) return null;
        return excel.Export("Salary revisions", ["Employee code", "Employee", "Department", "Planned by", "Rating", "Current annual gross", "Raise %", "New annual gross", "Increase", "Outside guideline", "Note", "Applied"],
            v.Lines.Select(l => (IReadOnlyList<object?>)[l.EmployeeCode, l.EmployeeName, l.Department, l.PlannerName ?? "HR", l.Rating, l.CurrentGross, l.Percent, l.NewGross, l.Increase,
                l.OutsideGuideline ? "Yes" : null, l.Note, l.Applied ? "Yes" : l.ApplyError]));
    }
}
