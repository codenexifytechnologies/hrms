using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Salary-revision cycles: budget, guidelines, every raise, approval and application.</summary>
[RequireFeature(FeatureCodes.CompensationPlans), RequirePermission(TenantPermissions.CompensationManage)]
[Route("org/compensation")]
public sealed class CompensationController(ICompensationService compensation, IReviewService reviews, IStructureService structure) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await compensation.CyclesAsync(ct));

    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        await LookupsAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        return View("Edit", new CompensationCycleInput
        {
            EffectiveOn = new DateOnly(today.Year, today.Month, 1).AddMonths(1),
            Guidelines = CompensationService.GuidelineRatings.Select(r => new GuidelineInput { Rating = r }).ToList()
        });
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(CompensationCycleInput model, CancellationToken ct) => await SaveAsync(null, model, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await compensation.GetForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CompensationCycleInput model, CancellationToken ct) => await SaveAsync(id, model, ct);

    private async Task<IActionResult> SaveAsync(int? id, CompensationCycleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await compensation.SaveAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Saved."; return Redirect($"/org/compensation/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View("Edit", model);
    }

    private async Task LookupsAsync(CancellationToken ct)
    {
        var (_, departments, _) = await structure.LookupsAsync(ct);
        ViewBag.Departments = departments;
        ViewBag.ReviewCycles = (await reviews.CyclesAsync(ct)).Where(c => c.Status != Domain.Performance.ReviewCycleStatus.Draft).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Cycle(int id, CancellationToken ct)
    {
        var view = await compensation.ViewAsync(id, null, ct);
        if (view is null) return NotFound();
        ViewData["LineAction"] = "/org/compensation/lines";
        return View(view);
    }

    [HttpPost("lines/{lineId:int}")]
    public async Task<IActionResult> Line(int lineId, int cycleId, decimal? percent, string? note, CancellationToken ct)
    {
        Flash(await compensation.SetLineAsync(lineId, null, true, percent, note, ct), "Saved.");
        return Redirect($"/org/compensation/{cycleId}#line-{lineId}");
    }

    [HttpPost("{id:int}/{step}")]
    public async Task<IActionResult> Step(int id, string step, CancellationToken ct)
    {
        switch (step)
        {
            case "launch":
                var l = await compensation.LaunchAsync(id, ct);
                if (l.Succeeded) TempData["Success"] = $"Launched for {l.Value} employee(s). Managers have been asked to plan their team."; else TempData["Error"] = l.Error;
                break;
            case "review": Flash(await compensation.MoveToReviewAsync(id, ct), "Planning closed. Review and adjust, then approve."); break;
            case "reopen": Flash(await compensation.ReopenAsync(id, ct), "Reopened for managers."); break;
            case "approve":
                var a = await compensation.ApproveAsync(id, ct);
                if (a.Succeeded) TempData["Success"] = a.Value > 0 ? $"Approved. {a.Value} salary revision(s) applied." : "Approved. Raises apply on the effective date."; else TempData["Error"] = a.Error;
                break;
            case "retry": TempData["Success"] = $"{await compensation.ApplyDueAsync(id, ct)} revision(s) applied."; break;
            case "close": Flash(await compensation.CloseAsync(id, ct), "Cycle closed."); break;
            case "delete":
                var d = await compensation.DeleteAsync(id, ct);
                if (d.Succeeded) { TempData["Success"] = "Draft deleted."; return Redirect("/org/compensation"); }
                TempData["Error"] = d.Error;
                break;
            default: return NotFound();
        }
        return Redirect($"/org/compensation/{id}");
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var bytes = await compensation.ExportAsync(id, ct);
        return bytes is null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"salary-revisions-{id}.xlsx");
    }
}

/// <summary>Calibration board for a review cycle: ratings side by side against target distribution; move people with a reason.</summary>
[RequireFeature(FeatureCodes.Calibration), RequirePermission(TenantPermissions.ReviewsManage)]
[Route("org/reviews/{cycleId:int}/calibration")]
public sealed class CalibrationController(ICalibrationService calibration, IReviewService reviews) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int cycleId, int? departmentId, int? reviewerId, CancellationToken ct)
    {
        var board = await calibration.BoardAsync(cycleId, departmentId, reviewerId, ct);
        if (board is null) return NotFound();
        ViewBag.DepartmentId = departmentId;
        ViewBag.ReviewerId = reviewerId;
        return View(board);
    }

    [HttpPost("targets")]
    public async Task<IActionResult> Targets(int cycleId, decimal[] percents, CancellationToken ct)
    {
        Flash(await calibration.SetTargetsAsync(cycleId, percents, ct), "Targets saved.");
        return Redirect($"/org/reviews/{cycleId}/calibration");
    }

    [HttpPost("move")]
    public async Task<IActionResult> Move(int cycleId, int reviewId, int rating, string? note, int? departmentId, int? reviewerId, CancellationToken ct)
    {
        Flash(await reviews.AdjustAsync(reviewId, rating, note, ct), "Rating updated.");
        var q = new List<string>();
        if (departmentId is int d) q.Add($"departmentId={d}");
        if (reviewerId is int r) q.Add($"reviewerId={r}");
        return Redirect($"/org/reviews/{cycleId}/calibration{(q.Count > 0 ? "?" + string.Join("&", q) : "")}");
    }
}
