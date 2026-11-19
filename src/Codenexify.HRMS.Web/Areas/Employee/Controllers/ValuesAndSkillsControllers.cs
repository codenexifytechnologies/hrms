using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Core values: what the company stands for and public shout-outs tied to a value.</summary>
[RequireFeature(FeatureCodes.CoreValues)]
[Route("me/values")]
public sealed class ValuesController(IEmployeeService employees, ICoreValueService values) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.ValuesManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? value, CancellationToken ct)
    {
        ViewBag.Me = await MyEmployeeIdAsync(ct);
        ViewBag.IsHr = IsHr;
        ViewBag.Values = await values.ValuesAsync(false, 90, ct);
        ViewBag.Top = await values.TopRecognisedAsync(90, 5, ct);
        ViewBag.Filter = value;
        ViewBag.Colleagues = await Employees.ManagersAsync(ViewBag.Me as int?, ct);
        return View(await values.FeedAsync(value, null, 60, ct));
    }

    [HttpPost("")]
    public async Task<IActionResult> Shout(ShoutoutInput shout, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/values"); }
        Flash(await values.ShoutAsync(me, shout, ct), "Shout-out posted. Thanks for recognising a colleague!");
        return Redirect("/me/values");
    }

    [HttpPost("{id:int}/remove")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        Flash(await values.RemoveAsync(id, await MyEmployeeIdAsync(ct), IsHr, ct), "Shout-out removed.");
        return Redirect("/me/values");
    }
}

/// <summary>Skills: my skills and gaps for my job title; managers assess their team; HR sees anyone.</summary>
[RequireFeature(FeatureCodes.Skills)]
[Route("me/skills")]
public sealed class SkillsController(IEmployeeService employees, ISkillService skills) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.SkillsManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        if (me is null) return IsHr ? Redirect("/org/skills") : NotLinked();
        ViewBag.Catalog = await skills.CatalogAsync(false, ct);
        ViewBag.Team = await skills.TeamAsync(me.Value, ct);
        return View(await skills.ProfileAsync(me.Value, me, IsHr, ct));
    }

    [HttpPost("")]
    public async Task<IActionResult> SetLevel(int skillId, int level, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await skills.SetSelfLevelAsync(me, skillId, level, ct), level == 0 ? "Removed." : "Saved.");
        return Redirect("/me/skills");
    }

    [HttpGet("people/{employeeId:int}")]
    public async Task<IActionResult> Person(int employeeId, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        if (me == employeeId) return Redirect("/me/skills");
        var profile = await skills.ProfileAsync(employeeId, me, IsHr, ct);
        if (profile is null) return NotFound();
        ViewBag.Catalog = await skills.CatalogAsync(false, ct);
        return View(profile);
    }

    [HttpPost("people/{employeeId:int}")]
    public async Task<IActionResult> Assess(int employeeId, int skillId, int level, string? note, CancellationToken ct)
    {
        Flash(await skills.AssessAsync(await MyEmployeeIdAsync(ct), IsHr, employeeId, skillId, level, note, ct), level == 0 ? "Assessment cleared." : "Assessment saved.");
        return Redirect($"/me/skills/people/{employeeId}");
    }
}
