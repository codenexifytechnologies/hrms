using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>HR list of all promotions; each case opens on the shared page (/me/promotions/{id}).</summary>
[RequireFeature(FeatureCodes.Promotions), RequirePermission(TenantPermissions.PromotionsManage)]
[Route("org/promotions")]
public sealed class PromotionsAdminController(IPromotionService promotions) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(PromotionStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await promotions.ListAsync(status, ct));
    }
}

/// <summary>HR list of all improvement plans; each plan opens on the shared page (/me/pips/{id}).</summary>
[RequireFeature(FeatureCodes.Pip), RequirePermission(TenantPermissions.PipManage)]
[Route("org/pips")]
public sealed class PipsAdminController(IPipService pips) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(PipStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await pips.ListAsync(status, ct));
    }
}
