using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Engagement;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>The organization's social wall: posts, praise, polls and HR announcements.</summary>
[RequireFeature(FeatureCodes.SocialWall)]
[Route("me/wall")]
public sealed class WallController(IEmployeeService employees, ISocialWallService wall) : SelfServiceController(employees)
{
    private bool CanModerate => User.HasPermission(TenantPermissions.WallModerate);

    private IActionResult Back(int? postId = null, string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return Redirect(postId is int id ? $"/me/wall#post-{id}" : "/me/wall");
    }

    private IActionResult Invalid()
    {
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return Back();
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(PostKind? kind, int page, CancellationToken ct)
    {
        ViewBag.Kind = kind;
        ViewBag.CanModerate = CanModerate;
        ViewBag.Badges = wall.Badges;
        ViewBag.Colleagues = await wall.ColleaguesAsync(ct);
        return View(await wall.FeedAsync(page, kind, CanModerate, ct));
    }

    [HttpGet("post/{id:int}")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var post = await wall.GetAsync(id, CanModerate, allComments: true, ct);
        return post is null ? NotFound() : View(post);
    }

    [HttpPost("post")]
    public async Task<IActionResult> Create(PostInput post, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid();
        var r = await wall.PostAsync(post, ct);
        Flash(r, "Posted.");
        return Back(r.Succeeded ? r.Value : null);
    }

    [HttpPost("praise")]
    public async Task<IActionResult> Praise(PraiseInput praise, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid();
        var r = await wall.PraiseAsync(praise, ct);
        Flash(r, "Praise shared — they'll be notified.");
        return Back(r.Succeeded ? r.Value : null);
    }

    [HttpPost("poll")]
    public async Task<IActionResult> Poll(PollInput poll, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid();
        var r = await wall.PollAsync(poll, ct);
        Flash(r, "Poll published.");
        return Back(r.Succeeded ? r.Value : null);
    }

    [HttpPost("announce"), RequirePermission(TenantPermissions.WallModerate)]
    public async Task<IActionResult> Announce(AnnouncementPostInput announcement, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid();
        var r = await wall.AnnounceAsync(announcement, CanModerate, ct);
        Flash(r, announcement.Notify ? "Announcement published and everyone notified." : "Announcement published.");
        return Back(r.Succeeded ? r.Value : null);
    }

    [HttpPost("{id:int}/like")]
    public async Task<IActionResult> Like(int id, string? returnUrl, CancellationToken ct)
    {
        var r = await wall.ToggleLikeAsync(id, ct);
        if (!r.Succeeded) TempData["Error"] = r.Error;
        return Back(id, returnUrl);
    }

    [HttpPost("{id:int}/comment")]
    public async Task<IActionResult> Comment(int id, string body, string? returnUrl, CancellationToken ct)
    {
        var r = await wall.CommentAsync(id, body, ct);
        if (!r.Succeeded) TempData["Error"] = r.Error;
        return Back(id, returnUrl);
    }

    [HttpPost("comment/{commentId:int}/delete")]
    public async Task<IActionResult> DeleteComment(int commentId, int postId, string? returnUrl, CancellationToken ct)
    {
        Flash(await wall.DeleteCommentAsync(commentId, CanModerate, ct), "Comment removed.");
        return Back(postId, returnUrl);
    }

    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> Vote(int id, int optionId, string? returnUrl, CancellationToken ct)
    {
        Flash(await wall.VoteAsync(id, optionId, ct), "Thanks for voting.");
        return Back(id, returnUrl);
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await wall.DeleteAsync(id, CanModerate, ct), "Post removed.");
        return Back();
    }

    [HttpPost("{id:int}/pin"), RequirePermission(TenantPermissions.WallModerate)]
    public async Task<IActionResult> Pin(int id, bool pinned, CancellationToken ct)
    {
        Flash(await wall.SetPinnedAsync(id, pinned, CanModerate, ct), pinned ? "Pinned to the top." : "Unpinned.");
        return Back(id);
    }
}

/// <summary>Colleague directory (work details only) and celebrations.</summary>
[RequireFeature(FeatureCodes.SelfService)]
[Route("me/people")]
public sealed class PeopleController(IEmployeeService employees, IPeopleService people, IStructureService structure) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int? departmentId, int page, CancellationToken ct)
    {
        ViewBag.Search = search;
        ViewBag.DepartmentId = departmentId;
        ViewBag.Departments = (await structure.LookupsAsync(ct)).Departments;
        ViewBag.Celebrations = await people.CelebrationsAsync(30, ct);
        return View(await people.DirectoryAsync(search, departmentId, page, ct));
    }
}

/// <summary>Request corrections to one's own personal details and set personal preferences.</summary>
[Route("me/profile")]
public sealed class ProfileController(IEmployeeService employees, IProfileChangeService changes, IPeopleService people) : SelfServiceController(employees)
{
    [HttpGet(""), RequireFeature(FeatureCodes.SelfService)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        ViewBag.History = await changes.ForEmployeeAsync(me, ct);
        return View(await changes.CurrentAsync(me, ct));
    }

    [HttpPost(""), RequireFeature(FeatureCodes.SelfService)]
    public async Task<IActionResult> Index(ProfileChangeInput model, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (ModelState.IsValid)
        {
            var r = await changes.SubmitAsync(me, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Sent to HR. Your details change once they approve."; return Redirect("/me/profile"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.History = await changes.ForEmployeeAsync(me, ct);
        return View(model);
    }

    [HttpPost("cancel/{id:int}"), RequireFeature(FeatureCodes.SelfService)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await changes.CancelAsync(id, me, ct), "Request cancelled.");
        return Redirect("/me/profile");
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> Preferences(bool hideBirthday, bool muteEmail, CancellationToken ct)
    {
        Flash(await people.SavePreferencesAsync(hideBirthday, muteEmail, ct), "Preferences saved.");
        return Redirect("/me#preferences");
    }
}
