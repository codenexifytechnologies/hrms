using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

[RequireFeature(FeatureCodes.Leave), RequirePermission(TenantPermissions.LeaveManage)]
public sealed class LeaveController(ILeaveService leave, IStructureService structure, IEmployeeService employees, IAttendanceService attendance) : OrgController
{
    public async Task<IActionResult> Index([FromQuery] LeaveRequestQuery query, int? year, string? search, int page = 1, CancellationToken ct = default)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        var y = year ?? today.Year;
        ViewBag.Year = y;
        ViewBag.Query = query;
        ViewBag.Search = search;
        ViewBag.Types = await leave.LeaveTypesAsync(false, ct);
        ViewBag.Holidays = await leave.HolidaysAsync(y, ct);
        ViewBag.Balances = await leave.BalancesAsync(y, search, page, ct);
        ViewBag.Branches = (await structure.LookupsAsync(ct)).Branches;
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        ViewBag.OnLeaveToday = await leave.OnLeaveAsync(today, ct);
        return View(await leave.RequestsAsync(query, ct));
    }

    private IActionResult Back(string tab, int? year = null) => Redirect($"/org/leave{(year is null ? "" : $"?year={year}")}#{tab}");

    private IActionResult Invalid(string tab)
    {
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return Back(tab);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? notes, CancellationToken ct)
    {
        Flash(await leave.ApproveAsync(id, notes, asHr: true, ct), "Leave approved.");
        return Back("requests");
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string notes, CancellationToken ct)
    {
        Flash(await leave.RejectAsync(id, notes, asHr: true, ct), "Leave rejected.");
        return Back("requests");
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        Flash(await leave.CancelAsync(id, null, ct), "Leave cancelled and balance restored.");
        return Back("requests");
    }

    [HttpPost]
    public async Task<IActionResult> ApplyFor(int employeeId, LeaveApplyInput application, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("requests");
        var result = await leave.ApplyAsync(employeeId, application, byEmployee: false, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back("requests"); }
        // HR recorded it on the employee's behalf, so it is approved straight away.
        Flash(await leave.ApproveAsync(result.Value, "Recorded by HR", asHr: true, ct), "Leave recorded and approved.");
        return Back("requests");
    }

    [HttpPost]
    public async Task<IActionResult> Adjust(int employeeId, int leaveTypeId, int year, decimal days, string reason, CancellationToken ct)
    {
        Flash(await leave.AdjustAsync(employeeId, leaveTypeId, year, days, reason, ct), "Balance adjusted.");
        return Back("balances", year);
    }

    [HttpPost]
    public async Task<IActionResult> Encash(int employeeId, int leaveTypeId, int year, decimal days, CancellationToken ct)
    {
        Flash(await leave.EncashAsync(employeeId, leaveTypeId, year, days, ct), "Leave encashed. It will be paid with payroll.");
        return Back("balances", year);
    }

    [HttpGet("/org/leave/type/{id:int?}")]
    public async Task<IActionResult> Type(int? id, CancellationToken ct)
    {
        var model = id is null ? new LeaveTypeInput() : await leave.GetLeaveTypeAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View("EditType", model);
    }

    [HttpPost("/org/leave/type/{id:int?}")]
    public async Task<IActionResult> Type(int? id, LeaveTypeInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await leave.SaveLeaveTypeAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Leave type saved."; return Back("types"); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View("EditType", model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveHoliday(int? id, HolidayInput holiday, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("holidays");
        Flash(await leave.SaveHolidayAsync(id, holiday, ct), "Holiday saved.");
        return Back("holidays", holiday.Date.Year);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteHoliday(int id, int year, CancellationToken ct)
    {
        Flash(await leave.DeleteHolidayAsync(id, ct), "Holiday deleted.");
        return Back("holidays", year);
    }

    /// <summary>Balance breakdown for one employee (used by the adjust/encash dialog).</summary>
    public async Task<IActionResult> Employee(int id, int year, CancellationToken ct) =>
        Json(await leave.BalancesForEmployeeAsync(id, year, ct));
}

