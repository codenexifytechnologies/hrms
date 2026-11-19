using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Letter templates, issuing letters as PDF, and the register of issued letters.</summary>
[RequireFeature(FeatureCodes.DocumentsLetters), RequirePermission(TenantPermissions.LettersManage)]
[Route("org/letters")]
public sealed class LettersController(ILetterService letters, IEmployeeService employees, IDocumentService documents,
    Application.Tenancy.ITenantAdminService admin, Application.Strength.ISignatureService signatures, Application.Subscriptions.IFeatureAccessService features) : OrgController
{
    public override async Task OnActionExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context, Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        ViewBag.ESign = await features.IsEnabledAsync(User.GetOrganizationId()!.Value, FeatureCodes.ESignature, HttpContext.RequestAborted);
        await base.OnActionExecutionAsync(context, next);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int page, CancellationToken ct)
    {
        ViewBag.Search = search;
        var list = await letters.ListAsync(null, search, page, ct);
        ViewBag.Signatures = ViewBag.ESign is true ? await signatures.ForLettersAsync(list.Items.Select(l => l.Id), ct) : new Dictionary<int, Application.Strength.SignatureRow>();
        return View(list);
    }

    // ------------------------------------------------------------------ e-signature

    [HttpPost("{id:int}/sign-request"), RequireFeature(FeatureCodes.ESignature)]
    public async Task<IActionResult> RequestSignature(int id, string? message, CancellationToken ct)
    {
        Flash(await signatures.RequestAsync(id, message, ct), "Signature requested. The employee has been notified.");
        return Redirect("/org/letters/signatures");
    }

    [HttpGet("signatures"), RequireFeature(FeatureCodes.ESignature)]
    public async Task<IActionResult> Signatures(Domain.Strength.SignatureStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await signatures.ListAsync(status, ct));
    }

    [HttpPost("signatures/{id:int}/cancel"), RequireFeature(FeatureCodes.ESignature)]
    public async Task<IActionResult> CancelSignature(int id, CancellationToken ct)
    {
        Flash(await signatures.CancelAsync(id, ct), "Signature request cancelled.");
        return Redirect("/org/letters/signatures");
    }

    [HttpGet("signed/{documentId:int}")]
    public async Task<IActionResult> Signed(int documentId, CancellationToken ct)
    {
        // Only documents produced by an e-signature are served here.
        var rows = await signatures.ListAsync(Domain.Strength.SignatureStatus.Signed, ct);
        if (rows.All(r => r.SignedDocumentId != documentId)) return NotFound();
        var doc = await documents.OpenAsync(documentId, null, ct);
        return doc is null ? NotFound() : File(doc.Content, doc.ContentType, doc.FileName);
    }

    // ------------------------------------------------------------------ templates

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct) => View(await letters.TemplatesAsync(activeOnly: false, ct));

    [HttpGet("templates/edit/{id:int?}")]
    public async Task<IActionResult> Template(int? id, CancellationToken ct)
    {
        var model = id is null ? new LetterTemplateInput() : await letters.GetTemplateAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.Placeholders = letters.Placeholders;
        return View(model);
    }

    [HttpPost("templates/edit/{id:int?}")]
    public async Task<IActionResult> Template(int? id, LetterTemplateInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await letters.SaveTemplateAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Template saved."; return Redirect("/org/letters/templates"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        ViewBag.Placeholders = letters.Placeholders;
        return View(model);
    }

    [HttpPost("templates/{id:int}/delete")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        Flash(await letters.DeleteTemplateAsync(id, ct), "Template deleted. Letters already issued are kept.");
        return Redirect("/org/letters/templates");
    }

    // ------------------------------------------------------------------ issue

    private async Task LoadIssueLookupsAsync(int? employeeId, CancellationToken ct)
    {
        ViewBag.Templates = await letters.TemplatesAsync(activeOnly: true, ct);
        ViewBag.EmployeeId = employeeId;
        ViewBag.Employee = employeeId is int id ? (await employees.GetProfileAsync(id, includeSensitive: false, ct))?.Employee : null;
        if (employeeId is null)
            ViewBag.Employees = await employees.ManagersAsync(null, ct);
    }

    [HttpGet("issue")]
    public async Task<IActionResult> Issue(int? employeeId, CancellationToken ct)
    {
        await LoadIssueLookupsAsync(employeeId, ct);
        var me = await admin.GetMeAsync(ct);
        return View(new LetterIssueInput { SignatoryName = me?.FullName ?? "" });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(int employeeId, LetterIssueInput issue, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadIssueLookupsAsync(employeeId, ct);
            return View("Issue", issue);
        }
        // Coming from the template choice: always start from the template text.
        if (Request.Form.ContainsKey("fromTemplate")) { issue.Subject = null; issue.Body = null; }
        var r = await letters.PreviewAsync(employeeId, issue, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/org/letters/issue?employeeId={employeeId}"); }
        ViewBag.EmployeeId = employeeId;
        ViewBag.Employee = (await employees.GetProfileAsync(employeeId, includeSensitive: false, ct))?.Employee;
        ViewBag.Input = issue;
        return View(r.Value);
    }

    [HttpPost("issue")]
    public async Task<IActionResult> IssuePost(int employeeId, LetterIssueInput issue, CancellationToken ct)
    {
        var r = await letters.IssueAsync(employeeId, issue, ct);
        if (!r.Succeeded)
        {
            TempData["Error"] = r.Error;
            var preview = await letters.PreviewAsync(employeeId, issue, ct);
            if (!preview.Succeeded) return Redirect($"/org/letters/issue?employeeId={employeeId}");
            ViewBag.EmployeeId = employeeId;
            ViewBag.Employee = (await employees.GetProfileAsync(employeeId, includeSensitive: false, ct))?.Employee;
            ViewBag.Input = issue;
            return View("Preview", preview.Value);
        }
        TempData["Success"] = issue.ShareWithEmployee ? "Letter issued and shared with the employee." : "Letter issued (not shared with the employee).";
        return Redirect($"/org/letters?highlight={r.Value}");
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, bool inline, CancellationToken ct)
    {
        if (await letters.DocumentIdAsync(id, ct) is not int documentId) return NotFound();
        var doc = await documents.OpenAsync(documentId, null, ct);
        if (doc is null) return NotFound();
        // Inline opens the browser's PDF viewer; otherwise it is saved as a file.
        return inline ? File(doc.Content, doc.ContentType) : File(doc.Content, doc.ContentType, doc.FileName);
    }

    [HttpPost("{id:int}/revoke")]
    public async Task<IActionResult> Revoke(int id, string reason, CancellationToken ct)
    {
        Flash(await letters.RevokeAsync(id, reason, ct), "Letter revoked and withdrawn from the employee's documents.");
        return Redirect("/org/letters");
    }
}
