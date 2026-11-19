using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

public sealed class LeaveTypeInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(10), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Letters and digits only.")] public string Code { get; set; } = string.Empty;
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] public string Color { get; set; } = "#0f766e";
    [Display(Name = "Paid leave")] public bool IsPaid { get; set; } = true;
    [Range(0, 365), Display(Name = "Days per year")] public decimal AnnualQuota { get; set; } = 12;
    [Display(Name = "Accrual")] public AccrualMethod AccrualMethod { get; set; } = AccrualMethod.AnnualUpfront;
    [Range(0, 365), Display(Name = "Carry-forward limit (days)")] public decimal CarryForwardLimit { get; set; }
    [Range(0, 365), Display(Name = "Max encashable days / year")] public decimal MaxEncashableDaysPerYear { get; set; }
    [Display(Name = "Allow half-day")] public bool AllowHalfDay { get; set; } = true;
    [Range(0, 90), Display(Name = "Minimum notice (days)")] public int MinNoticeDays { get; set; }
    [Display(Name = "Only for")] public Gender? ApplicableGender { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Range(0, 1000), Display(Name = "Sort order")] public int SortOrder { get; set; }
}

public sealed class HolidayInput
{
    [Required] public DateOnly Date { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Display(Name = "Optional holiday")] public bool IsOptional { get; set; }
    [Display(Name = "Branch (blank = all)")] public int? BranchId { get; set; }
}

public sealed class LeaveApplyInput
{
    [Required, Display(Name = "Leave type")] public int LeaveTypeId { get; set; }
    [Required, Display(Name = "From")] public DateOnly FromDate { get; set; }
    [Required, Display(Name = "To")] public DateOnly ToDate { get; set; }
    [Display(Name = "Half day")] public bool IsHalfDay { get; set; }
    [Display(Name = "Session")] public HalfDaySession? HalfDaySession { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}

public sealed record LeaveTypeRow(int Id, string Name, string Code, string Color, bool IsPaid, decimal AnnualQuota, AccrualMethod Accrual,
    decimal CarryForwardLimit, decimal MaxEncashable, bool AllowHalfDay, int MinNoticeDays, Gender? Gender, bool IsActive);

public sealed record LeaveRequestRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, string LeaveType, string Color,
    DateOnly From, DateOnly To, bool IsHalfDay, HalfDaySession? Session, decimal Days, string Reason, LeaveStatus Status,
    DateTime AppliedAtUtc, string? DecidedBy, string? DecisionNotes, int? ApproverUserId);

public sealed record BalanceRow(int LeaveTypeId, string LeaveType, string Code, string Color, bool IsPaid, decimal Opening, decimal Accrued,
    decimal Adjusted, decimal Used, decimal Encashed, decimal Available, decimal Pending, decimal MaxEncashable);

public sealed record EmployeeBalancesRow(int EmployeeId, string Name, string Code, string? Department, IReadOnlyDictionary<int, decimal> AvailableByType);

