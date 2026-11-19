using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Promotions: managers propose for their team; HR (with the permission) proposes for anyone and decides.</summary>
[RequireFeature(FeatureCodes.Promotions)]
[Route("me/promotions")]
public sealed class MyPromotionsController(IEmployeeService employees, IPromotionService promotions, IStructureService structure, ISkillService skills) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.PromotionsManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (IsHr) return Redirect("/org/promotions");
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        ViewBag.HasTeam = (await skills.TeamAsync(me, ct)).Count > 0;
        return View(await promotions.ForManagerAsync(me, ct));
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(int? employeeId, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        var people = IsHr ? await Employees.ManagersAsync(me, ct) : me is int m ? await skills.TeamAsync(m, ct) : [];
        if (people.Count == 0) { TempData["Error"] = "You can propose promotions for people in your team."; return Redirect("/me"); }
        var (_, _, designations) = await structure.LookupsAsync(ct);
        ViewBag.People = people;
        ViewBag.Designations = designations;
        ViewBag.Reviews = employeeId is int e && people.Any(p => p.Id == e) ? await promotions.ReviewsForAsync(e, ct) : [];
        return View(new PromotionInput { EmployeeId = employeeId ?? 0, EffectiveOn = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1) });
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(PromotionInput proposal, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect($"/me/promotions/new?employeeId={proposal.EmployeeId}"); }
        var r = await promotions.ProposeAsync(await MyEmployeeIdAsync(ct), IsHr, proposal, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/me/promotions/new?employeeId={proposal.EmployeeId}"); }
        TempData["Success"] = IsHr ? "Promotion created. Approve it to apply." : "Promotion proposed. HR will review it.";
        return Redirect($"/me/promotions/{r.Value}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Case(int id, CancellationToken ct)
    {
        var view = await promotions.GetAsync(id, await MyEmployeeIdAsync(ct), IsHr, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, bool approve, string? note, CancellationToken ct)
    {
        if (!IsHr) return Forbid();
        Flash(await promotions.DecideAsync(id, approve, note, ct), approve ? "Approved." : "Rejected.");
        return Redirect($"/me/promotions/{id}");
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        Flash(await promotions.WithdrawAsync(id, await MyEmployeeIdAsync(ct), IsHr, ct), "Proposal withdrawn.");
        return Redirect($"/me/promotions/{id}");
    }

    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, CancellationToken ct)
    {
        if (!IsHr) return Forbid();
        Flash(await promotions.ApplyAsync(id, ct), "Applied: the new title and salary are in place.");
        return Redirect($"/me/promotions/{id}");
    }
}

/// <summary>Performance improvement plans: the employee's own, the manager's team's, and HR's actions.</summary>
[RequireFeature(FeatureCodes.Pip)]
[Route("me/pips")]
public sealed class PipsController(IEmployeeService employees, IPipService pips, ISkillService skills) : SelfServiceController(employees)
{
    private bool IsHr => User.HasPermission(TenantPermissions.PipManage);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        if (me is null) return IsHr ? Redirect("/org/pips") : NotLinked();
        ViewBag.Team = await pips.ForManagerAsync(me.Value, ct);
        ViewBag.CanCreate = IsHr || (await skills.TeamAsync(me.Value, ct)).Count > 0;
        ViewBag.IsHr = IsHr;
        return View(await pips.MineAsync(me.Value, ct));
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(int? employeeId, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        var people = IsHr ? await Employees.ManagersAsync(me, ct) : me is int m ? await skills.TeamAsync(m, ct) : [];
        if (people.Count == 0) { TempData["Error"] = "Improvement plans are created by managers for their team, or by HR."; return Redirect("/me"); }
        ViewBag.People = people;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var model = new PipInput { EmployeeId = employeeId ?? 0, StartsOn = today, EndsOn = today.AddDays(60) };
        model.Objectives.AddRange([new(), new(), new()]);
        return View("Edit", model);
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(PipInput plan, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await EditAgainAsync(null, plan, ct);
        var r = await pips.CreateAsync(await MyEmployeeIdAsync(ct), IsHr, plan, ct);
        if (!r.Succeeded) { ModelState.AddModelError(string.Empty, r.Error!); return await EditAgainAsync(null, plan, ct); }
        TempData["Success"] = IsHr ? "Draft saved. Start it when you're ready to share it with the employee." : "Draft saved and sent to HR, who will start it.";
        return Redirect($"/me/pips/{r.Value}");
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var view = await pips.GetAsync(id, await MyEmployeeIdAsync(ct), IsHr, ct);
        if (view is null || !view.CanEditDraft) return NotFound();
        var model = (await pips.GetForEditAsync(id, ct))!;
        while (model.Objectives.Count < 3) model.Objectives.Add(new());
        ViewBag.Id = id;
        ViewBag.EmployeeName = view.EmployeeName;
        return View(model);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, PipInput plan, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await pips.SaveDraftAsync(id, await MyEmployeeIdAsync(ct), IsHr, plan, ct);
            if (r.Succeeded) { TempData["Success"] = "Draft saved."; return Redirect($"/me/pips/{id}"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        return await EditAgainAsync(id, plan, ct);
    }

    private async Task<IActionResult> EditAgainAsync(int? id, PipInput plan, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        ViewBag.Id = id;
        ViewBag.People = IsHr ? await Employees.ManagersAsync(me, ct) : me is int m ? await skills.TeamAsync(m, ct) : [];
        while (plan.Objectives.Count < 3) plan.Objectives.Add(new());
        return View("Edit", plan);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Plan(int id, CancellationToken ct)
    {
        var view = await pips.GetAsync(id, await MyEmployeeIdAsync(ct), IsHr, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        if (!IsHr) return Forbid();
        Flash(await pips.StartAsync(id, ct), "Plan started and shared with the employee.");
        return Redirect($"/me/pips/{id}");
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, string? comment, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await pips.AcknowledgeAsync(id, me, comment, ct), "Thank you. Your acknowledgement is recorded.");
        return Redirect($"/me/pips/{id}");
    }

    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id, PipProgress progress, string note, CancellationToken ct)
    {
        Flash(await pips.CheckInAsync(id, await MyEmployeeIdAsync(ct), IsHr, progress, note, ct), "Check-in added.");
        return Redirect($"/me/pips/{id}");
    }

    [HttpPost("objectives/{objectiveId:int}")]
    public async Task<IActionResult> Objective(int objectiveId, int planId, PipObjectiveStatus status, CancellationToken ct)
    {
        Flash(await pips.SetObjectiveAsync(objectiveId, await MyEmployeeIdAsync(ct), IsHr, status, ct), "Objective updated.");
        return Redirect($"/me/pips/{planId}");
    }

    [HttpPost("{id:int}/extend")]
    public async Task<IActionResult> Extend(int id, DateOnly newEnd, string reason, CancellationToken ct)
    {
        if (!IsHr) return Forbid();
        Flash(await pips.ExtendAsync(id, newEnd, reason, ct), "Plan extended.");
        return Redirect($"/me/pips/{id}");
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, PipStatus outcome, string note, CancellationToken ct)
    {
        Flash(await pips.CloseAsync(id, await MyEmployeeIdAsync(ct), IsHr, outcome, note, ct), outcome == PipStatus.Cancelled ? "Plan cancelled." : "Outcome recorded.");
        return Redirect($"/me/pips/{id}");
    }
}
