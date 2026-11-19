using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.CoreHr;

namespace Codenexify.HRMS.Domain.Time;

public enum AccrualMethod { AnnualUpfront, Monthly }
public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }
public enum HalfDaySession { FirstHalf, SecondHalf }
public enum PunchDirection { In, Out }
public enum PunchSource { Web, Mobile, Manual, Biometric }
public enum AttendanceStatus { NotMarked, Present, HalfDay, Absent, Leave, HalfDayLeave, Holiday, WeeklyOff }
public enum OvertimeStatus { Pending, Approved, Rejected }

public class Holiday : TenantEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional (restricted) holidays don't block working days.</summary>
    public bool IsOptional { get; set; }
    /// <summary>Null = applies to every branch.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
}

/// <summary>A leave type together with its policy (quota, accrual, carry-forward, encashment).</summary>
public class LeaveType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Color { get; set; } = "#0f766e";
    public bool IsPaid { get; set; } = true;
    /// <summary>Days granted per calendar year (0 for unlimited unpaid leave such as LOP).</summary>
    public decimal AnnualQuota { get; set; }
    public AccrualMethod AccrualMethod { get; set; } = AccrualMethod.AnnualUpfront;
    /// <summary>Maximum unused days carried into the next year (0 = lapses).</summary>
    public decimal CarryForwardLimit { get; set; }
    /// <summary>Maximum days that can be encashed per year (0 = not encashable).</summary>
    public decimal MaxEncashableDaysPerYear { get; set; }
    public bool AllowHalfDay { get; set; } = true;
    public int MinNoticeDays { get; set; }
    /// <summary>Restrict to a gender (e.g. maternity leave). Null = everyone.</summary>
    public Gender? ApplicableGender { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class LeaveBalance : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public int Year { get; set; }
    public decimal Opening { get; set; }
    public decimal Accrued { get; set; }
    public decimal Adjusted { get; set; }
    public decimal Used { get; set; }
    public decimal Encashed { get; set; }
    /// <summary>Encashed days already paid out through payroll.</summary>
    public decimal EncashmentPaid { get; set; }

    public decimal Available => Opening + Accrued + Adjusted - Used - Encashed;
}

public class LeaveApplication : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool IsHalfDay { get; set; }
    public HalfDaySession? HalfDaySession { get; set; }
    /// <summary>Working days charged (weekly offs and holidays excluded).</summary>
    public decimal Days { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    /// <summary>User expected to decide (reporting manager); HR can always decide.</summary>
    public int? ApproverUserId { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
}

public class Shift : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    /// <summary>Arrivals within this many minutes of the start are on time.</summary>
    public int GraceMinutes { get; set; } = 10;
    public decimal MinHoursFullDay { get; set; } = 8;
    public decimal MinHoursHalfDay { get; set; } = 4;
    /// <summary>Overtime counts only when work exceeds the shift by more than this.</summary>
    public int OvertimeThresholdMinutes { get; set; } = 30;
    /// <summary>Comma separated <see cref="DayOfWeek"/> names, e.g. "Saturday,Sunday".</summary>
    public string WeeklyOffs { get; set; } = "Sunday";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public bool CrossesMidnight => EndTime <= StartTime;
    public int ScheduledMinutes => (int)((CrossesMidnight ? EndTime.ToTimeSpan() + TimeSpan.FromDays(1) : EndTime.ToTimeSpan()) - StartTime.ToTimeSpan()).TotalMinutes - BreakMinutes;
    public bool IsWeeklyOff(DayOfWeek day) => WeeklyOffs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(day.ToString());
}

public class ShiftAssignment : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class AttendancePunch : TenantEntity
{
    public int EmployeeId { get; set; }
    public DateTime TimestampUtc { get; set; }
    /// <summary>Attendance date the punch belongs to (night shifts attribute early-morning punches to the previous day).</summary>
    public DateOnly AttendanceDate { get; set; }
    public PunchDirection Direction { get; set; }
    public PunchSource Source { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
    // GPS attendance (stored only when the feature is enabled and switched on by the organization)
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? AccuracyMeters { get; set; }
    public int? OfficeLocationId { get; set; }
    public int? DistanceMeters { get; set; }
    public Strength.PunchLocationStatus LocationStatus { get; set; }
    /// <summary>Private storage key of the clock-in photo (Selfie Clock-in); cleared when the retention period ends.</summary>
    public string? SelfieStorageKey { get; set; }
}

/// <summary>Calculated daily attendance (table "Attendance").</summary>
public class AttendanceDay : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? FirstInUtc { get; set; }
    public DateTime? LastOutUtc { get; set; }
    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public AttendanceStatus Status { get; set; }
    /// <summary>Manually set by HR; recalculation leaves regularized days untouched.</summary>
    public bool IsRegularized { get; set; }
    public string? Remarks { get; set; }
}

public class OvertimeRequest : TenantEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public string Reason { get; set; } = string.Empty;
    public OvertimeStatus Status { get; set; } = OvertimeStatus.Pending;
    public int? ApproverUserId { get; set; }
    public int? ApprovedMinutes { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNotes { get; set; }
}

/// <summary>In-app notification for one user.</summary>
public class Notification : TenantEntity
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    /// <summary>Waiting for the email dispatcher (which honours the plan and the user's preference).</summary>
    public bool EmailPending { get; set; }
}