public sealed class LeaveRequestQuery
{
    public LeaveStatus? Status { get; set; } = LeaveStatus.Pending;
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public interface ILeaveService
{
    // Policies & calendar
    Task<IReadOnlyList<LeaveTypeRow>> LeaveTypesAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<LeaveTypeInput?> GetLeaveTypeAsync(int id, CancellationToken ct = default);
    Task<Result> SaveLeaveTypeAsync(int? id, LeaveTypeInput input, CancellationToken ct = default);
    Task<IReadOnlyList<Holiday>> HolidaysAsync(int year, CancellationToken ct = default);
    Task<Result> SaveHolidayAsync(int? id, HolidayInput input, CancellationToken ct = default);
    Task<Result> DeleteHolidayAsync(int id, CancellationToken ct = default);

    // Balances
    Task EnsureBalancesAsync(int year, IReadOnlyCollection<int>? employeeIds = null, CancellationToken ct = default);
    Task<IReadOnlyList<BalanceRow>> BalancesForEmployeeAsync(int employeeId, int year, CancellationToken ct = default);
    Task<PagedResult<EmployeeBalancesRow>> BalancesAsync(int year, string? search, int page, CancellationToken ct = default);
    Task<Result> AdjustAsync(int employeeId, int leaveTypeId, int year, decimal days, string reason, CancellationToken ct = default);
    Task<Result> EncashAsync(int employeeId, int leaveTypeId, int year, decimal days, CancellationToken ct = default);

    // Applications
    Task<decimal> PreviewDaysAsync(int employeeId, LeaveApplyInput input, CancellationToken ct = default);
    Task<Result<int>> ApplyAsync(int employeeId, LeaveApplyInput input, bool byEmployee, CancellationToken ct = default);
    /// <param name="asHr">Caller holds the leave-management permission (may decide any request).</param>
    Task<Result> ApproveAsync(int id, string? notes, bool asHr, CancellationToken ct = default);
    Task<Result> RejectAsync(int id, string notes, bool asHr, CancellationToken ct = default);
    /// <param name="employeeId">When set, only that employee's own request may be cancelled (self-service).</param>
    Task<Result> CancelAsync(int id, int? employeeId, CancellationToken ct = default);
    Task<PagedResult<LeaveRequestRow>> RequestsAsync(LeaveRequestQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveRequestRow>> PendingForApproverAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveRequestRow>> ForEmployeeAsync(int employeeId, int year, CancellationToken ct = default);
    Task<IReadOnlyList<LeaveRequestRow>> OnLeaveAsync(DateOnly date, CancellationToken ct = default);
}

public sealed class LeaveService(
    IAppDbContext db, ICurrentContext context, IClock clock, IAttendanceService attendance,
    INotificationService notifications, IAuditWriter audit) : ILeaveService
{
    private int OrgId => context.OrgId();

    // ---------------------------------------------------------------- policies
    public async Task<IReadOnlyList<LeaveTypeRow>> LeaveTypesAsync(bool activeOnly = false, CancellationToken ct = default) =>
        await db.LeaveTypes.AsNoTracking().Where(t => !activeOnly || t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new LeaveTypeRow(t.Id, t.Name, t.Code, t.Color, t.IsPaid, t.AnnualQuota, t.AccrualMethod, t.CarryForwardLimit,
                t.MaxEncashableDaysPerYear, t.AllowHalfDay, t.MinNoticeDays, t.ApplicableGender, t.IsActive))
            .ToListAsync(ct);

    public async Task<LeaveTypeInput?> GetLeaveTypeAsync(int id, CancellationToken ct = default) =>
        await db.LeaveTypes.AsNoTracking().Where(t => t.Id == id).Select(t => new LeaveTypeInput
        {
            Name = t.Name, Code = t.Code, Color = t.Color, IsPaid = t.IsPaid, AnnualQuota = t.AnnualQuota, AccrualMethod = t.AccrualMethod,
            CarryForwardLimit = t.CarryForwardLimit, MaxEncashableDaysPerYear = t.MaxEncashableDaysPerYear, AllowHalfDay = t.AllowHalfDay,
            MinNoticeDays = t.MinNoticeDays, ApplicableGender = t.ApplicableGender, IsActive = t.IsActive, SortOrder = t.SortOrder
        }).FirstOrDefaultAsync(ct);

    public async Task<Result> SaveLeaveTypeAsync(int? id, LeaveTypeInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.LeaveTypes.AnyAsync(t => t.Code == code && t.Id != id, ct)) return Result.Fail($"Leave code {code} is already used.");
        if (!input.IsPaid && input.MaxEncashableDaysPerYear > 0) return Result.Fail("Unpaid leave cannot be encashed.");
        LeaveType? t;
        if (id is null) t = db.LeaveTypes.Add(new LeaveType()).Entity;
        else
        {
            t = await db.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return Result.Fail("Leave type not found.");
        }
        t.Name = input.Name.Trim();
        t.Code = code;
        t.Color = input.Color;
        t.IsPaid = input.IsPaid;
        t.AnnualQuota = input.AnnualQuota;
        t.AccrualMethod = input.AccrualMethod;
        t.CarryForwardLimit = input.CarryForwardLimit;
        t.MaxEncashableDaysPerYear = input.MaxEncashableDaysPerYear;
        t.AllowHalfDay = input.AllowHalfDay;
        t.MinNoticeDays = input.MinNoticeDays;
        t.ApplicableGender = input.ApplicableGender;
        t.IsActive = input.IsActive;
        t.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<Holiday>> HolidaysAsync(int year, CancellationToken ct = default)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return await db.Holidays.AsNoTracking().Include(h => h.Branch).Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date).ToListAsync(ct);
    }

