using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

/// <summary>Starting leave types and shift for a new organization; all editable.</summary>
public static class TimeDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        db.LeaveTypes.AddRange(
            new LeaveType { OrganizationId = organizationId, Name = "Casual Leave", Code = "CL", Color = "#0ea5e9", AnnualQuota = 12, AccrualMethod = AccrualMethod.AnnualUpfront, MinNoticeDays = 1, SortOrder = 10 },
            new LeaveType { OrganizationId = organizationId, Name = "Sick Leave", Code = "SL", Color = "#f97316", AnnualQuota = 12, AccrualMethod = AccrualMethod.AnnualUpfront, SortOrder = 20 },
            new LeaveType { OrganizationId = organizationId, Name = "Earned Leave", Code = "EL", Color = "#22c55e", AnnualQuota = 15, AccrualMethod = AccrualMethod.Monthly, CarryForwardLimit = 30, MaxEncashableDaysPerYear = 15, AllowHalfDay = false, MinNoticeDays = 7, SortOrder = 30 },
            new LeaveType { OrganizationId = organizationId, Name = "Loss of Pay", Code = "LOP", Color = "#94a3b8", IsPaid = false, AnnualQuota = 0, SortOrder = 90 });
        db.Shifts.Add(new Shift
        {
            OrganizationId = organizationId, Name = "General", Code = "GEN", StartTime = new TimeOnly(9, 30), EndTime = new TimeOnly(18, 30),
            BreakMinutes = 60, GraceMinutes = 10, MinHoursFullDay = 8, MinHoursHalfDay = 4, OvertimeThresholdMinutes = 30, WeeklyOffs = "Sunday", IsDefault = true
        });
    }
}

public sealed class ShiftInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(10), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Letters and digits only.")] public string Code { get; set; } = string.Empty;
    [Required, Display(Name = "Start")] public TimeOnly StartTime { get; set; } = new(9, 30);
    [Required, Display(Name = "End")] public TimeOnly EndTime { get; set; } = new(18, 30);
    [Range(0, 240), Display(Name = "Break (minutes)")] public int BreakMinutes { get; set; } = 60;
    [Range(0, 120), Display(Name = "Grace (minutes)")] public int GraceMinutes { get; set; } = 10;
    [Range(1, 24), Display(Name = "Full day after (hours)")] public decimal MinHoursFullDay { get; set; } = 8;
    [Range(0.5, 24), Display(Name = "Half day after (hours)")] public decimal MinHoursHalfDay { get; set; } = 4;
    [Range(0, 600), Display(Name = "Overtime threshold (minutes)")] public int OvertimeThresholdMinutes { get; set; } = 30;
    [Display(Name = "Weekly offs")] public List<DayOfWeek> WeeklyOffs { get; set; } = [DayOfWeek.Sunday];
    [Display(Name = "Default shift")] public bool IsDefault { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed record ShiftRow(int Id, string Name, string Code, TimeOnly Start, TimeOnly End, int BreakMinutes, int GraceMinutes,
    string WeeklyOffs, bool IsDefault, bool IsActive, int AssignedEmployees);

public sealed record ShiftAssignmentRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string ShiftName, DateOnly From, DateOnly? To);

public interface IShiftService
{
    Task<IReadOnlyList<ShiftRow>> ShiftsAsync(CancellationToken ct = default);
    Task<ShiftInput?> GetAsync(int id, CancellationToken ct = default);
    Task<Result> SaveAsync(int? id, ShiftInput input, CancellationToken ct = default);
    Task<Result> AssignAsync(IReadOnlyCollection<int> employeeIds, int shiftId, DateOnly effectiveFrom, CancellationToken ct = default);
    Task<IReadOnlyList<ShiftAssignmentRow>> CurrentAssignmentsAsync(CancellationToken ct = default);
}

