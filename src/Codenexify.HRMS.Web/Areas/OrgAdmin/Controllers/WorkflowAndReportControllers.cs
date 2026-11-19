using System.Security.Claims;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Automation rules: when something happens to an employee, notify people, create tasks, start onboarding or enrol in a benefit.</summary>
[RequireFeature(FeatureCodes.WorkflowAutomation), RequirePermission(TenantPermissions.WorkflowsManage)]
[Route("org/workflows")]
public sealed class WorkflowsController(IWorkflowService workflows, IStructureService structure) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await workflows.RulesAsync(ct));

    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        await LookupsAsync(ct);
        return View("Edit", Pad(new WorkflowRuleInput()));
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(WorkflowRuleInput model, CancellationToken ct) => await SaveAsync(null, model, ct);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await workflows.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.Runs = await workflows.RunsAsync(id, ct);
        await LookupsAsync(ct);
        return View(Pad(model));
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Edit(int id, WorkflowRuleInput model, CancellationToken ct) => await SaveAsync(id, model, ct);

    private static WorkflowRuleInput Pad(WorkflowRuleInput m)
    {
        // At least four rows, and always one spare row while the rule has room for another action.
        var target = Math.Min(WorkflowService.MaxActions, Math.Max(4, m.Actions.Count(a => a.Use) + 1));
        while (m.Actions.Count < target) m.Actions.Add(new WorkflowActionInput());
        return m;
    }

    private async Task<IActionResult> SaveAsync(int? id, WorkflowRuleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await workflows.SaveAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Rule saved. It runs every hour; use 'Run now' to try it straight away."; return Redirect($"/org/workflows/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        if (id is int existing) ViewBag.Runs = await workflows.RunsAsync(existing, ct);
        await LookupsAsync(ct);
        return View("Edit", Pad(model));
    }

    private async Task LookupsAsync(CancellationToken ct)
    {
        var (branches, departments, _) = await structure.LookupsAsync(ct);
        ViewBag.Branches = branches;
        ViewBag.Departments = departments;
        ViewBag.Choices = await workflows.ChoicesAsync(ct);
    }

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(int id, CancellationToken ct)
    {
        var n = await workflows.RunAsync(id, ct);
        TempData["Success"] = n == 0 ? "Nothing new matched the rule." : $"Ran for {n} employee(s).";
        return Redirect($"/org/workflows/{id}");
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await workflows.DeleteAsync(id, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/org/workflows/{id}"); }
        TempData["Success"] = "Rule deleted.";
        return Redirect("/org/workflows");
    }
}

/// <summary>Custom reports: pick a data source, columns, filters and grouping; run, export, save and share.</summary>
[RequireFeature(FeatureCodes.CustomReportBuilder), RequirePermission(TenantPermissions.ReportBuilder)]
[Route("org/report-builder")]
public sealed class ReportBuilderController(IReportBuilderService builder, IStructureService structure) : OrgController
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    private bool PayrollAllowed => User.HasPermission(TenantPermissions.PayrollView);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await builder.SavedAsync(UserId, ct));

    [HttpGet("build")]
    public async Task<IActionResult> Build(ReportSource source, int? id, CancellationToken ct)
    {
        SavedReportInput model;
        var isMine = true;
        if (id is int saved)
        {
            var found = await builder.GetAsync(saved, UserId, ct);
            if (found is null) return NotFound();
            (model, isMine) = found.Value;
            await builder.MarkRunAsync(saved, ct);
            ViewBag.Result = await RunAsync(model, ct);
        }
        else
        {
            model = new SavedReportInput { Source = source, Definition = new ReportDefinition { Columns = builder.Fields(source).Take(4).Select(f => f.Key).ToList() } };
        }
        return await ShowAsync(model, id, isMine, ct);
    }

    [HttpPost("build")]
    public async Task<IActionResult> Build(SavedReportInput report, int? id, [FromForm(Name = "do")] string? op, CancellationToken ct)
    {
        // Name is only needed for saving; running or exporting an unnamed report is fine.
        ModelState.Remove(nameof(SavedReportInput.Name));
        if (op == "save")
        {
            if (string.IsNullOrWhiteSpace(report.Name)) { ModelState.AddModelError(string.Empty, "Give the report a name to save it."); return await ShowAsync(report, id, true, ct); }
            var mine = id is int s && (await builder.GetAsync(s, UserId, ct))?.IsMine == true;
            var r = await builder.SaveAsync(mine ? id : null, UserId, report, ct);
            if (!r.Succeeded) { ModelState.AddModelError(string.Empty, r.Error!); return await ShowAsync(report, id, true, ct); }
            TempData["Success"] = "Report saved.";
            return Redirect($"/org/report-builder/build?id={r.Value}");
        }
        var result = await builder.RunAsync(report.Source, report.Definition, PayrollAllowed, ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return await ShowAsync(report, id, true, ct); }
        if (op == "export")
        {
            var bytes = await builder.ExportAsync(result.Value!, string.IsNullOrWhiteSpace(report.Name) ? report.Source.ToString() : report.Name);
            return File(bytes!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{(string.IsNullOrWhiteSpace(report.Name) ? report.Source.ToString() : report.Name)}.xlsx");
        }
        ViewBag.Result = result.Value;
        var isMine = id is null || (await builder.GetAsync(id.Value, UserId, ct))?.IsMine == true;
        return await ShowAsync(report, id, isMine, ct);
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await builder.DeleteAsync(id, UserId, ct), "Report deleted.");
        return Redirect("/org/report-builder");
    }

    private async Task<ReportResult?> RunAsync(SavedReportInput model, CancellationToken ct)
    {
        var r = await builder.RunAsync(model.Source, model.Definition, PayrollAllowed, ct);
        if (!r.Succeeded) ModelState.AddModelError(string.Empty, r.Error!);
        return r.Value;
    }

    private async Task<IActionResult> ShowAsync(SavedReportInput model, int? id, bool isMine, CancellationToken ct)
    {
        var (branches, departments, _) = await structure.LookupsAsync(ct);
        ViewBag.Branches = branches;
        ViewBag.Departments = departments;
        ViewBag.Fields = builder.Fields(model.Source);
        ViewBag.Id = id;
        ViewBag.IsMine = isMine;
        ViewBag.PayrollAllowed = PayrollAllowed;
        return View("Build", model);
    }
}