    public async Task<Result> SaveHolidayAsync(int? id, HolidayInput input, CancellationToken ct = default)
    {
        if (input.BranchId is int b && !await db.Branches.AnyAsync(x => x.Id == b, ct)) return Result.Fail("Branch not found.");
        if (await db.Holidays.AnyAsync(h => h.Date == input.Date && h.BranchId == input.BranchId && h.Id != id, ct))
            return Result.Fail("A holiday already exists on this date for the same branch.");
        Holiday? h;
        DateOnly? oldDate = null;
        if (id is null) h = db.Holidays.Add(new Holiday()).Entity;
        else
        {
            h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (h is null) return Result.Fail("Holiday not found.");
            oldDate = h.Date;
        }
        h.Date = input.Date;
        h.Name = input.Name.Trim();
        h.IsOptional = input.IsOptional;
        h.BranchId = input.BranchId;
        await db.SaveChangesAsync(ct);
        await attendance.RecalculateRangeAsync(null, input.Date, input.Date, ct);
        if (oldDate is { } od && od != input.Date) await attendance.RecalculateRangeAsync(null, od, od, ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteHolidayAsync(int id, CancellationToken ct = default)
    {
        var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result.Fail("Holiday not found.");
        db.Holidays.Remove(h);
        await db.SaveChangesAsync(ct);
        await attendance.RecalculateRangeAsync(null, h.Date, h.Date, ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------- balances

    /// <summary>Accrual is a pure function of policy, joining date and "as of" date, so recalculation is idempotent.</summary>
    public static decimal ExpectedAccrual(LeaveType type, DateOnly joining, int year, DateOnly asOf)
    {
        if (type.AnnualQuota <= 0) return 0;
        var yearStart = new DateOnly(year, 1, 1);
        var start = joining > yearStart ? joining : yearStart;
        if (start.Year != year) return 0;
        decimal months;
        if (type.AccrualMethod == AccrualMethod.AnnualUpfront)
            months = 12 - (start.Month - 1);
        else
        {
            if (asOf < start) return 0;
            var lastMonth = asOf.Year > year ? 12 : asOf.Month;
            months = Math.Max(0, lastMonth - start.Month + 1);
        }
        var days = type.AnnualQuota * months / 12m;
        return Math.Floor(days * 2) / 2; // credit in half-day steps, never more than earned
    }

    public async Task EnsureBalancesAsync(int year, IReadOnlyCollection<int>? employeeIds = null, CancellationToken ct = default)
    {
        var orgClock = await db.OrgClockAsync(OrgId, clock, ct);
        var asOf = orgClock.Today;
        var types = await db.LeaveTypes.Where(t => t.IsActive).ToListAsync(ct);
        if (types.Count == 0) return;
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && (employeeIds == null || employeeIds.Contains(e.Id)))
            .Select(e => new { e.Id, e.DateOfJoining, e.Gender }).ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();
        var existing = await db.LeaveBalances.Where(b => (b.Year == year || b.Year == year - 1) && ids.Contains(b.EmployeeId)).ToListAsync(ct);

        foreach (var e in employees)
        foreach (var t in types.Where(t => t.ApplicableGender is null || t.ApplicableGender == e.Gender))
        {
            var bal = existing.FirstOrDefault(b => b.EmployeeId == e.Id && b.LeaveTypeId == t.Id && b.Year == year);
            if (bal is null)
            {
                var prev = existing.FirstOrDefault(b => b.EmployeeId == e.Id && b.LeaveTypeId == t.Id && b.Year == year - 1);
                bal = new LeaveBalance
                {
                    EmployeeId = e.Id, LeaveTypeId = t.Id, Year = year,
                    Opening = prev is not null && t.CarryForwardLimit > 0 ? Math.Clamp(prev.Available, 0, t.CarryForwardLimit) : 0
                };
                db.LeaveBalances.Add(bal);
                existing.Add(bal);
            }
            var expected = ExpectedAccrual(t, e.DateOfJoining, year, asOf);
            if (bal.Accrued != expected) bal.Accrued = expected;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BalanceRow>> BalancesForEmployeeAsync(int employeeId, int year, CancellationToken ct = default)
    {
        await EnsureBalancesAsync(year, [employeeId], ct);
        var pending = await PendingDaysAsync(employeeId, year, ct);
        var rows = await db.LeaveBalances.AsNoTracking().Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == employeeId && b.Year == year && b.LeaveType.IsActive)
            .OrderBy(b => b.LeaveType.SortOrder).ThenBy(b => b.LeaveType.Name).ToListAsync(ct);
        return rows.Select(b => new BalanceRow(b.LeaveTypeId, b.LeaveType.Name, b.LeaveType.Code, b.LeaveType.Color, b.LeaveType.IsPaid,
            b.Opening, b.Accrued, b.Adjusted, b.Used, b.Encashed, b.Available, pending.GetValueOrDefault(b.LeaveTypeId), b.LeaveType.MaxEncashableDaysPerYear)).ToList();
    }

    private async Task<Dictionary<int, decimal>> PendingDaysAsync(int employeeId, int year, CancellationToken ct) =>
        (await db.LeaveApplications.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Status == LeaveStatus.Pending && a.FromDate.Year == year)
            .Select(a => new { a.LeaveTypeId, a.Days }).ToListAsync(ct))
        .GroupBy(a => a.LeaveTypeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Days));

    public async Task<PagedResult<EmployeeBalancesRow>> BalancesAsync(int year, string? search, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        await EnsureBalancesAsync(year, null, ct);
        var q = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(e => e.FirstName.Contains(search) || e.LastName.Contains(search) || e.EmployeeCode.Contains(search));
        var total = await q.CountAsync(ct);
        var emps = await q.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Skip((page - 1) * size).Take(size)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department != null ? e.Department.Name : null })
            .ToListAsync(ct);
        var ids = emps.Select(e => e.Id).ToList();
        var balances = await db.LeaveBalances.AsNoTracking().Where(b => b.Year == year && ids.Contains(b.EmployeeId)).ToListAsync(ct);
        var rows = emps.Select(e => new EmployeeBalancesRow(e.Id, e.Name, e.EmployeeCode, e.Dept,
            balances.Where(b => b.EmployeeId == e.Id).ToDictionary(b => b.LeaveTypeId, b => b.Available))).ToList();
        return new PagedResult<EmployeeBalancesRow>(rows, total, page, size);
    }