public sealed class ShiftService(IAppDbContext db, IAttendanceService attendance) : IShiftService
{
    public async Task<IReadOnlyList<ShiftRow>> ShiftsAsync(CancellationToken ct = default)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        return await db.Shifts.AsNoTracking().OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name)
            .Select(s => new ShiftRow(s.Id, s.Name, s.Code, s.StartTime, s.EndTime, s.BreakMinutes, s.GraceMinutes, s.WeeklyOffs, s.IsDefault, s.IsActive,
                db.ShiftAssignments.Count(a => a.ShiftId == s.Id && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))))
            .ToListAsync(ct);
    }

    public async Task<ShiftInput?> GetAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new ShiftInput
        {
            Name = s.Name, Code = s.Code, StartTime = s.StartTime, EndTime = s.EndTime, BreakMinutes = s.BreakMinutes, GraceMinutes = s.GraceMinutes,
            MinHoursFullDay = s.MinHoursFullDay, MinHoursHalfDay = s.MinHoursHalfDay, OvertimeThresholdMinutes = s.OvertimeThresholdMinutes,
            WeeklyOffs = s.WeeklyOffs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Enum.Parse<DayOfWeek>).ToList(),
            IsDefault = s.IsDefault, IsActive = s.IsActive
        };
    }

    public async Task<Result> SaveAsync(int? id, ShiftInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Shifts.AnyAsync(s => s.Code == code && s.Id != id, ct)) return Result.Fail($"Shift code {code} is already used.");
        if (input.MinHoursHalfDay >= input.MinHoursFullDay) return Result.Fail("Half-day hours must be less than full-day hours.");
        if (input.WeeklyOffs.Distinct().Count() >= 7) return Result.Fail("At least one working day is required.");
        var probe = new Shift { StartTime = input.StartTime, EndTime = input.EndTime, BreakMinutes = input.BreakMinutes };
        if (probe.ScheduledMinutes <= 0) return Result.Fail("The break is longer than the shift.");
        if (input.MinHoursFullDay * 60 > probe.ScheduledMinutes + input.BreakMinutes) return Result.Fail("Full-day hours exceed the shift length.");

        Shift? s;
        if (id is null) s = db.Shifts.Add(new Shift()).Entity;
        else
        {
            s = await db.Shifts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) return Result.Fail("Shift not found.");
            if (!input.IsActive && s.IsDefault) return Result.Fail("The default shift cannot be deactivated. Make another shift the default first.");
        }
        s.Name = input.Name.Trim();
        s.Code = code;
        s.StartTime = input.StartTime;
        s.EndTime = input.EndTime;
        s.BreakMinutes = input.BreakMinutes;
        s.GraceMinutes = input.GraceMinutes;
        s.MinHoursFullDay = input.MinHoursFullDay;
        s.MinHoursHalfDay = input.MinHoursHalfDay;
        s.OvertimeThresholdMinutes = input.OvertimeThresholdMinutes;
        s.WeeklyOffs = string.Join(",", input.WeeklyOffs.Distinct().OrderBy(d => d));
        s.IsActive = input.IsActive;
        if (input.IsDefault)
        {
            foreach (var other in await db.Shifts.Where(x => x.IsDefault && x.Id != s.Id).ToListAsync(ct)) other.IsDefault = false;
            s.IsDefault = true;
        }
        else if (s.IsDefault && !await db.Shifts.AnyAsync(x => x.IsDefault && x.Id != s.Id, ct) && id is not null)
            return Result.Fail("One shift must remain the default.");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AssignAsync(IReadOnlyCollection<int> employeeIds, int shiftId, DateOnly effectiveFrom, CancellationToken ct = default)
    {
        if (employeeIds.Count == 0) return Result.Fail("Select at least one employee.");
        if (!await db.Shifts.AnyAsync(s => s.Id == shiftId && s.IsActive, ct)) return Result.Fail("Shift not found or inactive.");
        var today = await attendance.TodayForOrganizationAsync(ct);
        if (effectiveFrom < today.AddDays(-31)) return Result.Fail("Shifts can be back-dated by at most 31 days.");
        var valid = await db.Employees.Where(e => employeeIds.Contains(e.Id) && e.Status != EmployeeStatus.Exited).Select(e => e.Id).ToListAsync(ct);
        if (valid.Count != employeeIds.Distinct().Count()) return Result.Fail("Some selected employees were not found.");

        var affected = await db.ShiftAssignments.Where(a => valid.Contains(a.EmployeeId) && (a.EffectiveTo == null || a.EffectiveTo >= effectiveFrom)).ToListAsync(ct);
        foreach (var a in affected)
        {
            if (a.EffectiveFrom >= effectiveFrom) db.ShiftAssignments.Remove(a); // superseded entirely
            else a.EffectiveTo = effectiveFrom.AddDays(-1);
        }
        foreach (var id in valid) db.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = id, ShiftId = shiftId, EffectiveFrom = effectiveFrom });
        await db.SaveChangesAsync(ct);
        if (effectiveFrom <= today) await attendance.RecalculateRangeAsync(valid, effectiveFrom, today, ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ShiftAssignmentRow>> CurrentAssignmentsAsync(CancellationToken ct = default)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        return await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EffectiveTo == null || a.EffectiveTo >= today)
            .OrderBy(a => a.Employee.FirstName)
            .Select(a => new ShiftAssignmentRow(a.Id, a.EmployeeId, a.Employee.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode, a.Shift.Name, a.EffectiveFrom, a.EffectiveTo))
            .ToListAsync(ct);
    }
}

public sealed record OvertimeRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, DateOnly Date, int Minutes, int? ApprovedMinutes,
    string Reason, OvertimeStatus Status, string? DecidedBy, string? DecisionNotes);

