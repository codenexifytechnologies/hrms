using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Tenancy;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Base for Organization Admin pages: tenant principals only, scoped to their own organization.</summary>
[Area("OrgAdmin")]
[Authorize(Policy = Policies.Tenant)]
public abstract class OrgController : Controller
{
    protected void Flash(Result result, string success)
    {
        if (result.Succeeded) TempData["Success"] = success; else TempData["Error"] = result.Error;
    }
}

[RequirePermission(TenantPermissions.DashboardView)]
public sealed class DashboardController(ITenantAdminService admin) : OrgController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await admin.GetDashboardAsync(ct));
}

[RequirePermission(TenantPermissions.ProfileView)]
public sealed class ProfileController(ITenantAdminService admin) : OrgController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await admin.GetOrganizationAsync(ct);
        ViewBag.Code = org.Code;
        ViewBag.CanEdit = User.HasPermission(TenantPermissions.ProfileManage);
        return View(OrganizationService.ToEditInput(org));
    }

    [HttpPost, RequirePermission(TenantPermissions.ProfileManage)]
    public async Task<IActionResult> Index(OrganizationEditInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.UpdateProfileAsync(model, ct);
            Flash(result, "Company profile saved.");
            if (result.Succeeded) return RedirectToAction(nameof(Index));
        }
        var org = await admin.GetOrganizationAsync(ct);
        ViewBag.Code = org.Code;
        ViewBag.CanEdit = true;
        return View(model);
    }
}

[RequirePermission(TenantPermissions.UsersView)]
public sealed class UsersController(ITenantAdminService admin) : OrgController
{
    public async Task<IActionResult> Index(string? search, bool? active, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Search = search;
        ViewBag.Active = active;
        return View(await admin.ListUsersAsync(search, active, page, ct));
    }

    [HttpGet, RequirePermission(TenantPermissions.UsersManage)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Roles = await admin.ListRolesAsync(ct);
        return View("Edit", new TenantUserInput());
    }

    [HttpPost, RequirePermission(TenantPermissions.UsersManage)]
    public async Task<IActionResult> Create(TenantUserInput model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "A password is required for new users.");
        if (ModelState.IsValid)
        {
            var result = await admin.CreateUserAsync(model, ct);
            if (result.Succeeded) { TempData["Success"] = $"{model.FullName} added."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Roles = await admin.ListRolesAsync(ct);
        return View("Edit", model);
    }

    [HttpGet, RequirePermission(TenantPermissions.UsersManage)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await admin.GetUserForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.Roles = await admin.ListRolesAsync(ct);
        return View(model);
    }

    [HttpPost, RequirePermission(TenantPermissions.UsersManage)]
    public async Task<IActionResult> Edit(int id, TenantUserInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.UpdateUserAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "User saved."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        ViewBag.Roles = await admin.ListRolesAsync(ct);
        return View(model);
    }
}

[RequirePermission(TenantPermissions.RolesManage)]
public sealed class RolesController(ITenantAdminService admin, IFeatureAccessService features) : OrgController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.CanCreate = await features.IsEnabledAsync(User.GetOrganizationId()!.Value, FeatureCodes.CustomRoles, ct);
        return View(await admin.ListRolesAsync(ct));
    }

    private async Task CatalogAsync(CancellationToken ct)
    {
        ViewBag.Permissions = await admin.GetPermissionCatalogAsync(ct);
        ViewBag.Features = await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct);
    }

    /// <param name="from">Start from a copy of this role's permissions.</param>
    [HttpGet, RequireFeature(FeatureCodes.CustomRoles)]
    public async Task<IActionResult> Create(int? from, CancellationToken ct)
    {
        await CatalogAsync(ct);
        ViewBag.IsSystem = false;
        var model = new RoleInput();
        if (from is int f && await admin.GetRoleForEditAsync(f, ct) is { } source)
        {
            model.Name = $"{source.Name} (copy)";
            model.Description = source.Description;
            model.PermissionIds = source.PermissionIds;
        }
        return View("Edit", model);
    }

    [HttpPost, RequireFeature(FeatureCodes.CustomRoles)]
    public async Task<IActionResult> Create(RoleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await admin.CreateRoleAsync(model, ct);
            if (result.Succeeded) { TempData["Success"] = $"Role {model.Name} created."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await CatalogAsync(ct);
        ViewBag.IsSystem = false;
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var role = await admin.GetRoleAsync(id, ct);
        var model = await admin.GetRoleForEditAsync(id, ct);
        if (role is null || model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.IsSystem = role.IsSystem;
        ViewBag.Locked = role.IsSystem && role.Name == TenantPermissions.OrgAdminRole;
        await CatalogAsync(ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, RoleInput model, CancellationToken ct)
    {
        var role = await admin.GetRoleAsync(id, ct);
        if (role is null) return NotFound();
        if (role.IsSystem) { ModelState.Remove(nameof(RoleInput.Name)); model.Name = role.Name; }
        if (ModelState.IsValid)
        {
            var result = await admin.UpdateRoleAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Role saved. Members will get the new permissions at their next refresh."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        ViewBag.IsSystem = role.IsSystem;
        ViewBag.Locked = role.IsSystem && role.Name == TenantPermissions.OrgAdminRole;
        await CatalogAsync(ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await admin.DeleteRoleAsync(id, ct), "Role deleted.");
        return RedirectToAction(nameof(Index));
    }
}

[RequirePermission(TenantPermissions.SubscriptionView)]
public sealed class SubscriptionController(ITenantAdminService admin, IPlanService plans) : OrgController
{
    public async Task<IActionResult> Index(string? feature, CancellationToken ct)
    {
        ViewBag.BlockedFeature = feature;
        ViewBag.Plans = (await plans.ListAsync(ct)).Where(p => p.IsActive && p.IsPublic).ToList();
        return View(await admin.GetSubscriptionOverviewAsync(ct));
    }
}

[RequirePermission(TenantPermissions.AuditView)]
public sealed class AuditController(ITenantAdminService admin) : OrgController
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await admin.AuditTrailAsync(page, ct));
}
