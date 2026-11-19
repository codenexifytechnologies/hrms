using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Domain.Strength;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Onboarding templates and new-joiner onboarding plans.</summary>
[RequireFeature(FeatureCodes.OnboardingAdvanced), RequirePermission(TenantPermissions.OnboardingManage)]
[Route("org/onboarding")]
public sealed class OnboardingController(IOnboardingService onboarding, IEmployeeService employees) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool all, CancellationToken ct)
    {
        ViewBag.All = all;
        ViewBag.Candidates = await onboarding.CandidatesAsync(ct);
        ViewBag.Templates = (await onboarding.TemplatesAsync(ct)).Where(t => t.IsActive).ToList();
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        return View(await onboarding.PlansAsync(!all, ct));
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(int employeeId, int templateId, int? buddyEmployeeId, CancellationToken ct)
    {
        var r = await onboarding.StartAsync(employeeId, templateId, buddyEmployeeId, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/org/onboarding"); }
        TempData["Success"] = "Onboarding started. The manager, buddy and new joiner have been notified of their tasks.";
        return Redirect($"/org/onboarding/{r.Value}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Plan(int id, CancellationToken ct)
    {
        var plan = await onboarding.PlanAsync(id, asHr: true, ct);
        return plan is null ? NotFound() : View(plan);
    }

    [HttpPost("tasks/{taskId:int}/complete")]
    public async Task<IActionResult> Complete(int taskId, int planId, string? note, CancellationToken ct)
    {
        Flash(await onboarding.CompleteTaskAsync(taskId, note, asHr: true, ct), "Task done.");
        return Redirect($"/org/onboarding/{planId}");
    }

    [HttpPost("tasks/{taskId:int}/reopen")]
    public async Task<IActionResult> Reopen(int taskId, int planId, CancellationToken ct)
    {
        Flash(await onboarding.ReopenTaskAsync(taskId, ct), "Task reopened.");
        return Redirect($"/org/onboarding/{planId}");
    }

    [HttpPost("{id:int}/tasks")]
    public async Task<IActionResult> AddTask(int id, OnboardingTaskInput task, DateOnly dueOn, CancellationToken ct)
    {
        Flash(await onboarding.AddTaskAsync(id, task, dueOn, ct), "Task added.");
        return Redirect($"/org/onboarding/{id}");
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        Flash(await onboarding.CancelPlanAsync(id, ct), "Onboarding cancelled.");
        return Redirect("/org/onboarding");
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct) => View(await onboarding.TemplatesAsync(ct));

    [HttpGet("templates/edit/{id:int?}")]
    public async Task<IActionResult> Template(int? id, CancellationToken ct)
    {
        var model = id is null ? new OnboardingTemplateInput() : await onboarding.GetTemplateAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost("templates/edit/{id:int?}")]
    public async Task<IActionResult> Template(int? id, OnboardingTemplateInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await onboarding.SaveTemplateAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Template saved."; return Redirect("/org/onboarding/templates"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost("templates/{id:int}/delete")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        Flash(await onboarding.DeleteTemplateAsync(id, ct), "Template deleted. Onboarding already started keeps its tasks.");
        return Redirect("/org/onboarding/templates");
    }
}

/// <summary>Selfie clock-in: settings and the day's photos for HR review.</summary>
[RequireFeature(FeatureCodes.SelfieClockIn), RequirePermission(TenantPermissions.AttendanceView)]
[Route("org/attendance/selfies")]
public sealed class SelfiesController(ISelfieService selfies, Application.Time.IAttendanceService attendance) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? date, CancellationToken ct)
    {
        var day = date ?? await attendance.TodayForOrganizationAsync(ct);
        ViewBag.Date = day;
        ViewBag.Settings = await selfies.SettingsAsync(ct);
        ViewBag.CanManage = User.HasPermission(TenantPermissions.AttendanceManage);
        return View(await selfies.ForDayAsync(day, ct));
    }

    [HttpPost("settings"), RequirePermission(TenantPermissions.AttendanceManage)]
    public async Task<IActionResult> Settings(bool required, int retentionDays, CancellationToken ct)
    {
        Flash(await selfies.SaveSettingsAsync(required, retentionDays, ct), required ? "Selfie clock-in is on: employees take a photo with each punch." : "Selfie clock-in is off.");
        return Redirect("/org/attendance/selfies");
    }

    [HttpGet("{punchId:int}")]
    public async Task<IActionResult> Photo(int punchId, CancellationToken ct)
    {
        var f = await selfies.OpenAsync(punchId, ct);
        if (f is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return File(f.Content, "image/jpeg");
    }
}

/// <summary>Automatic reminder rules (advanced notification engine).</summary>
[RequireFeature(FeatureCodes.NotificationsAdvanced), RequirePermission(TenantPermissions.NotificationRulesManage)]
[Route("org/reminders")]
public sealed class RemindersController(IReminderService reminders) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? edit, CancellationToken ct)
    {
        ViewBag.EditId = edit;
        ViewBag.Input = edit is int id ? await reminders.GetAsync(id, ct) ?? new ReminderRuleInput() : new ReminderRuleInput();
        return View(await reminders.ListAsync(ct));
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(int? id, ReminderRuleInput rule, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/org/reminders"); }
        Flash(await reminders.SaveAsync(id, rule, ct), "Reminder rule saved. It's checked every hour.");
        return Redirect("/org/reminders");
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await reminders.DeleteAsync(id, ct), "Rule deleted.");
        return Redirect("/org/reminders");
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var sent = await reminders.RunAsync(ct);
        TempData["Success"] = sent == 0 ? "No reminders are due right now." : $"{sent} reminder(s) sent.";
        return Redirect("/org/reminders");
    }
}

