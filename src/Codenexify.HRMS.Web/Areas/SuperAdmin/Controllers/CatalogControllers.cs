using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

[RequirePermission(PlatformPermissions.PlansView)]
public sealed class PlansController(IPlanService plans) : PlatformController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await plans.ListAsync(ct));

    [HttpGet, RequirePermission(PlatformPermissions.PlansManage)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadAsync(null, ct);
        return View("Edit", new PlanInput());
    }

    [HttpPost, RequirePermission(PlatformPermissions.PlansManage)]
    public async Task<IActionResult> Create(PlanInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await plans.CreateAsync(model, ct);
            if (result.Succeeded) { TempData["Success"] = $"Plan {model.Name} created."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await LoadAsync(null, ct);
        return View("Edit", model);
    }

    [HttpGet, RequirePermission(PlatformPermissions.PlansManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await plans.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        await LoadAsync(id, ct);
        return View(model);
    }

    [HttpPost, RequirePermission(PlatformPermissions.PlansManage)]
    public async Task<IActionResult> Edit(int id, PlanInput model, CancellationToken ct)
    {
        ModelState.Remove(nameof(PlanInput.Code)); // code is immutable after creation
        if (ModelState.IsValid)
        {
            var result = await plans.UpdateAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = $"Plan {model.Name} saved."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await LoadAsync(id, ct);
        return View(model);
    }

    [HttpPost, RequirePermission(PlatformPermissions.PlansManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => Done(await plans.DeleteAsync(id, ct), "Plan deleted.", RedirectToAction(nameof(Index)));

    private async Task LoadAsync(int? id, CancellationToken ct)
    {
        ViewBag.Id = id;
        ViewBag.Features = await plans.GetFeaturesAsync(ct);
        ViewBag.Parents = (await plans.ListAsync(ct)).Where(p => p.Id != id).ToList();
    }
}

[RequirePermission(PlatformPermissions.PlansView)]
public sealed class FeaturesController(IPlanService plans) : PlatformController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await plans.GetFeaturesAsync(ct));

    [HttpPost, RequirePermission(PlatformPermissions.FeaturesManage)]
    public async Task<IActionResult> Toggle(int id, bool enabled, CancellationToken ct)
        => Done(await plans.SetFeatureGlobalStateAsync(id, enabled, ct), enabled ? "Feature enabled platform-wide." : "Feature disabled platform-wide.", RedirectToAction(nameof(Index)));
}
