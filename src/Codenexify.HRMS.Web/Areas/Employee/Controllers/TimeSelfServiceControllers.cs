using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

[Area("Employee")]
[Authorize(Policy = Policies.Tenant)]
public abstract class SelfServiceController(IEmployeeService employees) : Controller
{
    protected IEmployeeService Employees { get; } = employees;

    protected async Task<int?> MyEmployeeIdAsync(CancellationToken ct) => await Employees.GetMyEmployeeIdAsync(ct);

    protected IActionResult NotLinked()
    {
        TempData["Error"] = "Your login is not linked to an employee record. Please contact HR.";
        return Redirect("/me");
    }

    protected void Flash(Application.Common.Result r, string ok)
    {
        if (r.Succeeded) TempData["Success"] = ok; else TempData["Error"] = r.Error;
    }
}

[RequireFeature(FeatureCodes.Attendance)]
public sealed class AttendanceController(IEmployeeService employees, IAttendanceService attendance, IOvertimeService overtime) : SelfServiceController(employees)
{
    [HttpPost]
    public async Task<IActionResult> Punch(decimal? latitude, decimal? longitude, int? accuracy, string? selfie, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var location = latitude is { } lat && longitude is { } lng ? new GeoPoint(lat, lng, accuracy) : null;
        var r = await attendance.PunchAsync(me, PunchSource.Web, HttpContext.Connection.RemoteIpAddress?.ToString(), location, SelfieData.Decode(selfie), ct);
        if (r.Succeeded) TempData["Success"] = $"Clocked {(r.Value!.Direction == PunchDirection.In ? "in" : "out")} at {r.Value.LocalTime:HH:mm}.";
        else TempData["Error"] = r.Error;
        return Redirect(Url.IsLocalUrl(Request.Headers.Referer.ToString()) ? Request.Headers.Referer.ToString() : "/me");
    }

    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var today = await attendance.TodayForOrganizationAsync(ct);
        var y = year ?? today.Year;
        var m = Math.Clamp(month ?? today.Month, 1, 12);
        var register = await attendance.RegisterAsync(y, m, null, [me], ct);
        ViewBag.Today = today;
        ViewBag.Status = await attendance.TodayAsync(me, ct);
        ViewBag.Overtime = await overtime.ForEmployeeAsync(me, y, m, ct);
        ViewBag.Register = register;
        return View(register.Rows.FirstOrDefault());
    }

    [HttpPost, RequireFeature(FeatureCodes.Overtime)]
    public async Task<IActionResult> ClaimOvertime(DateOnly date, string reason, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await overtime.RequestAsync(me, date, reason ?? string.Empty, ct), "Overtime claim submitted.");
        return Redirect($"/me/attendance?year={date.Year}&month={date.Month}");
    }
}

[RequireFeature(FeatureCodes.Leave)]
public sealed class LeaveController(IEmployeeService employees, ILeaveService leave, IAttendanceService attendance) : SelfServiceController(employees)
{
    public async Task<IActionResult> Index(int? year, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var today = await attendance.TodayForOrganizationAsync(ct);
        var y = year ?? today.Year;
        ViewBag.Year = y;
        ViewBag.Balances = await leave.BalancesForEmployeeAsync(me, y, ct);
        ViewBag.Types = await leave.LeaveTypesAsync(true, ct);
        ViewBag.Holidays = await leave.HolidaysAsync(y, ct);
        ViewBag.Today = today;
        return View(await leave.ForEmployeeAsync(me, y, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Apply(LeaveApplyInput application, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/me/leave");
        }
        var r = await leave.ApplyAsync(me, application, byEmployee: true, ct);
        if (r.Succeeded) TempData["Success"] = "Leave request submitted for approval."; else TempData["Error"] = r.Error;
        return Redirect($"/me/leave?year={application.FromDate.Year}");
    }

    /// <summary>Working days the request would use (AJAX preview while filling the form).</summary>
    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] LeaveApplyInput application, CancellationToken ct) =>
        await MyEmployeeIdAsync(ct) is int me ? Json(new { days = await leave.PreviewDaysAsync(me, application, ct) }) : Json(new { days = 0 });

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await leave.CancelAsync(id, me, ct), "Leave request cancelled.");
        return Redirect("/me/leave");
    }
}

/// <summary>Leave and overtime requests where the signed-in user is the reporting manager.</summary>
public sealed class ApprovalsController(IEmployeeService employees, ILeaveService leave, IOvertimeService overtime, IAttendanceService attendance)
    : SelfServiceController(employees)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var uid = User.GetUserId()!.Value;
        ViewBag.Leave = await leave.PendingForApproverAsync(uid, ct);
        ViewBag.Overtime = await overtime.PendingForApproverAsync(uid, ct);
        if (await MyEmployeeIdAsync(ct) is int me)
        {
            var today = await attendance.TodayForOrganizationAsync(ct);
            var team = await Employees.GetProfileAsync(me, false, ct);
            var teamIds = team?.DirectReports.Select(r => r.Id).ToList() ?? [];
            ViewBag.Team = team?.DirectReports ?? [];
            ViewBag.TeamToday = teamIds.Count == 0 ? null : await attendance.RegisterAsync(today.Year, today.Month, null, teamIds, ct);
            ViewBag.Today = today;
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Leave(int id, bool approve, string? notes, CancellationToken ct)
    {
        var r = approve ? await leave.ApproveAsync(id, notes, asHr: false, ct) : await leave.RejectAsync(id, notes ?? string.Empty, asHr: false, ct);
        Flash(r, approve ? "Leave approved." : "Leave rejected.");
        return Redirect("/me/approvals");
    }

    [HttpPost]
    public async Task<IActionResult> Overtime(int id, bool approve, string? notes, CancellationToken ct)
    {
        Flash(await overtime.DecideAsync(id, approve, null, notes, asHr: false, ct), approve ? "Overtime approved." : "Overtime rejected.");
        return Redirect("/me/approvals");
    }
}

public sealed class NotificationsController(IEmployeeService employees, INotificationService notifications) : SelfServiceController(employees)
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await notifications.MineAsync(page, ct));

    public async Task<IActionResult> Open(int id, CancellationToken ct)
    {
        var link = await notifications.OpenAsync(id, ct);
        return Redirect(link is not null && Url.IsLocalUrl(link) ? link : "/me/notifications");
    }

    [HttpPost]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return Redirect("/me/notifications");
    }
}

[RequireFeature(FeatureCodes.AttendanceGamification)]
public sealed class LeaderboardController(IEmployeeService employees, IGamificationService gamification, IAttendanceService attendance) : SelfServiceController(employees)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        ViewBag.Today = today;
        ViewBag.Mine = await MyEmployeeIdAsync(ct) is int me ? await gamification.ForEmployeeAsync(me, ct) : null;
        return View(await gamification.LeaderboardAsync(today.Year, today.Month, 10, ct));
    }
}