    private async Task<(LeaveBalance? Balance, string? Error)> BalanceForUpdateAsync(int employeeId, int leaveTypeId, int year, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct)) return (null, "Employee not found.");
        if (!await db.LeaveTypes.AnyAsync(t => t.Id == leaveTypeId, ct)) return (null, "Leave type not found.");
        await EnsureBalancesAsync(year, [employeeId], ct);
        var bal = await db.LeaveBalances.Include(b => b.LeaveType).FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, ct);
        return bal is null ? (null, "This leave type does not apply to the employee.") : (bal, null);
    }

    public async Task<Result> AdjustAsync(int employeeId, int leaveTypeId, int year, decimal days, string reason, CancellationToken ct = default)
    {
        if (days == 0 || Math.Abs(days) > 365) return Result.Fail("Enter a non-zero adjustment of at most 365 days.");
        if (days * 2 != Math.Floor(days * 2)) return Result.Fail("Adjust in steps of 0.5 days.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("A reason is required.");
        var (bal, error) = await BalanceForUpdateAsync(employeeId, leaveTypeId, year, ct);
        if (bal is null) return Result.Fail(error!);
        bal.Adjusted += days;
        audit.Add("Leave.Adjust", nameof(LeaveBalance), bal.Id.ToString(), $"{bal.LeaveType.Code} {year}: {days:+0.##;-0.##} days. {reason.Trim()}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> EncashAsync(int employeeId, int leaveTypeId, int year, decimal days, CancellationToken ct = default)
    {
        if (days <= 0 || days * 2 != Math.Floor(days * 2)) return Result.Fail("Enter a positive number of days in steps of 0.5.");
        var (bal, error) = await BalanceForUpdateAsync(employeeId, leaveTypeId, year, ct);
        if (bal is null) return Result.Fail(error!);
        var allowance = bal.LeaveType.MaxEncashableDaysPerYear - bal.Encashed;
        if (allowance <= 0) return Result.Fail($"{bal.LeaveType.Name} is not encashable (or the yearly limit is used).");
        if (days > allowance) return Result.Fail($"At most {allowance:0.##} more day(s) can be encashed this year.");
        if (days > bal.Available) return Result.Fail($"Only {bal.Available:0.##} day(s) are available.");
        bal.Encashed += days;
        audit.Add("Leave.Encash", nameof(LeaveBalance), bal.Id.ToString(), $"{bal.LeaveType.Code} {year}: {days:0.##} day(s) encashed (paid in payroll)");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------- applications
    private async Task<(decimal Days, string? Error, LeaveType? Type)> EvaluateAsync(int employeeId, LeaveApplyInput input, bool byEmployee, CancellationToken ct)
    {
        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.LeaveTypeId && t.IsActive, ct);
        if (type is null) return (0, "Select a valid leave type.", null);
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return (0, "Employee not found.", type);
        if (!employee.IsCurrent) return (0, "Leave cannot be applied for a former employee.", type);
        if (type.ApplicableGender is { } g && employee.Gender != g) return (0, $"{type.Name} is not applicable to this employee.", type);
        if (input.ToDate < input.FromDate) return (0, "'To' date cannot be before 'From' date.", type);
        if (input.FromDate.Year != input.ToDate.Year) return (0, "A request cannot span two calendar years. Split it into two requests.", type);
        if (input.ToDate.DayNumber - input.FromDate.DayNumber > 180) return (0, "A single request can cover at most 180 days.", type);
        if (input.FromDate < employee.DateOfJoining) return (0, "Leave cannot start before the joining date.", type);
        if (input.IsHalfDay)
        {
            if (!type.AllowHalfDay) return (0, $"{type.Name} cannot be taken as a half day.", type);
            if (input.FromDate != input.ToDate) return (0, "A half day must be a single date.", type);
            if (input.HalfDaySession is null) return (0, "Choose the first or second half.", type);
        }
        var today = (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        if (byEmployee && type.MinNoticeDays > 0 && input.FromDate < today.AddDays(type.MinNoticeDays))
            return (0, $"{type.Name} must be applied at least {type.MinNoticeDays} day(s) in advance.", type);

        var calendar = await WorkCalendar.LoadAsync(db, input.FromDate, input.ToDate, [employeeId], ct);
        var days = calendar.WorkingDays(employeeId, input.FromDate, input.ToDate);
        if (input.IsHalfDay && days > 0) days = 0.5m;
        if (days == 0) return (0, "The selected dates are weekly offs or holidays — no leave is needed.", type);
        return (days, null, type);
    }

    public async Task<decimal> PreviewDaysAsync(int employeeId, LeaveApplyInput input, CancellationToken ct = default) =>
        (await EvaluateAsync(employeeId, input, false, ct)).Days;

    public async Task<Result<int>> ApplyAsync(int employeeId, LeaveApplyInput input, bool byEmployee, CancellationToken ct = default)
    {
        var (days, error, type) = await EvaluateAsync(employeeId, input, byEmployee, ct);
        if (error is not null) return Result<int>.Fail(error);

        var overlap = await db.LeaveApplications.AnyAsync(a => a.EmployeeId == employeeId
            && (a.Status == LeaveStatus.Pending || a.Status == LeaveStatus.Approved)
            && a.FromDate <= input.ToDate && a.ToDate >= input.FromDate
            && !(a.IsHalfDay && input.IsHalfDay && a.HalfDaySession != input.HalfDaySession), ct);
        if (overlap) return Result<int>.Fail("You already have leave on some of these dates.");

        if (type!.IsPaid)
        {
            var balances = await BalancesForEmployeeAsync(employeeId, input.FromDate.Year, ct);
            var bal = balances.FirstOrDefault(b => b.LeaveTypeId == type.Id);
            var free = bal is null ? 0 : bal.Available - bal.Pending;
            if (days > free) return Result<int>.Fail($"Insufficient {type.Name} balance: {free:0.##} day(s) available, {days:0.##} requested.");
        }

        var approver = await db.ApproverForAsync(employeeId, ct);
        var app = new LeaveApplication
        {
            EmployeeId = employeeId, LeaveTypeId = type.Id, FromDate = input.FromDate, ToDate = input.ToDate,
            IsHalfDay = input.IsHalfDay, HalfDaySession = input.IsHalfDay ? input.HalfDaySession : null,
            Days = days, Reason = input.Reason.Trim(), ApproverUserId = approver
        };
        db.LeaveApplications.Add(app);
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
        var recipients = approver is int a ? [a] : await notifications.UsersWithPermissionAsync(TenantPermissions.LeaveManage, ct);
        notifications.Queue(recipients, "Leave request", $"{name} requested {days:0.#} day(s) of {type.Name} from {input.FromDate:dd MMM}.",
            approver is null ? "/org/leave" : "/me/approvals");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(app.Id);
    }

    private async Task<(LeaveApplication? App, string? Error)> ForDecisionAsync(int id, bool asHr, CancellationToken ct)
    {
        var app = await db.LeaveApplications.Include(a => a.LeaveType).Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return (null, "Leave request not found.");
        if (app.Status != LeaveStatus.Pending) return (null, "This request has already been decided.");
        if (!asHr && app.ApproverUserId != context.UserId) return (null, "You are not the approver for this request.");
        if (app.Employee.UserId == context.UserId) return (null, "You cannot decide your own leave request.");
        return (app, null);
    }

    public async Task<Result> ApproveAsync(int id, string? notes, bool asHr, CancellationToken ct = default)
    {
        var (app, error) = await ForDecisionAsync(id, asHr, ct);
        if (app is null) return Result.Fail(error!);

        if (app.LeaveType.IsPaid)
        {
            await EnsureBalancesAsync(app.FromDate.Year, [app.EmployeeId], ct);
            var bal = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == app.EmployeeId && b.LeaveTypeId == app.LeaveTypeId && b.Year == app.FromDate.Year, ct);
            if (bal is null || bal.Available < app.Days) return Result.Fail($"Insufficient balance: {(bal?.Available ?? 0):0.##} day(s) available.");
            bal.Used += app.Days;
        }
        else
        {
            await EnsureBalancesAsync(app.FromDate.Year, [app.EmployeeId], ct);
            var bal = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == app.EmployeeId && b.LeaveTypeId == app.LeaveTypeId && b.Year == app.FromDate.Year, ct);
            if (bal is not null) bal.Used += app.Days; // tracked for loss-of-pay in payroll
        }
        Decide(app, LeaveStatus.Approved, notes);
        Notify(app, "approved");
        await db.SaveChangesAsync(ct);
        await attendance.RecalculateRangeAsync([app.EmployeeId], app.FromDate, app.ToDate, ct);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int id, string notes, bool asHr, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notes)) return Result.Fail("Please give a reason for rejecting.");
        var (app, error) = await ForDecisionAsync(id, asHr, ct);
        if (app is null) return Result.Fail(error!);
        Decide(app, LeaveStatus.Rejected, notes);
        Notify(app, "rejected");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private void Decide(LeaveApplication app, LeaveStatus status, string? notes)
    {
        app.Status = status;
        app.DecidedBy = context.UserName;
        app.DecidedAtUtc = clock.UtcNow;
        app.DecisionNotes = notes?.Trim();
    }

    private void Notify(LeaveApplication app, string verb)
    {
        if (app.Employee.UserId is int uid)
            notifications.Queue([uid], $"Leave {verb}", $"Your {app.LeaveType.Name} from {app.FromDate:dd MMM} ({app.Days:0.#} day(s)) was {verb}.", "/me/leave");
    }

    public async Task<Result> CancelAsync(int id, int? employeeId, CancellationToken ct = default)
    {
        var app = await db.LeaveApplications.FirstOrDefaultAsync(a => a.Id == id && (employeeId == null || a.EmployeeId == employeeId), ct);
        if (app is null) return Result.Fail("Leave request not found.");
        var today = (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        if (app.Status == LeaveStatus.Approved)
        {
            if (employeeId is not null && app.FromDate <= today) return Result.Fail("Leave that has started can only be cancelled by HR.");
            var bal = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == app.EmployeeId && b.LeaveTypeId == app.LeaveTypeId && b.Year == app.FromDate.Year, ct);
            if (bal is not null) bal.Used -= app.Days;
        }
        else if (app.Status != LeaveStatus.Pending) return Result.Fail("Only pending or approved requests can be cancelled.");
        var wasApproved = app.Status == LeaveStatus.Approved;
        app.Status = LeaveStatus.Cancelled;
        app.DecidedBy = context.UserName;
        app.DecidedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        if (wasApproved) await attendance.RecalculateRangeAsync([app.EmployeeId], app.FromDate, app.ToDate, ct);
        return Result.Ok();
    }

    private static IQueryable<LeaveRequestRow> Project(IQueryable<LeaveApplication> q) =>
        q.Select(a => new LeaveRequestRow(a.Id, a.EmployeeId, a.Employee.FirstName + " " + a.Employee.LastName, a.Employee.EmployeeCode,
            a.LeaveType.Name, a.LeaveType.Color, a.FromDate, a.ToDate, a.IsHalfDay, a.HalfDaySession, a.Days, a.Reason, a.Status,
            a.CreatedAtUtc, a.DecidedBy, a.DecisionNotes, a.ApproverUserId));

    public async Task<PagedResult<LeaveRequestRow>> RequestsAsync(LeaveRequestQuery query, CancellationToken ct = default)
    {
        const int size = 25;
        var page = Math.Max(1, query.Page);
        var q = db.LeaveApplications.AsNoTracking();
        if (query.Status is { } s) q = q.Where(a => a.Status == s);
        if (query.From is { } f) q = q.Where(a => a.ToDate >= f);
        if (query.To is { } t) q = q.Where(a => a.FromDate <= t);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(a => a.Employee.FirstName.Contains(query.Search) || a.Employee.LastName.Contains(query.Search) || a.Employee.EmployeeCode.Contains(query.Search));
        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedResult<LeaveRequestRow>(items, total, page, size);
    }

    public async Task<IReadOnlyList<LeaveRequestRow>> PendingForApproverAsync(int userId, CancellationToken ct = default) =>
        await Project(db.LeaveApplications.AsNoTracking().Where(a => a.Status == LeaveStatus.Pending && a.ApproverUserId == userId)
            .OrderBy(a => a.FromDate)).ToListAsync(ct);

    public async Task<IReadOnlyList<LeaveRequestRow>> ForEmployeeAsync(int employeeId, int year, CancellationToken ct = default) =>
        await Project(db.LeaveApplications.AsNoTracking().Where(a => a.EmployeeId == employeeId && a.FromDate.Year == year)
            .OrderByDescending(a => a.FromDate)).ToListAsync(ct);

    public async Task<IReadOnlyList<LeaveRequestRow>> OnLeaveAsync(DateOnly date, CancellationToken ct = default) =>
        await Project(db.LeaveApplications.AsNoTracking().Where(a => a.Status == LeaveStatus.Approved && a.FromDate <= date && a.ToDate >= date)
            .OrderBy(a => a.Employee.FirstName)).ToListAsync(ct);
}
