using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Engagement;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers.Api;

/// <summary>Base for the mobile self-service API: bearer tokens only, scoped to the signed-in user.</summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Authorize(Policy = Policies.TenantApi)]
[EnableRateLimiting("api")]
public abstract class SelfServiceApiController(IEmployeeService employees) : ControllerBase
{
    protected async Task<int?> MyEmployeeIdAsync(CancellationToken ct) => await employees.GetMyEmployeeIdAsync(ct);

    protected ObjectResult NotLinked() => Problem(statusCode: StatusCodes.Status409Conflict, title: "Not linked to an employee",
        detail: "This login is not linked to an employee record. Ask HR to link it.");

    protected IActionResult FromResult(Result r, object? ok = null) =>
        r.Succeeded ? Ok(ok ?? new { ok = true }) : Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Request rejected", detail: r.Error);

    protected static object Page<T>(PagedResult<T> p) => new { items = p.Items, p.Page, p.PageSize, totalCount = p.TotalCount, totalPages = p.TotalPages };
}

[Route("api/v1/me/leave"), RequireFeature(FeatureCodes.Leave)]
public sealed class LeaveApiController(IEmployeeService employees, ILeaveService leave, IAttendanceService attendance) : SelfServiceApiController(employees)
{
    [HttpGet("balances")]
    public async Task<IActionResult> Balances(int? year, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var y = year ?? (await attendance.TodayForOrganizationAsync(ct)).Year;
        var rows = await leave.BalancesForEmployeeAsync(me, y, ct);
        return Ok(rows.Select(b => new { b.LeaveTypeId, b.LeaveType, b.Code, b.IsPaid, b.Available, b.Pending, b.Used, b.Accrued, b.Opening, b.Adjusted }));
    }

    [HttpGet("")]
    public async Task<IActionResult> Requests(int? year, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var y = year ?? (await attendance.TodayForOrganizationAsync(ct)).Year;
        var rows = await leave.ForEmployeeAsync(me, y, ct);
        return Ok(rows.Select(r => new { r.Id, r.LeaveType, r.From, r.To, r.IsHalfDay, session = r.Session?.ToString(), r.Days, r.Reason, status = r.Status.ToString(), r.AppliedAtUtc, r.DecidedBy, r.DecisionNotes }));
    }

    [HttpPost("")]
    public async Task<IActionResult> Apply(LeaveApplyInput input, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var r = await leave.ApplyAsync(me, input, byEmployee: true, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : FromResult(r);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return FromResult(await leave.CancelAsync(id, me, ct));
    }

    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken ct) =>
        Ok((await leave.LeaveTypesAsync(activeOnly: true, ct)).Select(t => new { t.Id, t.Name, t.Code }));
}

[Route("api/v1/me/attendance"), RequireFeature(FeatureCodes.Attendance)]
public sealed class AttendanceApiController(IEmployeeService employees, IAttendanceService attendance) : SelfServiceApiController(employees)
{
    private static object Today(TodayStatus s) => new
    {
        s.Date, s.IsClockedIn, s.WorkedMinutes, status = s.Status.ToString(), s.ShiftName, s.ShiftStart, s.ShiftEnd, s.IsWorkingDay, s.HolidayName,
        punches = s.Punches.Select(p => new { direction = p.Direction.ToString(), at = p.LocalTime })
    };

    [HttpGet("today")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return Ok(Today(await attendance.TodayAsync(me, ct)));
    }

    /// <param name="Selfie">Base64 JPEG (max 400 KB) when the organization requires selfie clock-in.</param>
    public sealed record PunchRequest(decimal? Latitude, decimal? Longitude, int? AccuracyMeters, string? Selfie = null);

    /// <summary>Clock in or out (the direction alternates). Send the device location when GPS attendance is on.</summary>
    [HttpPost("punch")]
    public async Task<IActionResult> Punch([FromBody] PunchRequest? request, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var location = request is { Latitude: { } lat, Longitude: { } lng } ? new GeoPoint(lat, lng, request.AccuracyMeters) : null;
        var r = await attendance.PunchAsync(me, PunchSource.Mobile, HttpContext.Connection.RemoteIpAddress?.ToString(), location, SelfieData.Decode(request?.Selfie), ct);
        if (!r.Succeeded) return FromResult(r);
        return Ok(new { direction = r.Value!.Direction.ToString(), at = r.Value.LocalTime, r.Value.AttendanceDate, today = Today(await attendance.TodayAsync(me, ct)) });
    }
}

