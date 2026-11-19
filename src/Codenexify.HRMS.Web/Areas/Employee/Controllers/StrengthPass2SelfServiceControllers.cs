using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Onboarding tasks for the new joiner, their manager and their buddy.</summary>
[RequireFeature(FeatureCodes.OnboardingAdvanced)]
[Route("me/onboarding")]
public sealed class MyOnboardingController(IEmployeeService employees, IOnboardingService onboarding) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.OnboardingManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await onboarding.MyTasksAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Plan(int id, CancellationToken ct)
    {
        var plan = await onboarding.PlanAsync(id, asHr: false, ct);
        return plan is null ? NotFound() : View(plan);
    }

    [HttpPost("tasks/{taskId:int}/complete")]
    public async Task<IActionResult> Complete(int taskId, string? note, string? returnUrl, CancellationToken ct)
    {
        // Self-service completes only tasks assigned to the user, even for HR staff (they use the HR screen otherwise).
        Flash(await onboarding.CompleteTaskAsync(taskId, note, asHr: false, ct), "Task done.");
        return Redirect(Url.IsLocalUrl(returnUrl) && returnUrl!.StartsWith("/me/onboarding") ? returnUrl : "/me/onboarding");
    }
}

/// <summary>Letters to accept electronically (in-app signing confirmed by an emailed code).</summary>
[RequireFeature(FeatureCodes.ESignature)]
[Route("me/sign")]
public sealed class MySignaturesController(IEmployeeService employees, ISignatureService signatures, IDocumentService documents) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return View(await signatures.MineAsync(me, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Letter(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var v = await signatures.ViewAsync(id, me, ct);
        return v is null ? NotFound() : View(v);
    }

    [HttpPost("{id:int}/code")]
    public async Task<IActionResult> Code(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await signatures.SendCodeAsync(id, me, ct), "A 6-digit code was sent to your email. It's valid for 10 minutes.");
        return Redirect($"/me/sign/{id}");
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Sign(int id, string? code, string? typedName, bool agree, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var r = await signatures.SignAsync(id, me, code ?? "", typedName ?? "", agree, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        Flash(r, "Signed. A signed copy has been added to your documents.");
        return Redirect(r.Succeeded ? "/me/sign" : $"/me/sign/{id}");
    }

    [HttpPost("{id:int}/decline")]
    public async Task<IActionResult> Decline(int id, string? reason, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await signatures.DeclineAsync(id, me, reason ?? "", ct), "HR has been told you declined.");
        return Redirect("/me/sign");
    }

    [HttpGet("signed/{documentId:int}")]
    public async Task<IActionResult> Signed(int documentId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var doc = await documents.OpenAsync(documentId, me, ct);
        return doc is null ? NotFound() : File(doc.Content, doc.ContentType, doc.FileName);
    }
}

/// <summary>Employee travel requests; managers approve their team's requests here too.</summary>
[RequireFeature(FeatureCodes.TravelDesk)]
[Route("me/travel")]
public sealed class MyTravelController(IEmployeeService employees, ITravelService travel) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        ViewBag.HasEmployee = me is not null;
        ViewBag.ToApprove = await travel.ToApproveAsync(ct);
        return View(me is int id ? await travel.MineAsync(id, ct) : []);
    }

    [HttpPost("")]
    public async Task<IActionResult> Submit(TravelInput trip, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/travel"); }
        var r = await travel.SubmitAsync(me, trip, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/me/travel"); }
        TempData["Success"] = "Travel request sent for approval.";
        return Redirect($"/me/travel/{r.Value}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Trip(int id, CancellationToken ct)
    {
        var view = await travel.GetAsync(id, isDesk: false, await MyEmployeeIdAsync(ct), ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, bool approve, string? note, CancellationToken ct)
    {
        Flash(await travel.DecideAsync(id, approve, note, isDesk: false, ct), approve ? "Approved. The travel desk will book it." : "Rejected.");
        return Redirect("/me/travel");
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await travel.CancelAsync(id, me, ct), "Trip cancelled.");
        return Redirect($"/me/travel/{id}");
    }

    [HttpGet("{id:int}/ticket")]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
    {
        var t = await travel.TicketAsync(id, isDesk: false, await MyEmployeeIdAsync(ct), ct);
        return t is null ? NotFound() : File(t.Content, t.ContentType, t.FileName);
    }
}