[RequireFeature(FeatureCodes.Attendance), RequirePermission(TenantPermissions.AttendanceView)]
public sealed class AttendanceController(IAttendanceService attendance, IStructureService structure, IOvertimeService overtime) : OrgController
{
    public async Task<IActionResult> Index(DateOnly? date, int? departmentId, string? search, OvertimeStatus? otStatus = OvertimeStatus.Pending, CancellationToken ct = default)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        var d = date is { } x && x <= today ? x : today;
        var (rows, summary) = await attendance.DayAsync(d, departmentId, search, ct);
        ViewBag.Date = d;
        ViewBag.Today = today;
        ViewBag.DepartmentId = departmentId;
        ViewBag.Search = search;
        ViewBag.Summary = summary;
        ViewBag.Departments = (await structure.LookupsAsync(ct)).Departments;
        ViewBag.OtStatus = otStatus;
        ViewBag.Overtime = await overtime.ListAsync(otStatus, 1, ct);
        return View(rows);
    }

    public async Task<IActionResult> Register(int? year, int? month, int? departmentId, CancellationToken ct)
    {
        var today = await attendance.TodayForOrganizationAsync(ct);
        var y = year ?? today.Year;
        var m = Math.Clamp(month ?? today.Month, 1, 12);
        ViewBag.DepartmentId = departmentId;
        ViewBag.Departments = (await structure.LookupsAsync(ct)).Departments;
        ViewBag.Today = today;
        return View(await attendance.RegisterAsync(y, m, departmentId, null, ct));
    }

    public async Task<IActionResult> Export(int year, int month, int? departmentId, CancellationToken ct)
    {
        var file = await attendance.ExportRegisterAsync(year, Math.Clamp(month, 1, 12), departmentId, ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    [HttpPost, RequirePermission(TenantPermissions.AttendanceManage)]
    public async Task<IActionResult> Regularize(int employeeId, RegularizeInput regularize, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect($"/org/attendance?date={regularize.Date:yyyy-MM-dd}");
        }
        Flash(await attendance.RegularizeAsync(employeeId, regularize, ct), "Attendance regularized.");
        return Redirect($"/org/attendance?date={regularize.Date:yyyy-MM-dd}");
    }

    [HttpPost, RequirePermission(TenantPermissions.AttendanceManage)]
    public async Task<IActionResult> ClearRegularization(int employeeId, DateOnly date, CancellationToken ct)
    {
        Flash(await attendance.ClearRegularizationAsync(employeeId, date, ct), "Regularization removed; attendance recalculated from punches.");
        return Redirect($"/org/attendance?date={date:yyyy-MM-dd}");
    }

    [HttpPost, RequireFeature(FeatureCodes.Overtime), RequirePermission(TenantPermissions.AttendanceManage)]
    public async Task<IActionResult> Overtime(int id, bool approve, int? approvedMinutes, string? notes, CancellationToken ct)
    {
        Flash(await overtime.DecideAsync(id, approve, approvedMinutes, notes, asHr: true, ct), approve ? "Overtime approved." : "Overtime rejected.");
        return Redirect("/org/attendance#overtime");
    }
}

[RequireFeature(FeatureCodes.Shifts), RequirePermission(TenantPermissions.AttendanceManage)]
public sealed class ShiftsController(IShiftService shifts, IEmployeeService employees, IAttendanceService attendance) : OrgController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Assignments = await shifts.CurrentAssignmentsAsync(ct);
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        ViewBag.Today = await attendance.TodayForOrganizationAsync(ct);
        return View(await shifts.ShiftsAsync(ct));
    }

    [HttpGet("/org/shifts/edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var model = id is null ? new ShiftInput() : await shifts.GetAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost("/org/shifts/edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, ShiftInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await shifts.SaveAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Shift saved."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Assign(List<int> employeeIds, int shiftId, DateOnly effectiveFrom, CancellationToken ct)
    {
        Flash(await shifts.AssignAsync(employeeIds, shiftId, effectiveFrom, ct), $"Shift assigned to {employeeIds.Count} employee(s).");
        return RedirectToAction(nameof(Index));
    }
}
