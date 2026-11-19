using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Planning;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Headcount plans: positions and budgets by department, filled by linking the people who joined.</summary>
[RequireFeature(FeatureCodes.HeadcountPlanning), RequirePermission(TenantPermissions.HeadcountManage)]
[Route("org/headcount")]
public sealed class HeadcountController(IHeadcountService headcount, IStructureService structure) : OrgController
{
    private bool PayrollAllowed => User.HasPermission(TenantPermissions.PayrollView);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await headcount.PlansAsync(ct));

    [HttpGet("new")]
    public IActionResult New()
    {
        var y = DateTime.Today.Month >= 4 ? DateTime.Today.Year + 1 : DateTime.Today.Year;
        return View("EditPlan", new HeadcountPlanInput { Name = $"FY {y}-{(y + 1) % 100:00}", StartsOn = new DateOnly(y, 4, 1), EndsOn = new DateOnly(y + 1, 3, 31) });
    }

    [HttpPost("new")]
    public Task<IActionResult> New(HeadcountPlanInput model, CancellationToken ct) => SavePlanAsync(null, model, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> EditPlan(int id, CancellationToken ct)
    {
        var m = await headcount.GetPlanAsync(id, ct);
        if (m is null) return NotFound();
        ViewBag.Id = id;
        return View(m);
    }

    [HttpPost("{id:int}/edit")]
    public Task<IActionResult> EditPlan(int id, HeadcountPlanInput model, CancellationToken ct) => SavePlanAsync(id, model, ct);

    private async Task<IActionResult> SavePlanAsync(int? id, HeadcountPlanInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await headcount.SavePlanAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Plan saved."; return Redirect($"/org/headcount/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        return View("EditPlan", model);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Plan(int id, CancellationToken ct)
    {
        var v = await headcount.ViewAsync(id, PayrollAllowed, ct);
        return v is null ? NotFound() : View(v);
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var name = (await headcount.GetPlanAsync(id, ct))?.Name;
        var bytes = await headcount.ExportAsync(id, PayrollAllowed, ct);
        return bytes is null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Headcount plan {name}.xlsx");
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> Status(int id, string step, CancellationToken ct)
    {
        Flash(await headcount.SetStatusAsync(id, step, ct), step switch { "approve" => "Plan approved.", "close" => "Plan closed.", "reopen" => "Plan reopened.", _ => "Plan moved back to draft." });
        return Redirect($"/org/headcount/{id}");
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await headcount.DeletePlanAsync(id, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/org/headcount/{id}"); }
        TempData["Success"] = "Plan deleted.";
        return Redirect("/org/headcount");
    }

    [HttpGet("{id:int}/positions/new")]
    public async Task<IActionResult> NewPosition(int id, CancellationToken ct)
    {
        var plan = await headcount.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();
        return await PositionViewAsync(id, null, new HeadcountPositionInput { TargetStartOn = plan.StartsOn }, ct);
    }

    [HttpPost("{id:int}/positions/new")]
    public Task<IActionResult> NewPosition(int id, HeadcountPositionInput model, CancellationToken ct) => SavePositionAsync(id, null, model, ct);

    [HttpGet("{id:int}/positions/{positionId:int}")]
    public async Task<IActionResult> EditPosition(int id, int positionId, CancellationToken ct)
    {
        var m = await headcount.GetPositionAsync(id, positionId, ct);
        return m is null ? NotFound() : await PositionViewAsync(id, positionId, m, ct);
    }

    [HttpPost("{id:int}/positions/{positionId:int}")]
    public Task<IActionResult> EditPosition(int id, int positionId, HeadcountPositionInput model, CancellationToken ct) => SavePositionAsync(id, positionId, model, ct);

    private async Task<IActionResult> SavePositionAsync(int id, int? positionId, HeadcountPositionInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await headcount.SavePositionAsync(id, positionId, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Position saved."; return Redirect($"/org/headcount/{id}#positions"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        return await PositionViewAsync(id, positionId, model, ct);
    }

    private async Task<IActionResult> PositionViewAsync(int id, int? positionId, HeadcountPositionInput model, CancellationToken ct)
    {
        var plan = await headcount.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();
        var (branches, departments, designations) = await structure.LookupsAsync(ct);
        ViewBag.Branches = branches;
        ViewBag.Departments = departments;
        ViewBag.Designations = designations;
        ViewBag.PlanId = id;
        ViewBag.PlanName = plan.Name;
        ViewBag.Plan = plan;
        ViewBag.PositionId = positionId;
        return View("Position", model);
    }

    [HttpPost("{id:int}/positions/{positionId:int}/delete")]
    public async Task<IActionResult> DeletePosition(int id, int positionId, CancellationToken ct)
    {
        Flash(await headcount.DeletePositionAsync(id, positionId, ct), "Position deleted.");
        return Redirect($"/org/headcount/{id}#positions");
    }

    [HttpPost("{id:int}/positions/{positionId:int}/link")]
    public async Task<IActionResult> Link(int id, int positionId, int employeeId, CancellationToken ct)
    {
        Flash(await headcount.LinkHireAsync(id, positionId, employeeId, ct), "Hire linked to the position.");
        return Redirect($"/org/headcount/{id}#positions");
    }

    [HttpPost("{id:int}/hires/{hireId:int}/unlink")]
    public async Task<IActionResult> Unlink(int id, int hireId, CancellationToken ct)
    {
        Flash(await headcount.UnlinkHireAsync(id, hireId, ct), "Hire unlinked.");
        return Redirect($"/org/headcount/{id}#positions");
    }
}

/// <summary>Pre-boarding: welcome content, what new hires are asked for, and their secure links.</summary>
[RequireFeature(FeatureCodes.Preboarding), RequirePermission(TenantPermissions.PreboardingManage)]
[Route("org/preboarding")]
public sealed class PreboardingController(IPreboardingService preboarding, IConfiguration config, IHostEnvironment env, ILogger<PreboardingController> logger) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await preboarding.JoinersAsync(ct));

    [HttpGet("{employeeId:int}")]
    public async Task<IActionResult> Person(int employeeId, CancellationToken ct)
    {
        var s = await preboarding.StatusAsync(employeeId, ct);
        return s is null ? NotFound() : View(s);
    }

    [HttpPost("{employeeId:int}/send")]
    public async Task<IActionResult> Send(int employeeId, string email, CancellationToken ct)
    {
        var baseUrl = PublicUrl.For(Request, config, env);
        if (baseUrl is null)
        {
            logger.LogError("Pre-boarding link not sent: set Platform:PublicUrl so emailed links point at this site.");
            TempData["Error"] = "The site's public address isn't configured, so links can't be sent. Ask the platform administrator to set Platform:PublicUrl.";
            return Redirect($"/org/preboarding/{employeeId}");
        }
        var r = await preboarding.SendAsync(employeeId, email, baseUrl, ct);
        if (r.Succeeded)
        {
            TempData["Success"] = "Welcome link emailed. Any earlier link has stopped working.";
            // Shown once so HR can also share it another way (e.g. if the email bounces).
            TempData["PreboardingLink"] = r.Value;
        }
        else TempData["Error"] = r.Error;
        return Redirect($"/org/preboarding/{employeeId}");
    }

    [HttpPost("{employeeId:int}/revoke")]
    public async Task<IActionResult> Revoke(int employeeId, CancellationToken ct)
    {
        Flash(await preboarding.RevokeAsync(employeeId, ct), "Link cancelled. It no longer opens.");
        return Redirect($"/org/preboarding/{employeeId}");
    }

    [HttpPost("{employeeId:int}/reopen")]
    public async Task<IActionResult> Reopen(int employeeId, CancellationToken ct)
    {
        Flash(await preboarding.ReopenAsync(employeeId, ct), "Reopened: the new hire can change their answers again.");
        return Redirect($"/org/preboarding/{employeeId}");
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        ViewBag.Items = await preboarding.ItemsAsync(false, ct);
        return View(await preboarding.GetSettingsAsync(ct));
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(PreboardingSettingsInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            Flash(await preboarding.SaveSettingsAsync(model, ct), "Pre-boarding settings saved.");
            return Redirect("/org/preboarding/settings");
        }
        ViewBag.Items = await preboarding.ItemsAsync(false, ct);
        return View(model);
    }

    [HttpGet("items/new")]
    public IActionResult NewItem() => View("Item", new PreboardingItemInput());

    [HttpPost("items/new")]
    public Task<IActionResult> NewItem(PreboardingItemInput model, CancellationToken ct) => SaveItemAsync(null, model, ct);

    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> EditItem(int id, CancellationToken ct)
    {
        var m = await preboarding.GetItemAsync(id, ct);
        if (m is null) return NotFound();
        ViewBag.Id = id;
        return View("Item", m);
    }

    [HttpPost("items/{id:int}")]
    public Task<IActionResult> EditItem(int id, PreboardingItemInput model, CancellationToken ct) => SaveItemAsync(id, model, ct);

    private async Task<IActionResult> SaveItemAsync(int? id, PreboardingItemInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await preboarding.SaveItemAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Item saved."; return Redirect("/org/preboarding/settings#items"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        return View("Item", model);
    }

    [HttpPost("items/{id:int}/delete")]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken ct)
    {
        Flash(await preboarding.DeleteItemAsync(id, ct), "Item deleted.");
        return Redirect("/org/preboarding/settings#items");
    }
}