[Route("api/v1/me/payslips"), RequireFeature(FeatureCodes.Payroll)]
public sealed class PayslipsApiController(IEmployeeService employees, IPayrollRunService runs) : SelfServiceApiController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return Ok((await runs.MyPayslipsAsync(me, ct)).Select(p => new { id = p.DetailId, year = p.Year, month = p.Month, netPay = p.Net }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var slip = await runs.PayslipAsync(id, me, ct);
        if (slip is null) return NotFound();
        var d = slip.Detail;
        object Lines(PayrollLineKind k) => d.Lines.Where(l => l.Kind == k).OrderBy(l => l.SortOrder).Select(l => new { l.Name, l.Amount });
        return Ok(new
        {
            id, year = slip.Run.Year, month = slip.Run.Month, status = slip.Run.Status.ToString(), organization = slip.OrganizationName,
            d.DaysInMonth, d.PaidDays, d.LopDays, d.GrossEarnings, d.TotalDeductions, d.NetPay, regime = d.Regime.ToString(),
            earnings = Lines(PayrollLineKind.Earning), deductions = Lines(PayrollLineKind.Deduction), employerContributions = Lines(PayrollLineKind.EmployerContribution)
        });
    }
}

[Route("api/v1/me/notifications")]
public sealed class NotificationsApiController(IEmployeeService employees, INotificationService notifications) : SelfServiceApiController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> List(int page, CancellationToken ct)
    {
        var p = await notifications.MineAsync(page, ct);
        return Ok(new
        {
            unread = await notifications.UnreadCountAsync(ct),
            items = p.Items.Select(n => new { n.Id, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAtUtc }), p.Page, totalPages = p.TotalPages
        });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        var link = await notifications.OpenAsync(id, ct);
        return Ok(new { link });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return Ok(new { ok = true });
    }
}

[Route("api/v1/wall"), RequireFeature(FeatureCodes.SocialWall)]
public sealed class WallApiController(IEmployeeService employees, ISocialWallService wall) : SelfServiceApiController(employees)
{
    private bool CanModerate => User.HasPermission(TenantPermissions.WallModerate);

    [HttpGet("")]
    public async Task<IActionResult> Feed(int page, Domain.Engagement.PostKind? kind, CancellationToken ct) => Ok(Page(await wall.FeedAsync(page, kind, CanModerate, ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        await wall.GetAsync(id, CanModerate, allComments: true, ct) is { } post ? Ok(post) : NotFound();

    [HttpPost("posts")]
    public async Task<IActionResult> Post(PostInput input, CancellationToken ct)
    {
        var r = await wall.PostAsync(input, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : FromResult(r);
    }

    [HttpPost("praise")]
    public async Task<IActionResult> Praise(PraiseInput input, CancellationToken ct)
    {
        var r = await wall.PraiseAsync(input, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : FromResult(r);
    }

    [HttpPost("{id:int}/like")]
    public async Task<IActionResult> Like(int id, CancellationToken ct)
    {
        var r = await wall.ToggleLikeAsync(id, ct);
        return r.Succeeded ? Ok(new { liked = r.Value }) : FromResult(r);
    }

    public sealed record CommentRequest(string Body);

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> Comment(int id, CommentRequest request, CancellationToken ct) => FromResult(await wall.CommentAsync(id, request.Body, ct));

    public sealed record VoteRequest(int OptionId);

    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> Vote(int id, VoteRequest request, CancellationToken ct) => FromResult(await wall.VoteAsync(id, request.OptionId, ct));
}

[Route("api/v1/directory"), RequireFeature(FeatureCodes.SelfService)]
public sealed class DirectoryApiController(IEmployeeService employees, IPeopleService people) : SelfServiceApiController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Search(string? search, int? departmentId, int page, CancellationToken ct) =>
        Ok(Page(await people.DirectoryAsync(search, departmentId, page, ct)));

    [HttpGet("celebrations")]
    public async Task<IActionResult> Celebrations(int days = 7, CancellationToken ct = default) =>
        Ok((await people.CelebrationsAsync(Math.Clamp(days, 0, 60), ct)).Select(c => new { c.EmployeeId, c.Name, c.Designation, kind = c.Kind.ToString(), c.Date, c.Years, c.IsToday }));
}
