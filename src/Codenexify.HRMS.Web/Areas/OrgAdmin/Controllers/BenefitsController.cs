using Codenexify.HRMS.Application.Benefits;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Perks & benefits: plans, eligibility, enrolments, dependents, policy documents and the insurer roster.</summary>
[RequireFeature(FeatureCodes.Benefits), RequirePermission(TenantPermissions.BenefitsManage)]
[Route("org/benefits")]
public sealed class BenefitsController(IBenefitService benefits, IStructureService structure) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await benefits.ListAsync(ct));

    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        await LookupsAsync(ct);
        return View("Edit", new BenefitPlanInput());
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(BenefitPlanInput model, CancellationToken ct) => await SaveAsync(null, model, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await benefits.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, BenefitPlanInput model, CancellationToken ct) => await SaveAsync(id, model, ct);

    private async Task<IActionResult> SaveAsync(int? id, BenefitPlanInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await benefits.SaveAsync(id, model, ct);
            if (r.Succeeded)
            {
                TempData["Success"] = id is null ? "Plan created." : "Plan saved.";
                return Redirect($"/org/benefits/{r.Value}");
            }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View("Edit", model);
    }

    private async Task LookupsAsync(CancellationToken ct)
    {
        var (branches, departments, _) = await structure.LookupsAsync(ct);
        ViewBag.Branches = branches;
        ViewBag.Departments = departments;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Plan(int id, CancellationToken ct)
    {
        var view = await benefits.GetAsync(id, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id, int employeeId, DateOnly startsOn, CancellationToken ct)
    {
        Flash(await benefits.EnrollAsync(id, employeeId, startsOn, ct), "Employee enrolled.");
        return Redirect($"/org/benefits/{id}");
    }

    [HttpPost("{id:int}/sync")]
    public async Task<IActionResult> Sync(int id, CancellationToken ct)
    {
        var changes = await benefits.SyncAsync(id, ct);
        TempData["Success"] = changes == 0 ? "Everyone is up to date." : $"{changes} membership change(s) made.";
        return Redirect($"/org/benefits/{id}");
    }

    [HttpPost("enrollments/{enrollmentId:int}/end")]
    public async Task<IActionResult> End(int enrollmentId, int planId, DateOnly lastDay, string reason, CancellationToken ct)
    {
        Flash(await benefits.EndAsync(enrollmentId, lastDay, reason, ct), "Membership ended.");
        return Redirect($"/org/benefits/{planId}");
    }

    [HttpPost("enrollments/{enrollmentId:int}/dependents")]
    public async Task<IActionResult> Dependents(int enrollmentId, int planId, List<DependentInput> dependents, CancellationToken ct)
    {
        Flash(await benefits.SaveDependentsAsync(enrollmentId, null, Filled(dependents), ct), "Dependents saved.");
        return Redirect($"/org/benefits/{planId}");
    }

    [HttpPost("{id:int}/document")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Document(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null) { TempData["Error"] = "Choose a PDF file."; return Redirect($"/org/benefits/{id}"); }
        await using var stream = file.OpenReadStream();
        Flash(await benefits.UploadDocumentAsync(id, new UploadedFile(stream, file.FileName, file.Length), ct), "Policy document uploaded.");
        return Redirect($"/org/benefits/{id}");
    }

    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var doc = await benefits.DocumentAsync(id, null, ct);
        return doc is null ? NotFound() : File(doc.Content, "application/pdf", doc.FileName);
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var bytes = await benefits.ExportMembersAsync(id, ct);
        return bytes is null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"benefit-{id}-members.xlsx");
    }

    /// <summary>Blank rows from the form (unused slots) are ignored.</summary>
    internal static List<DependentInput> Filled(List<DependentInput>? rows) =>
        (rows ?? []).Where(d => !string.IsNullOrWhiteSpace(d.Name)).ToList();
}
