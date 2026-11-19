using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Managers plan raises for their team within the cycle's budget and guidelines.</summary>
[RequireFeature(FeatureCodes.CompensationPlans)]
[Route("me/compensation")]
public sealed class MyCompensationController(IEmployeeService employees, ICompensationService compensation) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return View(await compensation.ForPlannerAsync(me, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Cycle(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var view = await compensation.ViewAsync(id, me, ct);
        if (view is null || view.Cycle.Status == Domain.Performance.CompensationCycleStatus.Draft) return NotFound();
        ViewData["LineAction"] = "/me/compensation/lines";
        return View("~/Areas/OrgAdmin/Views/Compensation/Cycle.cshtml", view);
    }

    [HttpPost("lines/{lineId:int}")]
    public async Task<IActionResult> Line(int lineId, int cycleId, decimal? percent, string? note, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await compensation.SetLineAsync(lineId, me, false, percent, note, ct), "Saved.");
        return Redirect($"/me/compensation/{cycleId}#line-{lineId}");
    }
}
