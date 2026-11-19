using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

[RequirePermission(PlatformPermissions.UsersManage)]
public sealed class UsersController(IPlatformUserService users) : PlatformController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await users.ListAsync(ct));

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Id = null;
        return View("Edit", new PlatformUserInput());
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlatformUserInput model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "A password is required for new users.");
        if (ModelState.IsValid)
        {
            var result = await users.CreateAsync(model, ct);
            if (result.Succeeded) { TempData["Success"] = $"{model.FullName} added. They must change the password at first sign-in."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = null;
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await users.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PlatformUserInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await users.UpdateAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "User saved. Active sessions will pick up the changes within minutes."; return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct)
        => Done(await users.UnlockAsync(id, ct), "Account unlocked.", RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> ResetTwoFactor(int id, CancellationToken ct)
        => Done(await users.ResetTwoFactorAsync(id, ct), "Two-factor authentication reset. The user can enrol again.", RedirectToAction(nameof(Index)));
}

[RequirePermission(PlatformPermissions.AuditView)]
public sealed class AuditController(IAuditQueryService audit, IPlatformUserService users, IOrganizationService organizations) : PlatformController
{
    public async Task<IActionResult> Index([FromQuery] AuditQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        if (query.OrganizationId is { } orgId && User.HasPermission(PlatformPermissions.OrganizationsView))
            ViewBag.OrganizationName = (await organizations.GetForEditAsync(orgId, ct))?.Name;
        return View(await audit.SearchAsync(query, ct));
    }

    public async Task<IActionResult> Logins(PrincipalType? type, bool? succeeded, string? search, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Type = type;
        ViewBag.Succeeded = succeeded;
        ViewBag.Search = search;
        return View(await users.LoginHistoryAsync(type, succeeded, search, page, ct));
    }
}
