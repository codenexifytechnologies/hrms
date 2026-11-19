using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Planning;

public sealed class HeadcountPlanInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Display(Name = "Starts on")] public DateOnly StartsOn { get; set; }
    [Display(Name = "Ends on")] public DateOnly EndsOn { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class HeadcountPositionInput
{
    [Required, StringLength(150), Display(Name = "Role")] public string Title { get; set; } = string.Empty;
    [Required, Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    [Display(Name = "Job title")] public int? DesignationId { get; set; }
    [Range(1, 500)] public int Openings { get; set; } = 1;
    [Display(Name = "Type")] public HeadcountPositionKind Kind { get; set; } = HeadcountPositionKind.NewRole;
    [Display(Name = "Target start")] public DateOnly TargetStartOn { get; set; }
    [Range(0, 1_000_000_000), Display(Name = "Annual budget per hire (₹)")] public decimal AnnualBudgetPerHire { get; set; }
    public HeadcountPriority Priority { get; set; } = HeadcountPriority.Medium;
    public HeadcountPositionStatus Status { get; set; } = HeadcountPositionStatus.Open;
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed record HeadcountPlanRow(int Id, string Name, DateOnly StartsOn, DateOnly EndsOn, HeadcountPlanStatus Status, int Positions, int Openings, int Filled, decimal Budget);
public sealed record HeadcountHireRow(int HireId, int EmployeeId, string Name, string Code, DateOnly DateOfJoining, decimal? AnnualGross);
public sealed record HeadcountPositionRow(int Id, string Title, int DepartmentId, string Department, string? Branch, string? Designation, int Openings, HeadcountPositionKind Kind,
    DateOnly TargetStartOn, decimal BudgetPerHire, HeadcountPriority Priority, HeadcountPositionStatus Status, string? Notes, IReadOnlyList<HeadcountHireRow> Hires)
{
    public int Filled => Hires.Count;
    /// <summary>Openings still to fill (none for a cancelled position).</summary>
    public int Remaining => Status == HeadcountPositionStatus.Cancelled ? 0 : Math.Max(0, Openings - Hires.Count);
    public decimal Budget => Status == HeadcountPositionStatus.Cancelled ? 0 : Openings * BudgetPerHire;
}
public sealed record HeadcountDepartmentSummary(int DepartmentId, string Department, int Today, int JoiningSoon, int KnownExits, int NewOpenings, int ReplacementOpenings,
    int Filled, int Remaining, int Projected, decimal Budget, decimal? Committed);
public sealed record HeadcountJoiner(int EmployeeId, string Name, string Code, int? DepartmentId, string? Department, DateOnly DateOfJoining);

public sealed class HeadcountPlanView
{
    public required HeadcountPlan Plan { get; init; }
    public required IReadOnlyList<HeadcountPositionRow> Positions { get; init; }
    public required IReadOnlyList<HeadcountDepartmentSummary> Departments { get; init; }
    /// <summary>People who joined in the plan period but aren't linked to a position yet.</summary>
    public required IReadOnlyList<HeadcountJoiner> UnlinkedJoiners { get; init; }
    public bool ShowsCost { get; init; }
    public DateOnly Today { get; init; }
}

public interface IHeadcountService
{
    Task<IReadOnlyList<HeadcountPlanRow>> PlansAsync(CancellationToken ct = default);
    Task<HeadcountPlanInput?> GetPlanAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SavePlanAsync(int? id, HeadcountPlanInput input, CancellationToken ct = default);
    Task<Result> DeletePlanAsync(int id, CancellationToken ct = default);
    /// <summary>Moves a plan between Draft, Approved and Closed ("approve", "draft", "close", "reopen").</summary>
    Task<Result> SetStatusAsync(int id, string step, CancellationToken ct = default);
    /// <param name="payrollAllowed">Whether hires' actual salaries (committed cost) may be shown.</param>
    Task<HeadcountPlanView?> ViewAsync(int id, bool payrollAllowed, CancellationToken ct = default);
    Task<HeadcountPositionInput?> GetPositionAsync(int planId, int positionId, CancellationToken ct = default);
    Task<Result<int>> SavePositionAsync(int planId, int? positionId, HeadcountPositionInput input, CancellationToken ct = default);
    Task<Result> DeletePositionAsync(int planId, int positionId, CancellationToken ct = default);
    Task<Result> LinkHireAsync(int planId, int positionId, int employeeId, CancellationToken ct = default);
    Task<Result> UnlinkHireAsync(int planId, int hireId, CancellationToken ct = default);
    Task<byte[]?> ExportAsync(int id, bool payrollAllowed, CancellationToken ct = default);
}

public sealed class HeadcountService(IAppDbContext db, ICurrentContext context, IClock clock, IExcelExporter excel) : IHeadcountService
{
    public const int MaxPositionsPerPlan = 300;
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    public async Task<IReadOnlyList<HeadcountPlanRow>> PlansAsync(CancellationToken ct = default)
    {
        var plans = await db.HeadcountPlans.AsNoTracking().Include(p => p.Positions).ThenInclude(x => x.Hires).OrderByDescending(p => p.StartsOn).ToListAsync(ct);
        return plans.Select(p =>
        {
            var live = p.Positions.Where(x => !x.IsDeleted && x.Status != HeadcountPositionStatus.Cancelled).ToList();
            return new HeadcountPlanRow(p.Id, p.Name, p.StartsOn, p.EndsOn, p.Status, live.Count, live.Sum(x => x.Openings),
                live.Sum(x => Math.Min(x.Openings, x.Hires.Count(h => !h.IsDeleted))), live.Sum(x => x.Openings * x.AnnualBudgetPerHire));
        }).ToList();
    }

    public async Task<HeadcountPlanInput?> GetPlanAsync(int id, CancellationToken ct = default) =>
        await db.HeadcountPlans.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new HeadcountPlanInput { Name = p.Name, StartsOn = p.StartsOn, EndsOn = p.EndsOn, Notes = p.Notes }).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> SavePlanAsync(int? id, HeadcountPlanInput i, CancellationToken ct = default)
    {
        if (i.EndsOn <= i.StartsOn) return Result<int>.Fail("The plan must end after it starts.");
        if (i.EndsOn > i.StartsOn.AddYears(3)) return Result<int>.Fail("A plan can cover at most three years.");
        HeadcountPlan p;
        if (id is int existing)
        {
            var found = await db.HeadcountPlans.Include(x => x.Positions).FirstOrDefaultAsync(x => x.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Plan not found.");
            if (found.Status == HeadcountPlanStatus.Closed) return Result<int>.Fail("A closed plan can't be changed. Reopen it first.");
            if (found.Positions.Any(x => !x.IsDeleted && (x.TargetStartOn < i.StartsOn || x.TargetStartOn > i.EndsOn)))
                return Result<int>.Fail("Some positions start outside the new dates. Change their target start first.");
            p = found;
        }
        else
        {
            p = new HeadcountPlan();
            db.HeadcountPlans.Add(p);
        }
        p.Name = i.Name.Trim();
        p.StartsOn = i.StartsOn;
        p.EndsOn = i.EndsOn;
        p.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(p.Id);
    }

    public async Task<Result> DeletePlanAsync(int id, CancellationToken ct = default)
    {
        var p = await db.HeadcountPlans.Include(x => x.Positions).ThenInclude(x => x.Hires).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Fail("Plan not found.");
        if (p.Status != HeadcountPlanStatus.Draft) return Result.Fail("Only draft plans can be deleted. Close an approved plan instead.");
        foreach (var pos in p.Positions)
        {
            db.HeadcountHires.RemoveRange(pos.Hires);
            db.HeadcountPositions.Remove(pos);
        }
        db.HeadcountPlans.Remove(p);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetStatusAsync(int id, string step, CancellationToken ct = default)
    {
        var p = await db.HeadcountPlans.Include(x => x.Positions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Fail("Plan not found.");
        switch (step, p.Status)
        {
            case ("approve", HeadcountPlanStatus.Draft):
                if (!p.Positions.Any(x => !x.IsDeleted && x.Status != HeadcountPositionStatus.Cancelled)) return Result.Fail("Add at least one position before approving.");
                p.Status = HeadcountPlanStatus.Approved;
                p.ApprovedByUserId = context.UserId;
                p.ApprovedAtUtc = clock.UtcNow;
                break;
            case ("draft", HeadcountPlanStatus.Approved):
                p.Status = HeadcountPlanStatus.Draft;
                p.ApprovedByUserId = null;
                p.ApprovedAtUtc = null;
                break;
            case ("close", HeadcountPlanStatus.Approved):
                p.Status = HeadcountPlanStatus.Closed;
                break;
            case ("reopen", HeadcountPlanStatus.Closed):
                p.Status = HeadcountPlanStatus.Approved;
                break;
            default:
                return Result.Fail("That step isn't possible for this plan now.");
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<HeadcountPlanView?> ViewAsync(int id, bool payrollAllowed, CancellationToken ct = default)
    {
        var plan = await db.HeadcountPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return null;
        var today = await TodayAsync(ct);

        var positions = await db.HeadcountPositions.AsNoTracking().Where(x => x.PlanId == id).OrderBy(x => x.TargetStartOn).ThenBy(x => x.Title)
            .Select(x => new
            {
                x.Id, x.Title, x.DepartmentId, x.Openings, x.Kind, x.TargetStartOn, x.AnnualBudgetPerHire, x.Priority, x.Status, x.Notes,
                Department = db.Departments.Where(d => d.Id == x.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                Branch = db.Branches.Where(b => b.Id == x.BranchId).Select(b => b.Name).FirstOrDefault(),
                Designation = db.Designations.Where(d => d.Id == x.DesignationId).Select(d => d.Name).FirstOrDefault()
            }).ToListAsync(ct);
        var hires = await db.HeadcountHires.AsNoTracking().Where(h => h.PlanId == id)
            .Select(h => new { h.Id, h.PositionId, h.EmployeeId, h.Employee.FirstName, h.Employee.LastName, h.Employee.EmployeeCode, h.Employee.DateOfJoining }).ToListAsync(ct);

        // Committed cost: each hire's salary as of their joining date (or the earliest one recorded).
        Dictionary<int, decimal> gross = [];
        if (payrollAllowed && hires.Count > 0)
        {
            var ids = hires.Select(h => h.EmployeeId).ToList();
            var salaries = await db.EmployeeSalaries.AsNoTracking().Where(s => ids.Contains(s.EmployeeId)).Select(s => new { s.EmployeeId, s.EffectiveFrom, s.AnnualGross }).ToListAsync(ct);
            foreach (var h in hires)
            {
                var mine = salaries.Where(s => s.EmployeeId == h.EmployeeId).OrderBy(s => s.EffectiveFrom).ToList();
                var pick = mine.LastOrDefault(s => s.EffectiveFrom <= h.DateOfJoining) ?? mine.FirstOrDefault();
                if (pick is not null) gross[h.EmployeeId] = pick.AnnualGross;
            }
        }

        var rows = positions.Select(x => new HeadcountPositionRow(x.Id, x.Title, x.DepartmentId, x.Department ?? "—", x.Branch, x.Designation, x.Openings, x.Kind,
            x.TargetStartOn, x.AnnualBudgetPerHire, x.Priority, x.Status, x.Notes,
            hires.Where(h => h.PositionId == x.Id).OrderBy(h => h.DateOfJoining)
                .Select(h => new HeadcountHireRow(h.Id, h.EmployeeId, $"{h.FirstName} {h.LastName}", h.EmployeeCode, h.DateOfJoining, gross.TryGetValue(h.EmployeeId, out var g) ? g : null)).ToList())).ToList();

        var current = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .Select(e => new { e.Id, e.DepartmentId, e.DateOfJoining, e.DateOfExit }).ToListAsync(ct);
        var exitFrom = today > plan.StartsOn ? today : plan.StartsOn;
        var departmentIds = rows.Select(r => r.DepartmentId).Distinct().ToList();
        var departmentNames = await db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var summaries = departmentIds.Select(d =>
        {
            var mine = rows.Where(r => r.DepartmentId == d && r.Status != HeadcountPositionStatus.Cancelled).ToList();
            var people = current.Where(e => e.DepartmentId == d).ToList();
            var todayCount = people.Count(e => e.DateOfJoining <= today);
            var joiningSoon = people.Count(e => e.DateOfJoining > today && e.DateOfJoining <= plan.EndsOn);
            var exits = people.Count(e => e.DateOfExit is { } x && x >= exitFrom && x <= plan.EndsOn);
            var remaining = mine.Sum(r => r.Remaining);
            var filled = mine.Sum(r => Math.Min(r.Openings, r.Filled));
            decimal? committed = payrollAllowed ? mine.SelectMany(r => r.Hires).Sum(h => h.AnnualGross ?? 0) : null;
            return new HeadcountDepartmentSummary(d, departmentNames.GetValueOrDefault(d, "—"), todayCount, joiningSoon, exits,
                mine.Where(r => r.Kind == HeadcountPositionKind.NewRole).Sum(r => r.Openings), mine.Where(r => r.Kind == HeadcountPositionKind.Replacement).Sum(r => r.Openings),
                filled, remaining, todayCount + joiningSoon - exits + remaining, mine.Sum(r => r.Budget), committed);
        }).OrderBy(s => s.Department).ToList();

        var linked = hires.Select(h => h.EmployeeId).ToHashSet();
        var joiners = await db.Employees.AsNoTracking()
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && e.DateOfJoining >= plan.StartsOn && e.DateOfJoining <= plan.EndsOn)
            .OrderBy(e => e.DateOfJoining).Take(500)
            .Select(e => new HeadcountJoiner(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode, e.DepartmentId, e.Department != null ? e.Department.Name : null, e.DateOfJoining))
            .ToListAsync(ct);

        return new HeadcountPlanView
        {
            Plan = plan, Positions = rows, Departments = summaries, UnlinkedJoiners = joiners.Where(j => !linked.Contains(j.EmployeeId)).ToList(),
            ShowsCost = payrollAllowed, Today = today
        };
    }

    public async Task<HeadcountPositionInput?> GetPositionAsync(int planId, int positionId, CancellationToken ct = default) =>
        await db.HeadcountPositions.AsNoTracking().Where(x => x.Id == positionId && x.PlanId == planId).Select(x => new HeadcountPositionInput
        {
            Title = x.Title, DepartmentId = x.DepartmentId, BranchId = x.BranchId, DesignationId = x.DesignationId, Openings = x.Openings, Kind = x.Kind,
            TargetStartOn = x.TargetStartOn, AnnualBudgetPerHire = x.AnnualBudgetPerHire, Priority = x.Priority, Status = x.Status, Notes = x.Notes
        }).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> SavePositionAsync(int planId, int? positionId, HeadcountPositionInput i, CancellationToken ct = default)
    {
        var plan = await db.HeadcountPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result<int>.Fail("Plan not found.");
        if (plan.Status == HeadcountPlanStatus.Closed) return Result<int>.Fail("A closed plan can't be changed. Reopen it first.");
        if (i.TargetStartOn < plan.StartsOn || i.TargetStartOn > plan.EndsOn)
            return Result<int>.Fail($"The target start must be within the plan ({plan.StartsOn:dd MMM yyyy} – {plan.EndsOn:dd MMM yyyy}).");
        if (i.DepartmentId is not int dep || !await db.Departments.AnyAsync(d => d.Id == dep && d.IsActive, ct)) return Result<int>.Fail("Choose an active department.");
        if (i.BranchId is int br && !await db.Branches.AnyAsync(b => b.Id == br, ct)) return Result<int>.Fail("Branch not found.");
        if (i.DesignationId is int des && !await db.Designations.AnyAsync(d => d.Id == des && d.IsActive, ct)) return Result<int>.Fail("Job title not found.");

        HeadcountPosition pos;
        if (positionId is int existing)
        {
            var found = await db.HeadcountPositions.Include(x => x.Hires).FirstOrDefaultAsync(x => x.Id == existing && x.PlanId == planId, ct);
            if (found is null) return Result<int>.Fail("Position not found.");
            var hired = found.Hires.Count(h => !h.IsDeleted);
            if (i.Openings < hired) return Result<int>.Fail($"{hired} people are already linked to this position; openings can't go below that.");
            if (hired > 0 && found.DepartmentId != dep) return Result<int>.Fail("People are linked to this position, so its department can't change. Unlink them first.");
            if (hired > 0 && i.Status == HeadcountPositionStatus.Cancelled) return Result<int>.Fail("People are linked to this position. Reduce openings to the number hired instead of cancelling.");
            pos = found;
        }
        else
        {
            if (await db.HeadcountPositions.CountAsync(x => x.PlanId == planId, ct) >= MaxPositionsPerPlan) return Result<int>.Fail($"A plan can have at most {MaxPositionsPerPlan} positions.");
            pos = new HeadcountPosition { PlanId = planId };
            db.HeadcountPositions.Add(pos);
        }
        pos.Title = i.Title.Trim();
        pos.DepartmentId = dep;
        pos.BranchId = i.BranchId;
        pos.DesignationId = i.DesignationId;
        pos.Openings = i.Openings;
        pos.Kind = i.Kind;
        pos.TargetStartOn = i.TargetStartOn;
        pos.AnnualBudgetPerHire = Math.Round(i.AnnualBudgetPerHire, 2);
        pos.Priority = i.Priority;
        pos.Status = i.Status;
        pos.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(pos.Id);
    }

    public async Task<Result> DeletePositionAsync(int planId, int positionId, CancellationToken ct = default)
    {
        var pos = await db.HeadcountPositions.Include(x => x.Hires).Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == positionId && x.PlanId == planId, ct);
        if (pos is null) return Result.Fail("Position not found.");
        if (pos.Plan.Status == HeadcountPlanStatus.Closed) return Result.Fail("A closed plan can't be changed. Reopen it first.");
        if (pos.Hires.Any(h => !h.IsDeleted)) return Result.Fail("People are linked to this position. Unlink them first.");
        db.HeadcountPositions.Remove(pos);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> LinkHireAsync(int planId, int positionId, int employeeId, CancellationToken ct = default)
    {
        var pos = await db.HeadcountPositions.Include(x => x.Hires).Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == positionId && x.PlanId == planId, ct);
        if (pos is null) return Result.Fail("Position not found.");
        if (pos.Plan.Status == HeadcountPlanStatus.Closed) return Result.Fail("A closed plan can't be changed. Reopen it first.");
        if (pos.Status == HeadcountPositionStatus.Cancelled) return Result.Fail("This position is cancelled.");
        if (pos.Hires.Count(h => !h.IsDeleted) >= pos.Openings) return Result.Fail("All openings of this position are filled.");
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null || !e.IsCurrent) return Result.Fail("Choose a current employee.");
        if (e.DateOfJoining < pos.Plan.StartsOn || e.DateOfJoining > pos.Plan.EndsOn) return Result.Fail("Only people who join during the plan period can fill its positions.");
        if (e.DepartmentId != pos.DepartmentId) return Result.Fail("The employee is in a different department from the position.");
        if (await db.HeadcountHires.AnyAsync(h => h.PlanId == planId && h.EmployeeId == employeeId, ct)) return Result.Fail("This employee already fills a position in this plan.");
        db.HeadcountHires.Add(new HeadcountHire { PositionId = pos.Id, PlanId = planId, EmployeeId = employeeId });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UnlinkHireAsync(int planId, int hireId, CancellationToken ct = default)
    {
        var h = await db.HeadcountHires.Include(x => x.Position).ThenInclude(x => x.Plan).FirstOrDefaultAsync(x => x.Id == hireId && x.PlanId == planId, ct);
        if (h is null) return Result.Fail("Link not found.");
        if (h.Position.Plan.Status == HeadcountPlanStatus.Closed) return Result.Fail("A closed plan can't be changed. Reopen it first.");
        db.HeadcountHires.Remove(h);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<byte[]?> ExportAsync(int id, bool payrollAllowed, CancellationToken ct = default)
    {
        var v = await ViewAsync(id, payrollAllowed, ct);
        if (v is null) return null;
        List<string> headers = ["Department", "Role", "Job title", "Branch", "Type", "Priority", "Status", "Target start", "Openings", "Filled", "Remaining", "Budget per hire", "Budget"];
        if (payrollAllowed) headers.Add("Committed (hires' salaries)");
        headers.Add("Hired");
        var rows = v.Positions.OrderBy(p => p.Department).ThenBy(p => p.TargetStartOn).Select(p =>
        {
            List<object?> r = [p.Department, p.Title, p.Designation, p.Branch, p.Kind == HeadcountPositionKind.NewRole ? "New role" : "Replacement", p.Priority.ToString(),
                p.Status == HeadcountPositionStatus.OnHold ? "On hold" : p.Status.ToString(), p.TargetStartOn, p.Openings, p.Filled, p.Remaining, p.BudgetPerHire, p.Budget];
            if (payrollAllowed) r.Add(p.Hires.Sum(h => h.AnnualGross ?? 0));
            r.Add(string.Join(", ", p.Hires.Select(h => $"{h.Name} ({h.Code})")));
            return (IReadOnlyList<object?>)r;
        });
        return excel.Export("Headcount plan", headers, rows);
    }
}
