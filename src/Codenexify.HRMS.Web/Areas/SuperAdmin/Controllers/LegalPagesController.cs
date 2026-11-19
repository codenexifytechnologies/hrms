using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

/// <summary>Edit the public privacy policy, terms, refund policy and contact page, and the values they print.</summary>
[RequirePermission(PlatformPermissions.SettingsManage)]
[Route("superadmin/legal")]
public sealed class LegalPagesController(ILegalPageService legal, IConfiguration config) : PlatformController
{
    private LegalSiteInfo Site => Web.Controllers.LegalController.Site(config);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Missing = await legal.MissingValuesAsync(Site, ct);
        ViewBag.Settings = await legal.GetSettingsAsync(ct);
        return View(await legal.ListAsync(ct));
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(LegalSettingsInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/superadmin/legal");
        }
        return Done(await legal.SaveSettingsAsync(model, ct), "Saved. The pages now show these details.", Redirect("/superadmin/legal"));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var page = await legal.GetForEditAsync(id, ct);
        if (page is null) return NotFound();
        await LoadEditorAsync(page.Value.Slug, page.Value.Version, page.Value.Input, ct);
        return View(page.Value.Input);
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Edit(int id, LegalPageInput model, string? preview, CancellationToken ct)
    {
        var page = await legal.GetForEditAsync(id, ct);
        if (page is null) return NotFound();
        if (preview is null && ModelState.IsValid)
        {
            var result = await legal.SaveAsync(id, model, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = model.NewVersion ? "Saved as a new version. New sign-ups accept this version." : "Saved.";
                return Redirect($"/superadmin/legal/{id}");
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await LoadEditorAsync(page.Value.Slug, page.Value.Version, model, ct);
        if (preview is not null) ViewBag.Preview = await legal.PreviewAsync(model.Body, model.EffectiveOn, Site, ct);
        return View(model);
    }

    private async Task LoadEditorAsync(string slug, int version, LegalPageInput input, CancellationToken ct)
    {
        ViewBag.Slug = slug;
        ViewBag.Version = version;
        ViewBag.Placeholders = LegalMarkup.Known;
        ViewBag.Missing = await legal.MissingValuesAsync(Site, ct);
        ViewBag.Unknown = LegalMarkup.UnknownPlaceholders(input.Body);
    }
}