/// <summary>Report summaries emailed weekly or monthly (advanced reports).</summary>
[RequireFeature(FeatureCodes.ReportsAdvanced), RequirePermission(TenantPermissions.ReportsView)]
[Route("org/reports/schedules")]
public sealed class ReportSchedulesController(IReportScheduleService schedules, IStructureService structure, IConfiguration config) : OrgController
{
    private async Task LookupsAsync(CancellationToken ct)
    {
        var l = await structure.LookupsAsync(ct);
        ViewBag.Departments = l.Departments;
        ViewBag.Branches = l.Branches;
        ViewBag.Recipients = await schedules.RecipientsAsync(ct);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await schedules.ListAsync(ct));

    [HttpGet("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var model = id is null ? new ReportScheduleInput() : await schedules.GetAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, ReportScheduleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await schedules.SaveAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Schedule saved."; return Redirect("/org/reports/schedules"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id, CancellationToken ct)
    {
        var r = await schedules.SendNowAsync(id, config["Platform:PublicUrl"], ct);
        if (r.Succeeded) TempData["Success"] = $"Sent to {r.Value} recipient(s)."; else TempData["Error"] = r.Error;
        return Redirect("/org/reports/schedules");
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await schedules.DeleteAsync(id, ct), "Schedule deleted.");
        return Redirect("/org/reports/schedules");
    }
}

/// <summary>Single sign-on and mobile OTP settings for the organization.</summary>
[RequirePermission(TenantPermissions.SecurityManage)]
[Route("org/security")]
public sealed class SignInSettingsController(Application.Identity.ISignInSettingsService settings) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await settings.GetAsync(ct));

    [HttpPost("")]
    public async Task<IActionResult> Save(Application.Identity.SignInSettingsInput input, CancellationToken ct)
    {
        Flash(await settings.SaveAsync(input, ct), "Sign-in options saved.");
        return Redirect("/org/security");
    }
}

/// <summary>Travel desk: book approved trips, record advances, close trips.</summary>
[RequireFeature(FeatureCodes.TravelDesk), RequirePermission(TenantPermissions.TravelManage)]
[Route("org/travel")]
public sealed class TravelDeskController(ITravelService travel, IEmployeeService employees) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(TravelStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await travel.DeskAsync(status, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Trip(int id, CancellationToken ct)
    {
        var view = await travel.GetAsync(id, isDesk: true, await employees.GetMyEmployeeIdAsync(ct), ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, bool approve, string? note, CancellationToken ct)
    {
        Flash(await travel.DecideAsync(id, approve, note, isDesk: true, ct), approve ? "Approved." : "Rejected.");
        return Redirect($"/org/travel/{id}");
    }

    [HttpPost("{id:int}/book")]
    public async Task<IActionResult> Book(int id, string? details, decimal? bookedCost, IFormFile? ticket, CancellationToken ct)
    {
        var file = ticket is { Length: > 0 } ? new UploadedFile(ticket.OpenReadStream(), ticket.FileName, ticket.Length) : null;
        Flash(await travel.BookAsync(id, details ?? "", bookedCost, file, ct), "Booking saved. The employee has been notified.");
        return Redirect($"/org/travel/{id}");
    }

    [HttpPost("{id:int}/advance")]
    public async Task<IActionResult> Advance(int id, decimal amount, DateOnly paidOn, CancellationToken ct)
    {
        Flash(await travel.RecordAdvanceAsync(id, amount, paidOn, ct), "Advance recorded.");
        return Redirect($"/org/travel/{id}");
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        Flash(await travel.CompleteAsync(id, ct), "Trip closed. The employee was asked to claim expenses.");
        return Redirect($"/org/travel/{id}");
    }

    [HttpGet("{id:int}/ticket")]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
    {
        var t = await travel.TicketAsync(id, isDesk: true, null, ct);
        return t is null ? NotFound() : File(t.Content, t.ContentType, t.FileName);
    }
}
