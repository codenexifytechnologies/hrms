using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Domain.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Continuous feedback: give and request feedback any time; managers see their team's.</summary>
[RequireFeature(FeatureCodes.ContinuousFeedback)]
[Route("me/feedback-hub")]
public sealed class FeedbackHubController(IEmployeeService employees, IFeedbackService feedback) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var team = await feedback.TeamAsync(me, ct);
        tab = tab is "given" or "requests" or "team" ? tab : "received";
        if (tab == "team" && team.Count == 0) tab = "received";
        ViewBag.Tab = tab;
        ViewBag.Me = me;
        ViewBag.ToAnswer = await feedback.ToAnswerAsync(me, ct);
        ViewBag.MyRequests = tab == "requests" ? await feedback.MyRequestsAsync(me, ct) : [];
        ViewBag.Team = team;
        ViewBag.Colleagues = (await Employees.ManagersAsync(me, ct)).ToList();
        IReadOnlyList<FeedbackRow> rows = tab switch
        {
            "given" => await feedback.GivenAsync(me, ct),
            "team" => await feedback.AboutMyTeamAsync(me, ct),
            "requests" => [],
            _ => await feedback.ReceivedAsync(me, ct)
        };
        return View(rows);
    }

    [HttpPost("give")]
    public async Task<IActionResult> Give(FeedbackInput note, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/feedback-hub?tab=given"); }
        Flash(await feedback.GiveAsync(me, note, ct), note.Visibility == FeedbackVisibility.ManagersOnly ? "Sent to their managers." : "Feedback sent.");
        return Redirect(note.RequestId is null ? "/me/feedback-hub?tab=given" : "/me/feedback-hub?tab=requests");
    }

    [HttpPost("request")]
    public async Task<IActionResult> Request(FeedbackRequestInput ask, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (ask.AboutEmployeeId == 0) ask.AboutEmployeeId = me;
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/feedback-hub?tab=requests"); }
        var r = await feedback.RequestAsync(me, ask, ct);
        if (r.Succeeded) TempData["Success"] = "Request sent. You'll be notified as answers come in."; else TempData["Error"] = r.Error;
        return Redirect("/me/feedback-hub?tab=requests");
    }

    [HttpPost("requests/{rowId:int}/decline")]
    public async Task<IActionResult> Decline(int rowId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await feedback.DeclineAsync(rowId, me, ct), "Request declined.");
        return Redirect("/me/feedback-hub?tab=requests");
    }
}

/// <summary>One-on-ones between a manager and the people in their reporting line.</summary>
[RequireFeature(FeatureCodes.OneOnOne)]
[Route("me/one-on-ones")]
public sealed class OneOnOnesController(IEmployeeService employees, IOneOnOneService meetings) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        ViewBag.Me = me;
        ViewBag.Partners = await meetings.PartnersAsync(me, ct);
        return View(await meetings.MineAsync(me, ct));
    }

    [HttpPost("")]
    public async Task<IActionResult> Schedule(OneOnOneInput meeting, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/one-on-ones"); }
        var r = await meetings.ScheduleAsync(me, meeting, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/me/one-on-ones"); }
        TempData["Success"] = "1:1 scheduled.";
        return Redirect($"/me/one-on-ones/{r.Value}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Meeting(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var view = await meetings.GetAsync(id, me, ct);
        if (view is null) return NotFound();
        ViewBag.Me = me;
        return View(view);
    }

    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> Notes(int id, string? shared, string? mine, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await meetings.SaveNotesAsync(id, me, shared, mine, ct), "Notes saved.");
        return Redirect($"/me/one-on-ones/{id}");
    }

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddItem(int id, MeetingItemKind kind, string text, int? ownerId, DateOnly? dueOn, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await meetings.AddItemAsync(id, me, kind, text, ownerId, dueOn, ct), kind == MeetingItemKind.Agenda ? "Topic added." : "Action item added.");
        return Redirect($"/me/one-on-ones/{id}");
    }

    [HttpPost("items/{itemId:int}/toggle")]
    public async Task<IActionResult> Toggle(int itemId, int back, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var r = await meetings.ToggleItemAsync(itemId, me, ct);
        if (!r.Succeeded) TempData["Error"] = r.Error;
        return Redirect($"/me/one-on-ones/{back}");
    }

    [HttpPost("items/{itemId:int}/remove")]
    public async Task<IActionResult> Remove(int itemId, int back, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var r = await meetings.RemoveItemAsync(itemId, me, ct);
        if (!r.Succeeded) TempData["Error"] = r.Error;
        return Redirect($"/me/one-on-ones/{back}");
    }

    [HttpPost("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int id, DateOnly onDate, TimeOnly startsAt, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await meetings.RescheduleAsync(id, me, onDate, startsAt, ct), "1:1 moved. The other person has been told.");
        return Redirect($"/me/one-on-ones/{id}");
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await meetings.CancelAsync(id, me, ct), "1:1 cancelled.");
        return Redirect($"/me/one-on-ones/{id}");
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var r = await meetings.CompleteAsync(id, me, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/me/one-on-ones/{id}"); }
        TempData["Success"] = r.Value is null ? "Marked as held." : "Marked as held. The next 1:1 is scheduled and open action items carry over.";
        return Redirect(r.Value is int next ? $"/me/one-on-ones/{next}" : $"/me/one-on-ones/{id}");
    }
}