public interface IOvertimeService
{
    Task<Result> RequestAsync(int employeeId, DateOnly date, string reason, CancellationToken ct = default);
    Task<Result> DecideAsync(int id, bool approve, int? approvedMinutes, string? notes, bool asHr, CancellationToken ct = default);
    Task<IReadOnlyList<OvertimeRow>> PendingForApproverAsync(int userId, CancellationToken ct = default);
    Task<PagedResult<OvertimeRow>> ListAsync(OvertimeStatus? status, int page, CancellationToken ct = default);
    Task<IReadOnlyList<OvertimeRow>> ForEmployeeAsync(int employeeId, int year, int month, CancellationToken ct = default);
}

public sealed class OvertimeService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IOvertimeService
{
    public async Task<Result> RequestAsync(int employeeId, DateOnly date, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return Result.Fail("Give a reason (max 500 characters).");
        var day = await db.Attendance.AsNoTracking().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date, ct);
        if (day is null || day.OvertimeMinutes <= 0) return Result.Fail("No overtime is recorded for this date.");
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        if (date >= today) return Result.Fail("Overtime can be claimed once the day is over.");
        if (date < today.AddDays(-45)) return Result.Fail("Overtime must be claimed within 45 days.");
        if (await db.OvertimeRequests.AnyAsync(o => o.EmployeeId == employeeId && o.Date == date && o.Status != OvertimeStatus.Rejected, ct))
            return Result.Fail("Overtime for this date has already been claimed.");

