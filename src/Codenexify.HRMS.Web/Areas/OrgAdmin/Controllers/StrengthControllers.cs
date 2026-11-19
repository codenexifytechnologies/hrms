using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Strength;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Office locations and GPS / geo-fenced attendance settings.</summary>
[RequireFeature(FeatureCodes.GpsAttendance), RequirePermission(TenantPermissions.AttendanceManage)]
[Route("org/attendance/locations")]
public sealed class LocationsController(IOfficeLocationService locations, IStructureService structure, IEmployeeService employees,
    IFeatureAccessService features, Application.Time.IAttendanceService attendance) : OrgController
{
    private async Task<IReadOnlySet<string>> FeaturesAsync(CancellationToken ct) => await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct);

    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date, CancellationToken ct)
    {
        var f = await FeaturesAsync(ct);
        var day = date ?? await attendance.TodayForOrganizationAsync(ct);
        ViewBag.Mode = await locations.ModeAsync(ct);
        ViewBag.CanEnforce = f.Contains(FeatureCodes.GeoFencing);
        ViewBag.Multiple = f.Contains(FeatureCodes.MultiLocation);
        ViewBag.Branches = (await structure.LookupsAsync(ct)).Branches;
        ViewBag.Exempt = await locations.ExemptEmployeesAsync(ct);
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        ViewBag.Date = day;
        ViewBag.Punches = await locations.RecentPunchesAsync(day, ct);
        return View(await locations.ListAsync(ct));
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(int? id, OfficeLocationInput location, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/org/attendance/locations"); }
        Flash(await locations.SaveAsync(id, location, (await FeaturesAsync(ct)).Contains(FeatureCodes.MultiLocation), ct), "Location saved.");
        return Redirect("/org/attendance/locations");
    }

    [HttpPost("mode")]
    public async Task<IActionResult> Mode(GeoAttendanceMode mode, CancellationToken ct)
    {
        if (mode == GeoAttendanceMode.Enforce && !(await FeaturesAsync(ct)).Contains(FeatureCodes.GeoFencing))
        { TempData["Error"] = "Geo-fencing is not included in your plan."; return Redirect("/org/attendance/locations"); }
        Flash(await locations.SetModeAsync(mode, ct), mode switch
        {
            GeoAttendanceMode.Off => "GPS attendance switched off. Locations are no longer recorded.",
            GeoAttendanceMode.Record => "Locations are now recorded with each clock-in/out.",
            _ => "Geo-fence enforced: clock-ins must be within an office radius."
        });
        return Redirect("/org/attendance/locations");
    }

    [HttpPost("exempt")]
    public async Task<IActionResult> Exempt(int employeeId, bool exempt, CancellationToken ct)
    {
        Flash(await locations.SetExemptAsync(employeeId, exempt, ct), exempt ? "Employee exempted from the geo-fence." : "Exemption removed.");
        return Redirect("/org/attendance/locations");
    }
}

