using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

public sealed class RegularizeInput
{
    [Required] public DateOnly Date { get; set; }
    [Required] public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    [Display(Name = "In time")] public TimeOnly? InTime { get; set; }
    [Display(Name = "Out time")] public TimeOnly? OutTime { get; set; }
    [Required, StringLength(300)] public string Remarks { get; set; } = string.Empty;
}

public sealed record PunchResult(PunchDirection Direction, DateTime LocalTime, DateOnly AttendanceDate);

public sealed record TodayStatus(DateOnly Date, bool IsClockedIn, int WorkedMinutes, AttendanceStatus Status, string? ShiftName,
    TimeOnly? ShiftStart, TimeOnly? ShiftEnd, IReadOnlyList<(PunchDirection Direction, DateTime LocalTime)> Punches, bool IsWorkingDay, string? HolidayName);

public sealed record AttendanceRow(int EmployeeId, string Name, string Code, string? Department, AttendanceStatus Status,
    DateTime? FirstInLocal, DateTime? LastOutLocal, int WorkedMinutes, int LateMinutes, int OvertimeMinutes, bool IsRegularized, string? Remarks);

public sealed record RegisterRow(int EmployeeId, string Name, string Code, string? Department, IReadOnlyDictionary<DateOnly, AttendanceStatus> Days,
    decimal Present, decimal Absent, decimal Leave, int LateDays, int OvertimeMinutes);

public sealed class AttendanceRegister
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required IReadOnlyList<DateOnly> Dates { get; init; }
    public required IReadOnlyList<RegisterRow> Rows { get; init; }
}

public sealed record DaySummary(int Present, int HalfDay, int Absent, int OnLeave, int Late, int NotMarked, int Off);

public interface IAttendanceService
{
    Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, CancellationToken ct = default);
    /// <param name="location">Device location from the browser/app, if the employee allowed it.</param>
    Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, GeoPoint? location, CancellationToken ct = default);
    /// <param name="selfie">JPEG photo from the device camera (required when the organization switched selfie clock-in on).</param>
    Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, GeoPoint? location, byte[]? selfie, CancellationToken ct = default);
    /// <summary>Whether this organization asks for a photo with each self-service punch.</summary>
    Task<bool> SelfieRequiredAsync(CancellationToken ct = default);
    Task<TodayStatus> TodayAsync(int employeeId, CancellationToken ct = default);
    /// <summary>Recomputes daily attendance (skips regularized days). Null employee ids = all employees.</summary>
    Task RecalculateRangeAsync(IReadOnlyCollection<int>? employeeIds, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<(IReadOnlyList<AttendanceRow> Rows, DaySummary Summary)> DayAsync(DateOnly date, int? departmentId, string? search, CancellationToken ct = default);
    Task<AttendanceRegister> RegisterAsync(int year, int month, int? departmentId, IReadOnlyCollection<int>? employeeIds = null, CancellationToken ct = default);
    Task<Result> RegularizeAsync(int employeeId, RegularizeInput input, CancellationToken ct = default);
    Task<Result> ClearRegularizationAsync(int employeeId, DateOnly date, CancellationToken ct = default);
    Task<ExportFile> ExportRegisterAsync(int year, int month, int? departmentId, CancellationToken ct = default);
    Task<DateOnly> TodayForOrganizationAsync(CancellationToken ct = default);
}

