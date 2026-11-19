using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Goals & OKRs for everyone: own goals, the team's goals (managers), company/department goals, and all goals (HR).</summary>
[RequireFeature(FeatureCodes.Okrs)]
[Route("me/goals")]
public sealed class GoalsController(IEmployeeService employees, IGoalService goals, IStructureService structure) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.GoalsManage);

    /// <summary>Indian financial year (April–March) containing the date.</summary>
    public static (DateOnly From, DateOnly To) FinancialYear(int startYear) => (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
    public static int CurrentFyStart(DateOnly today) => today.Month >= 4 ? today.Year : today.Year - 1;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, int? fy, CancellationToken ct)
    {
        var viewer = await goals.ViewerAsync(IsHr, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = fy ?? CurrentFyStart(today);
        var (from, to) = FinancialYear(start);
        tab = tab is "team" or "company" or "all" ? tab : viewer.EmployeeId is null ? "company" : "mine";
        if (tab == "team" && viewer.Reports.Count == 0 && !viewer.IsHr) tab = "mine";
        if (tab == "all" && !viewer.IsHr) tab = "mine";

        ViewBag.Tab = tab;
        ViewBag.Fy = start;
        ViewBag.Viewer = viewer;
        IReadOnlyList<GoalRow> rows = tab switch
        {
            "company" => await goals.SharedAsync(from, to, ct),
            "team" => await goals.TeamAsync(viewer with { IsHr = false }, from, to, ct),
            "all" => await goals.TeamAsync(viewer, from, to, ct),
            _ => viewer.EmployeeId is int me ? await goals.IndividualAsync(me, viewer, from, to, ct) : []
        };
        return View(rows);
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(GoalLevel? level, int? owner, int? parent, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var (_, fyEnd) = FinancialYear(CurrentFyStart(today));
        var model = new GoalInput { Level = level ?? GoalLevel.Individual, OwnerEmployeeId = owner, ParentGoalId = parent, StartsOn = today, DueOn = fyEnd };
        await LookupsAsync(ct);
        return View("Edit", model);
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(GoalInput model, CancellationToken ct) => await SaveAsync(null, model, ct);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Goal(int id, CancellationToken ct)
    {
        var view = await goals.GetAsync(id, await goals.ViewerAsync(IsHr, ct), ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await goals.GetForEditAsync(id, await goals.ViewerAsync(IsHr, ct), ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, GoalInput model, CancellationToken ct) => await SaveAsync(id, model, ct);

    private async Task<IActionResult> SaveAsync(int? id, GoalInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await goals.SaveAsync(id, model, await goals.ViewerAsync(IsHr, ct), ct);
            if (r.Succeeded) { TempData["Success"] = "Goal saved."; return Redirect($"/me/goals/{r.Value}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        await LookupsAsync(ct);
        return View("Edit", model);
    }

    private async Task LookupsAsync(CancellationToken ct)
    {
        var viewer = await goals.ViewerAsync(IsHr, ct);
        ViewBag.Viewer = viewer;
        ViewBag.Parents = await goals.ParentOptionsAsync(ct);
        var (_, departments, _) = await structure.LookupsAsync(ct);
        ViewBag.Departments = departments;
        // Whose goal: self, people in the reporting line, or (HR) any current employee.
        var people = await Employees.ManagersAsync(null, ct);
        ViewBag.Owners = people.Where(p => viewer.IsHr || p.Id == viewer.EmployeeId || viewer.Reports.Contains(p.Id)).ToList();
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await goals.DeleteAsync(id, await goals.ViewerAsync(IsHr, ct), ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/me/goals/{id}"); }
        TempData["Success"] = "Goal deleted.";
        return Redirect("/me/goals");
    }

    [HttpPost("{id:int}/key-results")]
    public async Task<IActionResult> AddKeyResult(int id, KeyResultInput keyResult, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Give the key result a title and numeric start and target values."; return Redirect($"/me/goals/{id}"); }
        Flash(await goals.AddKeyResultAsync(id, keyResult, await goals.ViewerAsync(IsHr, ct), ct), "Key result added.");
        return Redirect($"/me/goals/{id}");
    }

    [HttpPost("key-results/{krId:int}/remove")]
    public async Task<IActionResult> RemoveKeyResult(int krId, int goalId, CancellationToken ct)
    {
        Flash(await goals.RemoveKeyResultAsync(krId, await goals.ViewerAsync(IsHr, ct), ct), "Key result removed.");
        return Redirect($"/me/goals/{goalId}");
    }

    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id, CheckInInput checkIn, CancellationToken ct)
    {
        Flash(await goals.CheckInAsync(id, checkIn, await goals.ViewerAsync(IsHr, ct), ct), "Progress updated.");
        return Redirect($"/me/goals/{id}");
    }
}

/// <summary>Performance reviews: the employee's own reviews and the reviews they write as a manager.</summary>
[RequireFeature(FeatureCodes.PerformanceReviews)]
[Route("me/reviews")]
public sealed class MyReviewsController(IEmployeeService employees, IReviewService reviews) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.ReviewsManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        ViewBag.ToWrite = me is int m ? await reviews.ToWriteAsync(m, false, ct) : [];
        return View(me is int id ? await reviews.MineAsync(id, ct) : []);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        // HR reviewing someone without a manager uses the Control panel page; here only employee and manager views.
        var view = await reviews.GetAsync(id, await MyEmployeeIdAsync(ct), false, ct);
        if (view is null) return NotFound();
        ViewData["ReviewBase"] = $"/me/reviews/{id}";
        return View("~/Views/Shared/Review.cshtml", view);
    }

    [HttpPost("{id:int}/self")]
    public async Task<IActionResult> Self(int id, List<AnswerInput> answers, bool submit, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await reviews.SaveSelfAsync(id, me, answers, submit, ct), submit ? "Self-review submitted to your manager." : "Draft saved.");
        return Redirect($"/me/reviews/{id}");
    }

    [HttpPost("{id:int}/manager")]
    public async Task<IActionResult> Manager(int id, List<AnswerInput> answers, int? overallRating, bool submit, CancellationToken ct)
    {
        Flash(await reviews.SaveManagerAsync(id, await MyEmployeeIdAsync(ct), false, answers, overallRating, submit, ct), submit ? "Review submitted." : "Draft saved.");
        return Redirect($"/me/reviews/{id}");
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, string? comment, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await reviews.AcknowledgeAsync(id, me, comment, ct), "Thanks, your acknowledgement is recorded.");
        return Redirect($"/me/reviews/{id}");
    }
}
