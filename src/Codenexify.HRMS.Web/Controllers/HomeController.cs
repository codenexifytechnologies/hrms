using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Codenexify.HRMS.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController(IPlanService plans, IFeatureAccessService featureAccess, IMemoryCache cache,
    Application.Support.IAnnouncementService system, ILogger<HomeController> logger) : Controller
{
    /// <summary>Upper bound only: any plan or module change clears it at once (the key follows the feature cache generation).</summary>
    private static readonly TimeSpan PricingCacheFor = TimeSpan.FromMinutes(10);
    /// <summary>Shown (with 503) for every organization-portal request while maintenance mode is on.</summary>
    [Route("maintenance")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Maintenance(CancellationToken ct)
    {
        var state = await system.MaintenanceAsync(ct);
        if (!state.Enabled) return Redirect("/");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View(state);
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.IsTenant()) return Redirect(User.HasPermission(Application.Catalog.TenantPermissions.DashboardView) ? "/org" : "/me");
        // The public pricing data is the same for every visitor: build it once, not on every page view.
        var pricing = await cache.GetOrCreateAsync($"home:pricing:{featureAccess.CacheGeneration}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = PricingCacheFor;
            var list = (await plans.ListAsync(ct)).Where(p => p.IsActive && p.IsPublic).ToList();
            return (Plans: list, Modules: await plans.ModuleCatalogAsync(list.Select(p => p.Id), ct));
        });
        ViewBag.Modules = pricing.Modules;
        return View(pricing.Plans);
    }

    [Route("error/{code:int?}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error is { } ex) logger.LogError(ex, "Unhandled exception on {Path}", feature.Path);
        ViewData["Code"] = code ?? 500;
        Response.StatusCode = code ?? 500;
        return View();
    }
}
