using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

public sealed class OrganizationCreateViewModel : OrganizationRegistrationInput
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Start on free trial")]
    public bool StartAsTrial { get; set; } = true;
}

[RequirePermission(PlatformPermissions.OrganizationsView)]
public sealed class OrganizationsController(
    IOrganizationService organizations, IProvisioningService provisioning, ISubscriptionService subscriptions, IPlanService plans)
    : PlatformController
{
    public async Task<IActionResult> Index([FromQuery] OrganizationListQuery query, CancellationToken ct)
    {
        ViewBag.Counts = await organizations.CountByStatusAsync(ct);
        ViewBag.Query = query;
        ViewBag.Plans = await PlanItemsAsync(ct, includeCustom: true);
        return View(await organizations.ListAsync(query, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await organizations.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();
        ViewBag.Plans = await PlanItemsAsync(ct, includeCustom: true);
        return View(details);
    }

    [HttpGet, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Plans = await PlanItemsAsync(ct, includeCustom: true);
        return View(new OrganizationCreateViewModel());
    }

    [HttpPost, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> Create(OrganizationCreateViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await provisioning.ProvisionAsync(model, model.StartAsTrial, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Organization {model.CompanyName} created.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Plans = await PlanItemsAsync(ct, includeCustom: true);
        return View(model);
    }

    [HttpGet, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await organizations.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> Edit(int id, OrganizationEditInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await organizations.UpdateAsync(id, model, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Organization details saved.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> ChangeStatus(int id, OrganizationStatus status, string reason, CancellationToken ct)
        => Done(await organizations.ChangeStatusAsync(id, status, reason, ct), "Organization status updated.", BackTo(id));

    [HttpPost, RequirePermission(PlatformPermissions.OrganizationsManage)]
    public async Task<IActionResult> Limits(int id, OrganizationLimitsInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Limits must be positive numbers."; return BackTo(id, "overview"); }
        return Done(await organizations.UpdateLimitsAsync(id, model, ct), "Limits updated.", BackTo(id, "overview"));
    }

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Feature(int id, int featureId, OverrideMode mode, string? reason, DateOnly? expiresOn, CancellationToken ct)
        => Done(await organizations.SetFeatureOverrideAsync(id, featureId, mode, reason, expiresOn, ct), "Feature access updated.", BackTo(id, "features"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Activate(int id, ActivateSubscriptionInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Please check the subscription details."; return BackTo(id, "subscription"); }
        return Done(await subscriptions.ActivateAsync(id, model, ct), "Subscription activated.", BackTo(id, "subscription"));
    }

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> ChangePlan(int id, int planId, decimal? price, CancellationToken ct)
        => Done(await subscriptions.ChangePlanAsync(id, planId, price, ct), "Plan changed.", BackTo(id, "subscription"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Extend(int id, int days, string? reason, CancellationToken ct)
        => Done(await subscriptions.ExtendAsync(id, days, reason ?? string.Empty, ct), $"Subscription extended by {days} day(s).", BackTo(id, "subscription"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Pause(int id, string reason, CancellationToken ct)
        => Done(await subscriptions.PauseAsync(id, reason ?? string.Empty, ct), "Subscription paused.", BackTo(id, "subscription"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
        => Done(await subscriptions.ResumeAsync(id, ct), "Subscription resumed.", BackTo(id, "subscription"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Cancel(int id, string reason, bool immediately, CancellationToken ct)
        => Done(await subscriptions.CancelAsync(id, reason ?? string.Empty, immediately, ct),
            immediately ? "Subscription cancelled." : "Subscription will end at the close of the current period.", BackTo(id, "subscription"));

    [HttpPost, RequirePermission(PlatformPermissions.SubscriptionsManage)]
    public async Task<IActionResult> Terms(int id, SubscriptionTermsInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Please check the subscription terms."; return BackTo(id, "subscription"); }
        return Done(await subscriptions.UpdateTermsAsync(id, model, ct), "Subscription terms updated.", BackTo(id, "subscription"));
    }

    [HttpPost, RequirePermission(PlatformPermissions.OrganizationsDelete)]
    public async Task<IActionResult> Delete(int id, string confirmation, string reason, CancellationToken ct)
    {
        var result = await organizations.DeleteAsync(id, confirmation, reason, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return BackTo(id); }
        TempData["Success"] = "Organization deleted. Its data is retained and its code stays reserved.";
        return RedirectToAction(nameof(Index));
    }

    private RedirectResult BackTo(int id, string? tab = null) =>
        Redirect($"/superadmin/organizations/details/{id}{(tab is null ? "" : "#" + tab)}");

    private async Task<List<SelectListItem>> PlanItemsAsync(CancellationToken ct, bool includeCustom) =>
        (await plans.ListAsync(ct)).Where(p => p.IsActive && (includeCustom || !p.IsCustom))
        .Select(p => new SelectListItem($"{p.Name} (₹{p.MonthlyPrice:N0}/m · ₹{p.YearlyPrice:N0}/y{(p.IsPerEmployeePricing ? " per employee" : "")})", p.Id.ToString()))
        .ToList();
}
