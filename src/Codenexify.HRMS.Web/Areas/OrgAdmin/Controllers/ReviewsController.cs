using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Review cycles for HR: set up the form, launch, follow progress, adjust ratings, release and close.</summary>
[RequireFeature(FeatureCodes.PerformanceReviews), RequirePermission(TenantPermissions.ReviewsManage)]
[Route("org/reviews")]
public sealed class ReviewsController(IReviewService reviews, IStructureService structure) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.ToWrite = await reviews.ToWriteAsync(null, true, ct);
        return View(await reviews.CyclesAsync(ct));
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await LookupsAsync(ct);
        return View("Edit", new ReviewCycleInput
        {
            PeriodStart = new DateOnly(today.Year, today.Month <= 6 ? 1 : 7, 1),
            PeriodEnd = new DateOnly(today.Year, today.Month <= 6 ? 6 : 12, today.Month <= 6 ? 30 : 31),
            SelfReviewDue = today.AddDays(14), ManagerReviewDue = today.AddDays(28)
        });
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(ReviewCycleInput model, CancellationToken ct) => await SaveAsync(null, model, ct);

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await reviews.GetCycleForEditAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, ReviewCycleInput model, CancellationToken ct) => await SaveAsync(id, model, ct);

    private async Task<IActionResult> SaveAsync(int? id, ReviewCycleInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await reviews.SaveCycleAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = id is null ? "Cycle created with a starter form. Review the questions, then launch." : "Saved."; return Redirect($"/org/reviews/{r.Value}"); }
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
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Cycle(int id, [FromServices] Application.Subscriptions.IFeatureAccessService features,
        [FromServices] Application.Abstractions.ICurrentContext context, CancellationToken ct)
    {
        var enabled = context.OrganizationId is int org ? await features.GetEnabledFeaturesAsync(org, ct) : new HashSet<string>();
        ViewBag.ValuesOn = enabled.Contains(FeatureCodes.CoreValues);
        ViewBag.CalibrationOn = enabled.Contains(FeatureCodes.Calibration);
        var view = await reviews.CycleAsync(id, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/questions")]
    public async Task<IActionResult> AddQuestion(int id, ReviewQuestionInput question, CancellationToken ct)
    {
        Flash(await reviews.AddQuestionAsync(id, question, ct), "Question added.");
        return Redirect($"/org/reviews/{id}");
    }

    [HttpPost("{id:int}/value-questions")]
    public async Task<IActionResult> ValueQuestions(int id, CancellationToken ct)
    {
        var r = await reviews.AddValueQuestionsAsync(id, ct);
        if (r.Succeeded) TempData["Success"] = r.Value == 0 ? "Every value already has a question." : $"{r.Value} value question(s) added.";
        else TempData["Error"] = r.Error;
        return Redirect($"/org/reviews/{id}");
    }

    [HttpPost("questions/{questionId:int}/remove")]
    public async Task<IActionResult> RemoveQuestion(int questionId, int cycleId, CancellationToken ct)
    {
        Flash(await reviews.RemoveQuestionAsync(questionId, ct), "Question removed.");
        return Redirect($"/org/reviews/{cycleId}");
    }

    [HttpPost("{id:int}/launch")]
    public async Task<IActionResult> Launch(int id, CancellationToken ct)
    {
        var r = await reviews.LaunchAsync(id, ct);
        if (r.Succeeded) TempData["Success"] = $"Launched for {r.Value} employee(s). Employees and managers have been notified.";
        else TempData["Error"] = r.Error;
        return Redirect($"/org/reviews/{id}");
    }

    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> Release(int id, CancellationToken ct)
    {
        var r = await reviews.ReleaseAsync(id, ct);
        if (r.Succeeded) TempData["Success"] = $"{r.Value} result(s) shared with employees. Reviews submitted later are shared as soon as they're done.";
        else TempData["Error"] = r.Error;
        return Redirect($"/org/reviews/{id}");
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        Flash(await reviews.CloseAsync(id, ct), "Cycle closed.");
        return Redirect($"/org/reviews/{id}");
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await reviews.DeleteCycleAsync(id, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/org/reviews/{id}"); }
        TempData["Success"] = "Draft cycle deleted.";
        return Redirect("/org/reviews");
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken ct)
    {
        var bytes = await reviews.ExportAsync(id, ct);
        return bytes is null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"review-cycle-{id}.xlsx");
    }

    // ------------------------------------------------------------------ one review (HR view; HR writes reviews for employees without a manager)

    [HttpGet("r/{reviewId:int}")]
    public async Task<IActionResult> Review(int reviewId, CancellationToken ct)
    {
        var view = await reviews.GetAsync(reviewId, null, true, ct);
        if (view is null) return NotFound();
        ViewData["ReviewBase"] = $"/org/reviews/r/{reviewId}";
        return View("~/Views/Shared/Review.cshtml", view);
    }

    [HttpPost("r/{reviewId:int}/manager")]
    public async Task<IActionResult> Manager(int reviewId, List<AnswerInput> answers, int? overallRating, bool submit, CancellationToken ct)
    {
        Flash(await reviews.SaveManagerAsync(reviewId, null, true, answers, overallRating, submit, ct), submit ? "Review submitted." : "Draft saved.");
        return Redirect($"/org/reviews/r/{reviewId}");
    }

    [HttpPost("r/{reviewId:int}/adjust")]
    public async Task<IActionResult> Adjust(int reviewId, int finalRating, string? note, CancellationToken ct)
    {
        Flash(await reviews.AdjustAsync(reviewId, finalRating, note, ct), "Final rating saved.");
        return Redirect($"/org/reviews/r/{reviewId}");
    }
}
