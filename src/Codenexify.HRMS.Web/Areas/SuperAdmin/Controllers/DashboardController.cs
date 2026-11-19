using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

public sealed class DashboardController(IPlatformDashboardService dashboard) : PlatformController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!User.HasPermission(PlatformPermissions.DashboardView))
            return User.HasPermission(PlatformPermissions.OrganizationsView) ? Redirect("/superadmin/organizations") : Redirect("/superadmin/account/security");
        return View(await dashboard.GetAsync(ct));
    }
}
