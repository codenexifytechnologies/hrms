using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

/// <summary>Converts between UTC and the organization's local time.</summary>
public sealed class OrgClock(TimeZoneInfo zone, IClock clock)
{
    public TimeZoneInfo Zone { get; } = zone;
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, Zone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public DateTime ToUtc(DateOnly date, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);

    public static TimeZoneInfo Resolve(string? id)
    {
        foreach (var candidate in new[] { id, "India Standard Time", "Asia/Kolkata" })
            if (!string.IsNullOrWhiteSpace(candidate) && TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
    }
}

/// <summary>
/// Resolves, for one organization, which shift an employee works on a date, whether the date is a weekly off
/// or holiday, and therefore whether it is a working day. Data is loaded once per instance.
/// </summary>
public sealed class WorkCalendar
{
    private readonly List<Holiday> _holidays;
    private readonly List<ShiftAssignment> _assignments;
    private readonly Shift? _defaultShift;
    private readonly Dictionary<int, int?> _branchOf;

    private WorkCalendar(List<Holiday> holidays, List<ShiftAssignment> assignments, Shift? defaultShift, Dictionary<int, int?> branchOf)
    {
        _holidays = holidays;
        _assignments = assignments;
        _defaultShift = defaultShift;
        _branchOf = branchOf;
    }

    public static async Task<WorkCalendar> LoadAsync(IAppDbContext db, DateOnly from, DateOnly to, IEnumerable<int>? employeeIds, CancellationToken ct)
    {
        var ids = employeeIds?.Distinct().ToList();
        var holidays = await db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to && !h.IsOptional).ToListAsync(ct);
        var assignments = await db.ShiftAssignments.AsNoTracking().Include(a => a.Shift)
            .Where(a => a.EffectiveFrom <= to && (a.EffectiveTo == null || a.EffectiveTo >= from) && (ids == null || ids.Contains(a.EmployeeId)))
            .ToListAsync(ct);
        var defaultShift = await db.Shifts.AsNoTracking().Where(s => s.IsActive).OrderByDescending(s => s.IsDefault).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        var branchOf = await db.Employees.AsNoTracking().Where(e => ids == null || ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.BranchId, ct);
        return new WorkCalendar(holidays, assignments, defaultShift, branchOf);
    }

    public Shift? ShiftFor(int employeeId, DateOnly date) =>
        _assignments.Where(a => a.EmployeeId == employeeId && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
            .OrderByDescending(a => a.EffectiveFrom).Select(a => a.Shift).FirstOrDefault() ?? _defaultShift;

    public Holiday? HolidayFor(int employeeId, DateOnly date)
    {
        var branch = _branchOf.GetValueOrDefault(employeeId);
        return _holidays.FirstOrDefault(h => h.Date == date && (h.BranchId == null || h.BranchId == branch));
    }

    public bool IsWeeklyOff(int employeeId, DateOnly date) =>
        ShiftFor(employeeId, date) is { } s ? s.IsWeeklyOff(date.DayOfWeek) : date.DayOfWeek == DayOfWeek.Sunday;

    public bool IsWorkingDay(int employeeId, DateOnly date) => !IsWeeklyOff(employeeId, date) && HolidayFor(employeeId, date) is null;

    public decimal WorkingDays(int employeeId, DateOnly from, DateOnly to)
    {
        decimal days = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (IsWorkingDay(employeeId, d)) days++;
        return days;
    }
}

internal static class TimeContext
{
    public static int OrgId(this ICurrentContext context) =>
        context.OrganizationId ?? throw new TenantIsolationException("No organization in the current context.");

    public static async Task<OrgClock> OrgClockAsync(this IAppDbContext db, int orgId, IClock clock, CancellationToken ct)
    {
        var tz = await db.Organizations.AsNoTracking().Where(o => o.Id == orgId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        return new OrgClock(OrgClock.Resolve(tz), clock);
    }

    /// <summary>Reporting manager's active login decides; null means HR decides.</summary>
    public static async Task<int?> ApproverForAsync(this IAppDbContext db, int employeeId, CancellationToken ct) =>
        await db.Employees.AsNoTracking().Where(e => e.Id == employeeId && e.ReportingManager != null
                && e.ReportingManager.Status != EmployeeStatus.Exited && e.ReportingManager.User != null && e.ReportingManager.User.IsActive)
            .Select(e => e.ReportingManager!.UserId).FirstOrDefaultAsync(ct);
}