public sealed class AttendanceService(IAppDbContext db, ICurrentContext context, IClock clock, IExcelExporter excel, Subscriptions.IFeatureAccessService features,
    IFileStorage storage) : IAttendanceService
{
    /// <summary>Camera photos are downscaled in the browser; anything bigger is not a clock-in selfie.</summary>
    public const int MaxSelfieBytes = 400 * 1024;
    /// <summary>Night-shift punches up to this long after the shift end belong to the previous day.</summary>
    private static readonly TimeSpan NightShiftSpill = TimeSpan.FromHours(4);
    private int OrgId => context.OrgId();

    public async Task<DateOnly> TodayForOrganizationAsync(CancellationToken ct = default) => (await db.OrgClockAsync(OrgId, clock, ct)).Today;

    public Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, CancellationToken ct = default) =>
        PunchAsync(employeeId, source, ipAddress, null, ct);

    public Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, GeoPoint? location, CancellationToken ct = default) =>
        PunchAsync(employeeId, source, ipAddress, location, null, ct);

    public async Task<bool> SelfieRequiredAsync(CancellationToken ct = default) =>
        (await features.GetEnabledFeaturesAsync(OrgId, ct)).Contains(Catalog.FeatureCodes.SelfieClockIn)
        && await db.OrganizationSettings.AsNoTracking().Select(s => s.SelfieRequired).FirstOrDefaultAsync(ct);

    private static bool IsJpeg(byte[] b) => b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF && b[^2] == 0xFF && b[^1] == 0xD9;

    public async Task<Result<PunchResult>> PunchAsync(int employeeId, PunchSource source, string? ipAddress, GeoPoint? location, byte[]? selfie, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return Result<PunchResult>.Fail("Employee not found.");
        if (!employee.IsCurrent) return Result<PunchResult>.Fail("Former employees cannot mark attendance.");

        // Selfie clock-in: a photo is required (and kept) only when the plan includes it and the organization switched it on.
        var keepSelfie = await SelfieRequiredAsync(ct);
        if (keepSelfie)
        {
            if (selfie is not { Length: > 0 }) return Result<PunchResult>.Fail("A photo is needed to clock in. Allow camera access and try again.");
            if (selfie.Length > MaxSelfieBytes || !IsJpeg(selfie)) return Result<PunchResult>.Fail("The photo couldn't be read. Please try again.");
        }

        // GPS attendance: location is kept only when the plan includes it and the organization switched it on.
        var enabled = await features.GetEnabledFeaturesAsync(OrgId, ct);
        var mode = await db.OrganizationSettings.AsNoTracking().Select(s => s.GeoMode).FirstOrDefaultAsync(ct);
        GeoCheck geo = GeoCheck.None;
        if (mode != Domain.Strength.GeoAttendanceMode.Off && enabled.Contains(Catalog.FeatureCodes.GpsAttendance))
        {
            var enforce = mode == Domain.Strength.GeoAttendanceMode.Enforce && enabled.Contains(Catalog.FeatureCodes.GeoFencing) && !employee.GeoFenceExempt;
            var offices = await db.OfficeLocations.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Id)
                .Select(o => new GeoOffice(o.Id, o.Name, o.Latitude, o.Longitude, o.RadiusMeters)).ToListAsync(ct);
            if (!enabled.Contains(Catalog.FeatureCodes.MultiLocation)) offices = offices.Take(1).ToList();
            geo = GeoFence.Evaluate(location, offices, enforce, employee.GeoFenceExempt);
            if (geo.Error is not null) return Result<PunchResult>.Fail(geo.Error);
        }
        var org = await db.OrgClockAsync(OrgId, clock, ct);
        var nowLocal = org.LocalNow;
        var today = org.Today;
        if (today < employee.DateOfJoining) return Result<PunchResult>.Fail("Attendance starts from your joining date.");

        var calendar = await WorkCalendar.LoadAsync(db, today.AddDays(-1), today, [employeeId], ct);
        var date = today;
        if (calendar.ShiftFor(employeeId, today.AddDays(-1)) is { CrossesMidnight: true } night
            && nowLocal.TimeOfDay < night.EndTime.ToTimeSpan() + NightShiftSpill)
            date = today.AddDays(-1);

        var last = await db.AttendancePunches.Where(p => p.EmployeeId == employeeId && p.AttendanceDate == date)
            .OrderByDescending(p => p.TimestampUtc).FirstOrDefaultAsync(ct);
        if (last is not null && clock.UtcNow - last.TimestampUtc < TimeSpan.FromMinutes(1))
            return Result<PunchResult>.Fail("You just punched. Please wait a minute before punching again.");

        var direction = last?.Direction == PunchDirection.In ? PunchDirection.Out : PunchDirection.In;
        string? selfieKey = null;
        if (keepSelfie) selfieKey = await storage.SaveAsync(OrgId, new MemoryStream(selfie!), ".jpg", ct);
        db.AttendancePunches.Add(new AttendancePunch
        {
            EmployeeId = employeeId, TimestampUtc = clock.UtcNow, AttendanceDate = date, Direction = direction, Source = source, IpAddress = ipAddress,
            Latitude = geo.Point?.Latitude, Longitude = geo.Point?.Longitude, AccuracyMeters = geo.Point?.AccuracyMeters,
            OfficeLocationId = geo.OfficeId, DistanceMeters = geo.DistanceMeters, LocationStatus = geo.Status, SelfieStorageKey = selfieKey
        });
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            if (selfieKey is not null) await storage.DeleteAsync(selfieKey, ct);
            throw;
        }
        await RecalculateRangeAsync([employeeId], date, date, ct);
        return Result<PunchResult>.Ok(new PunchResult(direction, nowLocal, date));
    }

    public async Task<TodayStatus> TodayAsync(int employeeId, CancellationToken ct = default)
    {
        var org = await db.OrgClockAsync(OrgId, clock, ct);
        var today = org.Today;
        var calendar = await WorkCalendar.LoadAsync(db, today.AddDays(-1), today, [employeeId], ct);
        // Still inside last night's shift window? Show that day.
        var date = calendar.ShiftFor(employeeId, today.AddDays(-1)) is { CrossesMidnight: true } night
                   && org.LocalNow.TimeOfDay < night.EndTime.ToTimeSpan() + NightShiftSpill
                   && await db.AttendancePunches.AnyAsync(p => p.EmployeeId == employeeId && p.AttendanceDate == today.AddDays(-1), ct)
            ? today.AddDays(-1) : today;
        await RecalculateRangeAsync([employeeId], date, date, ct);
        var punches = await db.AttendancePunches.AsNoTracking().Where(p => p.EmployeeId == employeeId && p.AttendanceDate == date)
            .OrderBy(p => p.TimestampUtc).ToListAsync(ct);
        var day = await db.Attendance.AsNoTracking().FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date, ct);
        var shift = calendar.ShiftFor(employeeId, date);
        return new TodayStatus(date, punches.LastOrDefault()?.Direction == PunchDirection.In, day?.WorkedMinutes ?? 0,
            day?.Status ?? AttendanceStatus.NotMarked, shift?.Name, shift?.StartTime, shift?.EndTime,
            punches.Select(p => (p.Direction, org.ToLocal(p.TimestampUtc))).ToList(),
            calendar.IsWorkingDay(employeeId, date), calendar.HolidayFor(employeeId, date)?.Name);
    }

    public async Task RecalculateRangeAsync(IReadOnlyCollection<int>? employeeIds, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var org = await db.OrgClockAsync(OrgId, clock, ct);
        var today = org.Today;
        if (to > today) to = today;
        if (from > to) return;
        if (to.DayNumber - from.DayNumber > 92) from = to.AddDays(-92); // bounded batch

        var employees = await db.Employees.AsNoTracking()
            .Where(e => (employeeIds == null || employeeIds.Contains(e.Id)) && e.DateOfJoining <= to && (e.DateOfExit == null || e.DateOfExit >= from))
            .Select(e => new { e.Id, e.DateOfJoining, e.DateOfExit }).ToListAsync(ct);
        if (employees.Count == 0) return;
        var ids = employees.Select(e => e.Id).ToList();

        var calendar = await WorkCalendar.LoadAsync(db, from, to, ids, ct);
        var leaves = await db.LeaveApplications.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.Status == LeaveStatus.Approved && a.FromDate <= to && a.ToDate >= from)
            .Select(a => new { a.EmployeeId, a.FromDate, a.ToDate, a.IsHalfDay }).ToListAsync(ct);
        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(p => ids.Contains(p.EmployeeId) && p.AttendanceDate >= from && p.AttendanceDate <= to)
            .OrderBy(p => p.TimestampUtc).ToListAsync(ct);
        var days = await db.Attendance.Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to).ToListAsync(ct);

        foreach (var e in employees)
        {
            var start = e.DateOfJoining > from ? e.DateOfJoining : from;
            var end = e.DateOfExit is { } x && x < to ? x : to;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var existing = days.FirstOrDefault(d => d.EmployeeId == e.Id && d.Date == date);
                if (existing?.IsRegularized == true) continue;
                var leave = leaves.FirstOrDefault(l => l.EmployeeId == e.Id && l.FromDate <= date && l.ToDate >= date);
                var computed = Compute(e.Id, date, calendar, leave is null ? null : leave.IsHalfDay,
                    punches.Where(p => p.EmployeeId == e.Id && p.AttendanceDate == date).ToList(), date == today, org);
                if (existing is null)
                {
                    db.Attendance.Add(computed);
                    days.Add(computed);
                }
                else if (Differs(existing, computed))
                {
                    existing.ShiftId = computed.ShiftId;
                    existing.FirstInUtc = computed.FirstInUtc;
                    existing.LastOutUtc = computed.LastOutUtc;
                    existing.WorkedMinutes = computed.WorkedMinutes;
                    existing.LateMinutes = computed.LateMinutes;
                    existing.OvertimeMinutes = computed.OvertimeMinutes;
                    existing.Status = computed.Status;
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static bool Differs(AttendanceDay a, AttendanceDay b) =>
        a.ShiftId != b.ShiftId || a.FirstInUtc != b.FirstInUtc || a.LastOutUtc != b.LastOutUtc || a.WorkedMinutes != b.WorkedMinutes
        || a.LateMinutes != b.LateMinutes || a.OvertimeMinutes != b.OvertimeMinutes || a.Status != b.Status;

    /// <summary>Pure daily attendance rule set (unit tested).</summary>
    internal AttendanceDay Compute(int employeeId, DateOnly date, WorkCalendar calendar, bool? leaveHalfDay, IReadOnlyList<AttendancePunch> punches, bool isToday, OrgClock org)
    {
        var shift = calendar.ShiftFor(employeeId, date);
        var holiday = calendar.HolidayFor(employeeId, date);
        var weeklyOff = calendar.IsWeeklyOff(employeeId, date);

        int worked = 0;
        DateTime? openIn = null;
        foreach (var p in punches.OrderBy(p => p.TimestampUtc))
        {
            if (p.Direction == PunchDirection.In) openIn ??= p.TimestampUtc;
            else if (openIn is { } i) { worked += (int)(p.TimestampUtc - i).TotalMinutes; openIn = null; }
        }
        if (openIn is { } stillIn && isToday) worked += (int)Math.Max(0, (clock.UtcNow - stillIn).TotalMinutes);

        var firstIn = punches.Where(p => p.Direction == PunchDirection.In).Select(p => (DateTime?)p.TimestampUtc).Min();
        var lastOut = punches.Where(p => p.Direction == PunchDirection.Out).Select(p => (DateTime?)p.TimestampUtc).Max();
        var day = new AttendanceDay
        {
            EmployeeId = employeeId, Date = date, ShiftId = shift?.Id, FirstInUtc = firstIn, LastOutUtc = lastOut, WorkedMinutes = worked
        };

        var fullMinutes = (int)((shift?.MinHoursFullDay ?? 8) * 60);
        var halfMinutes = (int)((shift?.MinHoursHalfDay ?? 4) * 60);
        var threshold = shift?.OvertimeThresholdMinutes ?? 30;

        if (leaveHalfDay == false) day.Status = AttendanceStatus.Leave;
        else if (holiday is not null || weeklyOff)
        {
            day.Status = holiday is not null ? AttendanceStatus.Holiday : AttendanceStatus.WeeklyOff;
            day.OvertimeMinutes = worked >= threshold ? worked : 0; // work on a day off is overtime
        }
        else if (leaveHalfDay == true) day.Status = AttendanceStatus.HalfDayLeave;
        else if (punches.Count > 0)
            day.Status = worked >= fullMinutes || isToday ? AttendanceStatus.Present
                : worked >= halfMinutes ? AttendanceStatus.HalfDay : AttendanceStatus.Absent;
        else day.Status = isToday ? AttendanceStatus.NotMarked : AttendanceStatus.Absent;

        var isWorking = holiday is null && !weeklyOff && leaveHalfDay != false;
        if (isWorking && shift is not null && firstIn is { } fi)
        {
            var scheduled = org.ToUtc(date, shift.StartTime);
            var lateBy = (int)(fi - scheduled).TotalMinutes;
            day.LateMinutes = lateBy > shift.GraceMinutes ? lateBy : 0;
            if (leaveHalfDay is null)
            {
                var extra = worked - shift.ScheduledMinutes;
                day.OvertimeMinutes = extra >= threshold ? extra : 0;
            }
        }
        return day;
    }

    public async Task<(IReadOnlyList<AttendanceRow>, DaySummary)> DayAsync(DateOnly date, int? departmentId, string? search, CancellationToken ct = default)
    {
        var org = await db.OrgClockAsync(OrgId, clock, ct);
        await RecalculateRangeAsync(null, date, date, ct);
        var q = db.Attendance.AsNoTracking().Where(a => a.Date == date);
        if (departmentId is { } d) q = q.Where(a => a.Employee.DepartmentId == d);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(a => a.Employee.FirstName.Contains(search) || a.Employee.LastName.Contains(search) || a.Employee.EmployeeCode.Contains(search));
        var rows = await q.OrderBy(a => a.Employee.FirstName).ThenBy(a => a.Employee.LastName)
            .Select(a => new { a, Name = a.Employee.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode, Dept = a.Employee.Department != null ? a.Employee.Department.Name : null })
            .ToListAsync(ct);
        var list = rows.Select(r => new AttendanceRow(r.a.EmployeeId, r.Name, r.EmployeeCode, r.Dept, r.a.Status,
            r.a.FirstInUtc is { } fi ? org.ToLocal(fi) : null, r.a.LastOutUtc is { } lo ? org.ToLocal(lo) : null,
            r.a.WorkedMinutes, r.a.LateMinutes, r.a.OvertimeMinutes, r.a.IsRegularized, r.a.Remarks)).ToList();
        var summary = new DaySummary(
            list.Count(r => r.Status == AttendanceStatus.Present), list.Count(r => r.Status == AttendanceStatus.HalfDay),
            list.Count(r => r.Status == AttendanceStatus.Absent), list.Count(r => r.Status is AttendanceStatus.Leave or AttendanceStatus.HalfDayLeave),
            list.Count(r => r.LateMinutes > 0), list.Count(r => r.Status == AttendanceStatus.NotMarked),
            list.Count(r => r.Status is AttendanceStatus.Holiday or AttendanceStatus.WeeklyOff));
        return (list, summary);
    }

    public async Task<AttendanceRegister> RegisterAsync(int year, int month, int? departmentId, IReadOnlyCollection<int>? employeeIds = null, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        await RecalculateRangeAsync(employeeIds, from, to, ct);
        var q = db.Attendance.AsNoTracking().Where(a => a.Date >= from && a.Date <= to);
        if (departmentId is { } d) q = q.Where(a => a.Employee.DepartmentId == d);
        if (employeeIds is not null) q = q.Where(a => employeeIds.Contains(a.EmployeeId));
        var data = await q.Select(a => new
        {
            a.EmployeeId, a.Date, a.Status, a.LateMinutes, a.OvertimeMinutes,
            Name = a.Employee.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode,
            Dept = a.Employee.Department != null ? a.Employee.Department.Name : null
        }).ToListAsync(ct);

        var rows = data.GroupBy(x => x.EmployeeId).Select(g =>
        {
            var first = g.First();
            decimal present = g.Count(x => x.Status == AttendanceStatus.Present) + 0.5m * g.Count(x => x.Status is AttendanceStatus.HalfDay or AttendanceStatus.HalfDayLeave);
            decimal absent = g.Count(x => x.Status == AttendanceStatus.Absent) + 0.5m * g.Count(x => x.Status == AttendanceStatus.HalfDay);
            decimal leave = g.Count(x => x.Status == AttendanceStatus.Leave) + 0.5m * g.Count(x => x.Status == AttendanceStatus.HalfDayLeave);
            return new RegisterRow(g.Key, first.Name, first.EmployeeCode, first.Dept, g.ToDictionary(x => x.Date, x => x.Status),
                present, absent, leave, g.Count(x => x.LateMinutes > 0), g.Sum(x => x.OvertimeMinutes));
        }).OrderBy(r => r.Name).ToList();

        var dates = Enumerable.Range(0, to.Day).Select(i => from.AddDays(i)).ToList();
        return new AttendanceRegister { Year = year, Month = month, Dates = dates, Rows = rows };
    }

    public async Task<Result> RegularizeAsync(int employeeId, RegularizeInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Remarks)) return Result.Fail("A reason is required.");
        var org = await db.OrgClockAsync(OrgId, clock, ct);
        if (input.Date > org.Today) return Result.Fail("Future dates cannot be regularized.");
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return Result.Fail("Employee not found.");
        if (input.Date < employee.DateOfJoining || (employee.DateOfExit is { } x && input.Date > x)) return Result.Fail("The date is outside the employee's employment period.");
        if (input.InTime is { } i && input.OutTime is { } o && o <= i) return Result.Fail("Out time must be after in time (use separate days for night shifts).");

        await RecalculateRangeAsync([employeeId], input.Date, input.Date, ct);
        var day = await db.Attendance.FirstAsync(a => a.EmployeeId == employeeId && a.Date == input.Date, ct);
        day.Status = input.Status;
        day.FirstInUtc = input.InTime is { } it ? org.ToUtc(input.Date, it) : day.FirstInUtc;
        day.LastOutUtc = input.OutTime is { } ot ? org.ToUtc(input.Date, ot) : day.LastOutUtc;
        if (input.InTime is not null && input.OutTime is not null) day.WorkedMinutes = (int)(day.LastOutUtc!.Value - day.FirstInUtc!.Value).TotalMinutes;
        if (input.Status is not (AttendanceStatus.Present or AttendanceStatus.HalfDay)) day.LateMinutes = 0;
        day.IsRegularized = true;
        day.Remarks = input.Remarks.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ClearRegularizationAsync(int employeeId, DateOnly date, CancellationToken ct = default)
    {
        var day = await db.Attendance.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date, ct);
        if (day is null || !day.IsRegularized) return Result.Fail("This day is not regularized.");
        day.IsRegularized = false;
        day.Remarks = null;
        await db.SaveChangesAsync(ct);
        await RecalculateRangeAsync([employeeId], date, date, ct);
        return Result.Ok();
    }

    public static string Code(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "P", AttendanceStatus.HalfDay => "HD", AttendanceStatus.Absent => "A", AttendanceStatus.Leave => "L",
        AttendanceStatus.HalfDayLeave => "HL", AttendanceStatus.Holiday => "H", AttendanceStatus.WeeklyOff => "WO", _ => "-"
    };

    public async Task<ExportFile> ExportRegisterAsync(int year, int month, int? departmentId, CancellationToken ct = default)
    {
        var reg = await RegisterAsync(year, month, departmentId, null, ct);
        var headers = new List<string> { "Employee ID", "Name", "Department" };
        headers.AddRange(reg.Dates.Select(d => d.Day.ToString()));
        headers.AddRange(["Present", "Absent", "Leave", "Late days", "Overtime (h)"]);
        var rows = reg.Rows.Select(r =>
        {
            var cells = new List<object?> { r.Code, r.Name, r.Department };
            cells.AddRange(reg.Dates.Select(d => (object?)(r.Days.TryGetValue(d, out var s) ? Code(s) : "")));
            cells.AddRange([r.Present, r.Absent, r.Leave, r.LateDays, Math.Round(r.OvertimeMinutes / 60m, 2)]);
            return (IReadOnlyList<object?>)cells;
        });
        return new ExportFile(excel.Export($"Attendance {year}-{month:00}", headers, rows), $"attendance-{year}-{month:00}.xlsx");
    }
}
