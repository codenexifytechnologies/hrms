using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>HR: define the company's core values.</summary>
[RequireFeature(FeatureCodes.CoreValues), RequirePermission(TenantPermissions.ValuesManage)]
[Route("org/values")]
public sealed class CoreValuesController(ICoreValueService values) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? edit, CancellationToken ct)
    {
        ViewBag.Icons = values.Icons;
        ViewBag.EditId = edit;
        ViewBag.Form = edit is int id ? await values.GetForEditAsync(id, ct) ?? new CoreValueInput() : new CoreValueInput();
        return View(await values.ValuesAsync(true, 90, ct));
    }

    [HttpPost("")]
    public async Task<IActionResult> Save(int? id, CoreValueInput value, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/org/values" + (id is null ? "" : $"?edit={id}")); }
        var r = await values.SaveAsync(id, value, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/org/values" + (id is null ? "" : $"?edit={id}")); }
        TempData["Success"] = "Value saved.";
        return Redirect("/org/values");
    }
}

/// <summary>HR: skill catalog, job-title requirements, finding people by skill and the skills matrix.</summary>
[RequireFeature(FeatureCodes.Skills), RequirePermission(TenantPermissions.SkillsManage)]
[Route("org/skills")]
public sealed class SkillsAdminController(ISkillService skills, IStructureService structure) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? edit, int? designationId, int? find, int? minLevel, int? departmentId, CancellationToken ct)
    {
        var (_, departments, designations) = await structure.LookupsAsync(ct);
        ViewBag.Departments = departments;
        ViewBag.Designations = designations;
        ViewBag.EditId = edit;
        ViewBag.Form = edit is int id ? await skills.GetForEditAsync(id, ct) ?? new SkillInput() : new SkillInput();
        ViewBag.DesignationId = designationId;
        ViewBag.Requirements = designationId is int d ? await skills.RequirementsAsync(d, ct) : [];
        ViewBag.Find = find;
        ViewBag.MinLevel = minLevel ?? 3;
        ViewBag.DepartmentId = departmentId;
        ViewBag.Matches = find is int f ? await skills.FindAsync(f, minLevel ?? 3, departmentId, ct) : [];
        return View(await skills.CatalogAsync(true, ct));
    }

    [HttpPost("")]
    public async Task<IActionResult> Save(int? id, SkillInput skill, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/org/skills"); }
        Flash(await skills.SaveAsync(id, skill, ct), "Skill saved.");
        return Redirect("/org/skills");
    }

    [HttpPost("requirements")]
    public async Task<IActionResult> Requirement(int designationId, int skillId, int level, CancellationToken ct)
    {
        Flash(await skills.SetRequirementAsync(designationId, skillId, level, ct), level == 0 ? "Requirement removed." : "Requirement saved.");
        return Redirect($"/org/skills?designationId={designationId}#requirements");
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(int? departmentId, CancellationToken ct) =>
        File(await skills.ExportMatrixAsync(departmentId, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "skills-matrix.xlsx");
}