/// <summary>Asset register: add, assign, take back, repair/retire.</summary>
[RequireFeature(FeatureCodes.Assets), RequirePermission(TenantPermissions.AssetsManage)]
[Route("org/assets")]
public sealed class AssetsController(IAssetService assets, IEmployeeService employees) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AssetQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.Categories = await assets.CategoriesAsync(ct);
        return View(await assets.ListAsync(query, ct));
    }

    [HttpGet("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var model = id is null ? new AssetInput() : await assets.GetAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.Categories = await assets.CategoriesAsync(ct);
        if (id is int x)
        {
            ViewBag.History = await assets.HistoryAsync(x, ct);
            ViewBag.Row = (await assets.ListAsync(new AssetQuery { Search = model.AssetTag }, ct)).Items.FirstOrDefault(r => r.Id == x);
            ViewBag.Employees = await employees.ManagersAsync(null, ct);
        }
        return View(model);
    }

    [HttpPost("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, AssetInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await assets.SaveAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Asset saved."; return Redirect($"/org/assets/edit/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        ViewBag.Categories = await assets.CategoriesAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, int employeeId, DateOnly? expectedReturn, string? notes, CancellationToken ct)
    {
        Flash(await assets.AssignAsync(id, employeeId, expectedReturn, notes, ct), "Assigned. The employee will be asked to confirm receipt.");
        return Redirect($"/org/assets/edit/{id}");
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, AssetCondition condition, string? notes, CancellationToken ct)
    {
        Flash(await assets.ReturnAsync(id, condition, notes, ct), "Asset taken back.");
        return Redirect($"/org/assets/edit/{id}");
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> Status(int id, AssetStatus status, CancellationToken ct)
    {
        Flash(await assets.SetStatusAsync(id, status, ct), "Status updated.");
        return Redirect($"/org/assets/edit/{id}");
    }
}

/// <summary>Pulse, engagement and exit surveys.</summary>
[RequireFeature(FeatureCodes.PulseSurveys), RequirePermission(TenantPermissions.SurveysManage)]
[Route("org/surveys")]
public sealed class SurveysController(ISurveyService surveys, IStructureService structure, IFeatureAccessService features) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await surveys.ListAsync(ct));

    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        ViewBag.Departments = (await structure.LookupsAsync(ct)).Departments;
        ViewBag.Exit = (await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct)).Contains(FeatureCodes.ExitSurvey);
        return View(new SurveyInput());
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(SurveyInput survey, CancellationToken ct)
    {
        var exit = (await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct)).Contains(FeatureCodes.ExitSurvey);
        if (ModelState.IsValid)
        {
            var r = await surveys.CreateAsync(survey, exit, ct);
            if (r.Succeeded) { TempData["Success"] = "Draft saved. Publish it when ready."; return Redirect($"/org/surveys/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Departments = (await structure.LookupsAsync(ct)).Departments;
        ViewBag.Exit = exit;
        return View(survey);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Results(int id, CancellationToken ct)
    {
        var r = await surveys.ResultsAsync(id, ct);
        return r is null ? NotFound() : View(r);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct) { Flash(await surveys.PublishAsync(id, ct), "Published — everyone in the audience has been notified."); return Redirect($"/org/surveys/{id}"); }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct) { Flash(await surveys.CloseAsync(id, ct), "Survey closed."); return Redirect($"/org/surveys/{id}"); }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { Flash(await surveys.DeleteDraftAsync(id, ct), "Draft deleted."); return Redirect("/org/surveys"); }
}

/// <summary>Anonymous feedback inbox.</summary>
[RequireFeature(FeatureCodes.AnonymousFeedback), RequirePermission(TenantPermissions.FeedbackManage)]
[Route("org/feedback")]
public sealed class FeedbackController(IFeedbackService feedback) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool all, CancellationToken ct)
    {
        ViewBag.All = all;
        return View(await feedback.ListAsync(!all, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Thread(int id, CancellationToken ct)
    {
        var t = await feedback.GetAsync(id, ct);
        return t is null ? NotFound() : View(t);
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Reply(int id, string? body, bool close, CancellationToken ct)
    {
        Flash(await feedback.ReplyAsync(id, body ?? "", close, ct), close ? "Conversation closed." : "Reply sent. The sender sees it when they check with their code.");
        return Redirect($"/org/feedback/{id}");
    }
}

/// <summary>People analytics dashboard and employee timelines.</summary>
[Route("org")]
public sealed class InsightsController(IInsightsService insights, IEmployeeService employees) : OrgController
{
    [HttpGet("analytics"), RequireFeature(FeatureCodes.PeopleAnalytics), RequirePermission(TenantPermissions.AnalyticsView)]
    public async Task<IActionResult> Analytics(CancellationToken ct) => View(await insights.DashboardAsync(ct));

    [HttpGet("employees/{id:int}/timeline"), RequireFeature(FeatureCodes.EmployeeTimeline), RequirePermission(TenantPermissions.EmployeesView)]
    public async Task<IActionResult> Timeline(int id, CancellationToken ct)
    {
        var profile = await employees.GetProfileAsync(id, includeSensitive: false, ct);
        if (profile is null) return NotFound();
        ViewBag.Employee = profile.Employee;
        return View(await insights.TimelineAsync(id, User.HasPermission(TenantPermissions.PayrollView), ct));
    }
}
