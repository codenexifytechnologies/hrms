using Codenexify.HRMS.Application.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Controllers;

/// <summary>Public policy pages: /privacy, /terms, /refund-policy, /contact (text maintained in the Control Center).</summary>
[AllowAnonymous]
public sealed class LegalController(ILegalPageService legal, IConfiguration config) : Controller
{
    public static LegalSiteInfo Site(IConfiguration config) =>
        new(config["Platform:ProductName"] ?? "Codenexify HRMS", config["Platform:CompanyWebsite"], config["Platform:SupportEmail"]);

    [HttpGet("/privacy")] public Task<IActionResult> Privacy(CancellationToken ct) => Show(LegalPageDefaults.Privacy, ct);
    [HttpGet("/terms")] public Task<IActionResult> Terms(CancellationToken ct) => Show(LegalPageDefaults.Terms, ct);
    [HttpGet("/refund-policy")] public Task<IActionResult> Refunds(CancellationToken ct) => Show(LegalPageDefaults.Refunds, ct);
    [HttpGet("/contact")] public Task<IActionResult> Contact(CancellationToken ct) => Show(LegalPageDefaults.Contact, ct);

    private async Task<IActionResult> Show(string slug, CancellationToken ct)
    {
        var page = await legal.GetPublishedAsync(slug, Site(config), ct);
        if (page is null) return NotFound();
        return View("Page", page);
    }
}