        var approver = await db.ApproverForAsync(employeeId, ct);
        db.OvertimeRequests.Add(new OvertimeRequest { EmployeeId = employeeId, Date = date, Minutes = day.OvertimeMinutes, Reason = reason.Trim(), ApproverUserId = approver });
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
        var recipients = approver is int a ? [a] : await notifications.UsersWithPermissionAsync(TenantPermissions.AttendanceManage, ct);
        notifications.Queue(recipients, "Overtime claim", $"{name} claimed {day.OvertimeMinutes / 60.0:0.#} h overtime for {date:dd MMM}.", approver is null ? "/org/attendance#overtime" : "/me/approvals");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DecideAsync(int id, bool approve, int? approvedMinutes, string? notes, bool asHr, CancellationToken ct = default)
    {
        var req = await db.OvertimeRequests.Include(o => o.Employee).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (req is null) return Result.Fail("Overtime request not found.");
        if (req.Status != OvertimeStatus.Pending) return Result.Fail("This request has already been decided.");
        if (!asHr && req.ApproverUserId != context.UserId) return Result.Fail("You are not the approver for this request.");
        if (req.Employee.UserId == context.UserId) return Result.Fail("You cannot decide your own request.");
        if (!approve && string.IsNullOrWhiteSpace(notes)) return Result.Fail("Please give a reason for rejecting.");
        if (approvedMinutes is { } m && (m <= 0 || m > req.Minutes)) return Result.Fail($"Approved minutes must be between 1 and {req.Minutes}.");

        req.Status = approve ? OvertimeStatus.Approved : OvertimeStatus.Rejected;
        req.ApprovedMinutes = approve ? approvedMinutes ?? req.Minutes : null;
        req.DecidedBy = context.UserName;
        req.DecidedAtUtc = clock.UtcNow;
        req.DecisionNotes = notes?.Trim();
        if (req.Employee.UserId is int uid)
            notifications.Queue([uid], $"Overtime {(approve ? "approved" : "rejected")}", $"Your overtime for {req.Date:dd MMM} was {(approve ? "approved" : "rejected")}.", "/me/attendance");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static IQueryable<OvertimeRow> Project(IQueryable<OvertimeRequest> q) =>
        q.Select(o => new OvertimeRow(o.Id, o.EmployeeId, o.Employee.FirstName + " " + o.Employee.LastName, o.Employee.EmployeeCode, o.Date,
            o.Minutes, o.ApprovedMinutes, o.Reason, o.Status, o.DecidedBy, o.DecisionNotes));

    public async Task<IReadOnlyList<OvertimeRow>> PendingForApproverAsync(int userId, CancellationToken ct = default) =>
        await Project(db.OvertimeRequests.AsNoTracking().Where(o => o.Status == OvertimeStatus.Pending && o.ApproverUserId == userId).OrderBy(o => o.Date)).ToListAsync(ct);

    public async Task<PagedResult<OvertimeRow>> ListAsync(OvertimeStatus? status, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.OvertimeRequests.AsNoTracking();
        if (status is { } s) q = q.Where(o => o.Status == s);
        var total = await q.CountAsync(ct);
        return new PagedResult<OvertimeRow>(await Project(q.OrderByDescending(o => o.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), total, page, size);
    }

    public async Task<IReadOnlyList<OvertimeRow>> ForEmployeeAsync(int employeeId, int year, int month, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return await Project(db.OvertimeRequests.AsNoTracking().Where(o => o.EmployeeId == employeeId && o.Date >= from && o.Date <= to).OrderBy(o => o.Date)).ToListAsync(ct);
    }
}

public sealed record Badge(string Name, string Icon, string Description);
public sealed record AttendanceScore(int EmployeeId, string Name, string Code, string? Department, int Points, int OnTimeDays, int LateDays, int AbsentDays, int Streak, IReadOnlyList<Badge> Badges);

public interface IGamificationService
{
    Task<IReadOnlyList<AttendanceScore>> LeaderboardAsync(int year, int month, int top = 10, CancellationToken ct = default);
    Task<AttendanceScore?> ForEmployeeAsync(int employeeId, CancellationToken ct = default);
}

/// <summary>Streaks, punctuality points, badges and leaderboards derived from calculated attendance.</summary>
public sealed class GamificationService(IAppDbContext db, IAttendanceService attendance) : IGamificationService
{
    public static readonly Badge EarlyBird = new("Early Bird", "bi-sunrise", "5+ on-time days this month");
    public static readonly Badge PunctualityPro = new("Punctuality Pro", "bi-stopwatch", "10 working days on time in a row");
    public static readonly Badge IronAttendance = new("Iron Attendance", "bi-shield-check", "No absences this month (10+ working days)");
    public static readonly Badge PerfectMonth = new("Perfect Month", "bi-trophy", "On time every working day this month (15+ days)");

    public async Task<IReadOnlyList<AttendanceScore>> LeaderboardAsync(int year, int month, int top = 10, CancellationToken ct = default) =>
        (await ScoresAsync(year, month, null, ct)).OrderByDescending(s => s.Points).ThenByDescending(s => s.Streak).ThenBy(s => s.Name).Take(top).ToList();

    public async Task<AttendanceScore?> ForEmployeeAsync(int employeeId, CancellationToken ct = default)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        return (await ScoresAsync(today.Year, today.Month, [employeeId], ct)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<AttendanceScore>> ScoresAsync(int year, int month, IReadOnlyCollection<int>? employeeIds, CancellationToken ct)
    {
        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OrganizationSetting();
        var today = await attendance.TodayForOrganizationAsync(ct);
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        if (to > today) to = today;
        await attendance.RecalculateRangeAsync(employeeIds, from.AddDays(-45), to, ct); // streaks may reach into last month

        var data = await db.Attendance.AsNoTracking()
            .Where(a => a.Date >= from.AddDays(-45) && a.Date <= to && a.Employee.Status != EmployeeStatus.Exited && (employeeIds == null || employeeIds.Contains(a.EmployeeId)))
            .Select(a => new { a.EmployeeId, a.Date, a.Status, a.LateMinutes, Name = a.Employee.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode, Dept = a.Employee.Department != null ? a.Employee.Department.Name : null })
            .ToListAsync(ct);

        return data.GroupBy(d => d.EmployeeId).Select(g =>
        {
            var month_ = g.Where(d => d.Date >= from).ToList();
            bool Working(AttendanceStatus s) => s is AttendanceStatus.Present or AttendanceStatus.HalfDay or AttendanceStatus.Absent;
            var onTime = month_.Count(d => d.Status == AttendanceStatus.Present && d.LateMinutes == 0);
            var late = month_.Count(d => d.Status is AttendanceStatus.Present or AttendanceStatus.HalfDay && d.LateMinutes > 0);
            var absent = month_.Count(d => d.Status == AttendanceStatus.Absent);
            var workingDays = month_.Count(d => Working(d.Status) && d.Date < today);

            // Streak: consecutive on-time working days, newest first; offs, holidays, leave and today (in progress) are neutral.
            var streak = 0;
            foreach (var d in g.OrderByDescending(x => x.Date))
            {
                if (d.Date == today && d.Status is AttendanceStatus.NotMarked) continue;
                if (!Working(d.Status)) continue;
                if (d.Status == AttendanceStatus.Present && d.LateMinutes == 0) streak++;
                else break;
            }

            var badges = new List<Badge>();
            if (onTime >= 5) badges.Add(EarlyBird);
            if (streak >= 10) badges.Add(PunctualityPro);
            if (absent == 0 && workingDays >= 10) badges.Add(IronAttendance);
            if (workingDays >= 15 && onTime >= workingDays) badges.Add(PerfectMonth);

            var first = g.First();
            return new AttendanceScore(g.Key, first.Name, first.EmployeeCode, first.Dept,
                onTime * settings.OnTimePoints + late * settings.LatePoints, onTime, late, absent, streak, badges);
        }).ToList();
    }
}
