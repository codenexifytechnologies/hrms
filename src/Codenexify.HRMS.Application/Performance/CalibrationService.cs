using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed record CalibrationEntry(int ReviewId, int EmployeeId, string EmployeeName, string? Department, string? Reviewer, int? ManagerRating, int Rating, bool Adjusted, string? AdjustmentNote);
public sealed record CalibrationColumn(int Rating, string Label, IReadOnlyList<CalibrationEntry> Entries, decimal? TargetPercent)
{
    public int Count => Entries.Count;
}
public sealed record CalibrationBoard(ReviewCycle Cycle, IReadOnlyList<CalibrationColumn> Columns, int Total, int NotYetRated, bool CanAdjust,
    IReadOnlyList<LookupPair> Departments, IReadOnlyList<LookupPair> Reviewers)
{
    public decimal Percent(CalibrationColumn c) => Total == 0 ? 0 : Math.Round(c.Count * 100m / Total, 1);
}
public sealed record LookupPair(int Id, string Name);

public interface ICalibrationService
{
    Task<CalibrationBoard?> BoardAsync(int reviewCycleId, int? departmentId, int? reviewerId, CancellationToken ct = default);
    /// <param name="percents">Index 0 = rating 1 … index 4 = rating 5; all zero clears the targets.</param>
    Task<Result> SetTargetsAsync(int reviewCycleId, IReadOnlyList<decimal> percents, CancellationToken ct = default);
}

public sealed class CalibrationService(IAppDbContext db) : ICalibrationService
{
    public async Task<CalibrationBoard?> BoardAsync(int reviewCycleId, int? departmentId, int? reviewerId, CancellationToken ct = default)
    {
        var cycle = await db.ReviewCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == reviewCycleId, ct);
        if (cycle is null || cycle.Status == ReviewCycleStatus.Draft) return null;
        var labels = cycle.RatingLabels.Split('|', StringSplitOptions.TrimEntries);
        var targets = await db.CalibrationTargets.AsNoTracking().Where(t => t.ReviewCycleId == reviewCycleId).ToDictionaryAsync(t => t.Rating, t => t.Percent, ct);

        var all = await (from r in db.PerformanceReviews.AsNoTracking().Where(r => r.ReviewCycleId == reviewCycleId)
                         join e in db.Employees on r.EmployeeId equals e.Id
                         join d in db.Departments on e.DepartmentId equals d.Id into ds
                         from dept in ds.DefaultIfEmpty()
                         join m in db.Employees on r.ReviewerEmployeeId equals m.Id into ms
                         from mg in ms.DefaultIfEmpty()
                         select new
                         {
                             r.Id, r.EmployeeId, Name = e.FirstName + " " + e.LastName, DeptId = e.DepartmentId, Dept = dept == null ? null : dept.Name,
                             r.ReviewerEmployeeId, Reviewer = mg == null ? null : mg.FirstName + " " + mg.LastName, r.OverallRating, r.FinalRating, r.AdjustmentNote, r.ManagerSubmittedAtUtc
                         }).ToListAsync(ct);
        var departments = all.Where(a => a.DeptId != null).Select(a => new LookupPair(a.DeptId!.Value, a.Dept!)).DistinctBy(x => x.Id).OrderBy(x => x.Name).ToList();
        var reviewers = all.Where(a => a.ReviewerEmployeeId != null).Select(a => new LookupPair(a.ReviewerEmployeeId!.Value, a.Reviewer!)).DistinctBy(x => x.Id).OrderBy(x => x.Name).ToList();

        var scoped = all.Where(a => (departmentId == null || a.DeptId == departmentId) && (reviewerId == null || a.ReviewerEmployeeId == reviewerId)).ToList();
        var rated = scoped.Where(a => a.ManagerSubmittedAtUtc != null && (a.FinalRating ?? a.OverallRating) is >= 1 and <= 5).ToList();
        var columns = Enumerable.Range(1, 5).Reverse().Select(v => new CalibrationColumn(v, labels.ElementAtOrDefault(v - 1) ?? v.ToString(),
            rated.Where(a => (a.FinalRating ?? a.OverallRating) == v)
                .Select(a => new CalibrationEntry(a.Id, a.EmployeeId, a.Name, a.Dept, a.Reviewer, a.OverallRating, v, a.FinalRating is not null, a.AdjustmentNote))
                .OrderBy(x => x.EmployeeName).ToList(),
            targets.TryGetValue(v, out var t) ? t : null)).ToList();
        return new CalibrationBoard(cycle, columns, rated.Count, scoped.Count - rated.Count, cycle.Status == ReviewCycleStatus.Active, departments, reviewers);
    }

    public async Task<Result> SetTargetsAsync(int reviewCycleId, IReadOnlyList<decimal> percents, CancellationToken ct = default)
    {
        if (percents.Count != 5 || percents.Any(p => p is < 0 or > 100)) return Result.Fail("Enter a percentage for each of the five ratings.");
        var sum = percents.Sum();
        if (sum != 0 && sum != 100) return Result.Fail($"The targets add up to {sum}%; they must add up to 100%.");
        if (!await db.ReviewCycles.AnyAsync(c => c.Id == reviewCycleId, ct)) return Result.Fail("Review cycle not found.");
        var existing = await db.CalibrationTargets.Where(t => t.ReviewCycleId == reviewCycleId).ToListAsync(ct);
        db.CalibrationTargets.RemoveRange(existing);
        if (sum == 100)
            for (var i = 0; i < 5; i++) db.CalibrationTargets.Add(new CalibrationTarget { ReviewCycleId = reviewCycleId, Rating = i + 1, Percent = percents[i] });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
