using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

/// <summary>Platform-wide statutory reference data used by every organization's payroll.</summary>
[RequirePermission(PlatformPermissions.StatutoryManage)]
public sealed class StatutoryController(IStatutoryRuleService rules) : PlatformController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Pt = await rules.PtSlabsAsync(ct);
        ViewBag.Lwf = await rules.LwfRulesAsync(ct);
        ViewBag.Tax = await rules.TaxRulesAsync(ct);
        return View(await rules.RateSetsAsync(ct));
    }

    [HttpGet("/superadmin/statutory/rates/{id:int?}")]
    public async Task<IActionResult> Rates(int? id, CancellationToken ct)
    {
        var all = await rules.RateSetsAsync(ct);
        var r = id is null ? all.FirstOrDefault() : all.FirstOrDefault(x => x.Id == id);
        if (id is not null && r is null) return NotFound();
        ViewBag.Id = id;
        return View(r is null ? new RateSetInput() : new RateSetInput
        {
            EffectiveFrom = id is null ? DateOnly.FromDateTime(DateTime.Today) : r.EffectiveFrom,
            PfEmployeeRate = r.PfEmployeeRate, PfEmployerRate = r.PfEmployerRate, EpsRate = r.EpsRate, PfWageCeiling = r.PfWageCeiling, EdliRate = r.EdliRate,
            PfAdminChargesRate = r.PfAdminChargesRate, EsiEmployeeRate = r.EsiEmployeeRate, EsiEmployerRate = r.EsiEmployerRate, EsiWageLimit = r.EsiWageLimit,
            GratuityMinServiceYears = r.GratuityMinServiceYears, GratuityDaysPerYear = r.GratuityDaysPerYear, GratuityWageDivisor = r.GratuityWageDivisor,
            GratuityCeiling = r.GratuityCeiling, IsVerified = id is not null && r.IsVerified, Notes = id is null ? null : r.Notes
        });
    }

    [HttpPost("/superadmin/statutory/rates/{id:int?}")]
    public async Task<IActionResult> Rates(int? id, RateSetInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await rules.SaveRateSetAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Rate set saved."; return Redirect("/superadmin/statutory"); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AddPt(ProfessionalTaxSlab slab, CancellationToken ct) =>
        Done(await rules.AddPtSlabAsync(slab, ct), "PT slab added.", Redirect("/superadmin/statutory#pt"));

    [HttpPost]
    public async Task<IActionResult> AddLwf(LabourWelfareFundRule rule, CancellationToken ct) =>
        Done(await rules.AddLwfRuleAsync(rule, ct), "LWF rule added.", Redirect("/superadmin/statutory#lwf"));

    [HttpPost]
    public async Task<IActionResult> Delete(string kind, int id, CancellationToken ct) =>
        Done(await rules.DeleteAsync(kind, id, ct), "Deleted.", Redirect($"/superadmin/statutory#{kind}"));

    [HttpGet("/superadmin/statutory/tax/{id:int?}")]
    public async Task<IActionResult> Tax(int? id, CancellationToken ct)
    {
        var model = id is null ? new TaxRuleInput { FinancialYear = DateTime.Today.Month >= 4 ? DateTime.Today.Year : DateTime.Today.Year - 1, CessRate = 4 } : await rules.GetTaxRuleAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost("/superadmin/statutory/tax/{id:int?}")]
    public async Task<IActionResult> Tax(int? id, TaxRuleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await rules.SaveTaxRuleAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Income-tax rules saved."; return Redirect("/superadmin/statutory#tax"); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }
}
