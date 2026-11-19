using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

[RequireFeature(FeatureCodes.Assets)]
[Route("me/assets")]
public sealed class MyAssetsController(IEmployeeService employees, IAssetService assets) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        return View(await assets.ForEmployeeAsync(me, ct));
    }

    [HttpPost("{assignmentId:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int assignmentId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await assets.AcknowledgeAsync(assignmentId, me, ct), "Thanks — receipt confirmed.");
        return Redirect("/me/assets");
    }
}

[RequireFeature(FeatureCodes.PulseSurveys)]
[Route("me/surveys")]
public sealed class MySurveysController(IEmployeeService employees, ISurveyService surveys) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await surveys.PendingForMeAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Take(int id, CancellationToken ct)
    {
        var s = await surveys.ToTakeAsync(id, ct);
        if (s is null) { TempData["Error"] = "This survey is closed or already answered."; return Redirect("/me/surveys"); }
        return View(s);
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        // Answers arrive as q_{questionId}.
        var answers = Request.Form.Keys.Where(k => k.StartsWith("q_") && int.TryParse(k[2..], out _))
            .ToDictionary(k => int.Parse(k[2..]), k => (string?)Request.Form[k].ToString());
        var r = await surveys.SubmitAsync(id, answers, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/me/surveys/{id}"); }
        TempData["Success"] = "Thank you — your response was recorded.";
        return Redirect("/me/surveys");
    }
}

[RequireFeature(FeatureCodes.AnonymousFeedback)]
[Route("me/feedback")]
public sealed class MyFeedbackController(IEmployeeService employees, IFeedbackService feedback) : SelfServiceController(employees)
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.Categories = feedback.Categories;
        ViewBag.Code = TempData["FeedbackCode"] as string;
        return View(new FeedbackInput());
    }

    [HttpPost("")]
    public async Task<IActionResult> Send(FeedbackInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/feedback"); }
        var r = await feedback.SendAsync(input, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/me/feedback"); }
        TempData["FeedbackCode"] = r.Value;
        return Redirect("/me/feedback");
    }

    /// <summary>The code travels in the POST body only (never in a URL, so it doesn't end up in logs or history).</summary>
    [HttpPost("open")]
    public async Task<IActionResult> Open(string code, string? reply, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(reply))
        {
            var r = await feedback.FollowUpAsync(code, reply, ct);
            if (r.Succeeded) TempData["Success"] = "Message sent anonymously."; else TempData["Error"] = r.Error;
        }
        var view = await feedback.OpenByCodeAsync(code, ct);
        if (view is null) { TempData["Error"] = "That code doesn't match any feedback."; return Redirect("/me/feedback"); }
        ViewBag.Code = code.Trim();
        return View("Thread", view);
    }
}
