using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Domain.Support;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

/// <summary>Customer support desk and consented read-only access.</summary>
[Route("superadmin/support")]
public sealed class SupportController(ISupportTicketService tickets, ISupportAccessService access) : PlatformController
{
    [HttpGet(""), RequirePermission(PlatformPermissions.SupportManage)]
    public async Task<IActionResult> Index([FromQuery] TicketQuery query, CancellationToken ct)
    {
        if (query.Status is null && !Request.Query.ContainsKey(nameof(query.OpenOnly))) query.OpenOnly = true;
        ViewBag.Query = query;
        ViewBag.Counts = await tickets.CountsAsync(ct);
        return View(await tickets.ListAsync(query, ct));
    }

    [HttpGet("tickets/{id:int}"), RequirePermission(PlatformPermissions.SupportManage)]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
    {
        var view = await tickets.GetAsync(id, ct);
        if (view is null) return NotFound();
        ViewBag.Assignees = (await tickets.AssigneesAsync(ct)).Select(a => new SelectListItem(a.Name, a.Id.ToString(), a.Id == view.Ticket.AssignedToPlatformUserId)).ToList();
        return View(view);
    }

    [HttpPost("tickets/{id:int}/reply"), RequirePermission(PlatformPermissions.SupportManage)]
    public async Task<IActionResult> Reply(int id, string body, bool internalNote, TicketStatus? status, CancellationToken ct) =>
        Done(await tickets.ReplyAsync(id, body, internalNote, status, ct), internalNote ? "Internal note added." : "Reply sent.", Redirect($"/superadmin/support/tickets/{id}"));

    [HttpPost("tickets/{id:int}/update"), RequirePermission(PlatformPermissions.SupportManage)]
    public async Task<IActionResult> Update(int id, TicketStatus status, TicketPriority priority, int? assigneeId, CancellationToken ct) =>
        Done(await tickets.UpdateAsync(id, status, priority, assigneeId, ct), "Ticket updated.", Redirect($"/superadmin/support/tickets/{id}"));

    [HttpGet("access"), RequirePermission(PlatformPermissions.SupportAccess)]
    public async Task<IActionResult> Access(bool ended, CancellationToken ct)
    {
        if (ended) TempData["Error"] = "The support session ended (expired, revoked by the organization, or closed).";
        return View(await access.GrantsAsync(ct));
    }

    /// <summary>Starts a read-only visit: signs this browser into the organization portal as a support principal.</summary>
    [HttpPost("access/{grantId:int}/open"), RequirePermission(PlatformPermissions.SupportAccess)]
    public async Task<IActionResult> Open(int grantId, CancellationToken ct)
    {
        var result = await access.StartSessionAsync(grantId, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Redirect("/superadmin/support/access"); }
        var t = result.Value!;
        await HttpContext.SignInAsync(Schemes.Tenant, SupportSessionMiddleware.CreatePrincipal(t), new AuthenticationProperties
        {
            IsPersistent = false, AllowRefresh = false, ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(t.ExpiresAtUtc, DateTimeKind.Utc))
        });
        return Redirect("/org");
    }
}

[RequirePermission(PlatformPermissions.SupportManage)]
[Route("superadmin/announcements")]
public sealed class AnnouncementsController(IAnnouncementService announcements, IPlanService plans) : PlatformController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Plans = (await plans.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        return View(await announcements.ListAsync(ct));
    }

    [HttpGet("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var model = id is null ? new AnnouncementInput { StartsAt = DateTime.UtcNow.AddHours(5.5) } : await announcements.GetAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        ViewBag.Plans = await PlanItems(ct);
        return View(model);
    }

    [HttpPost("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, AnnouncementInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await announcements.SaveAsync(id, model, ct);
            if (result.Succeeded) { TempData["Success"] = "Announcement saved."; return Redirect("/superadmin/announcements"); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Id = id;
        ViewBag.Plans = await PlanItems(ct);
        return View(model);
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Done(await announcements.DeleteAsync(id, ct), "Announcement deleted.", Redirect("/superadmin/announcements"));

    private async Task<List<SelectListItem>> PlanItems(CancellationToken ct) =>
        (await plans.ListAsync(ct)).Where(p => p.IsActive).Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();
}

[RequirePermission(PlatformPermissions.SettingsManage)]
[Route("superadmin/system")]
public sealed class SystemController(IAnnouncementService system) : PlatformController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await system.GetSystemSettingsAsync(ct));

    [HttpPost("")]
    public async Task<IActionResult> Index(MaintenanceInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await system.SaveSystemSettingsAsync(model, ct);
        Flash(result, model.Enabled ? "Saved. Maintenance mode is ON: organization portals now show the maintenance page." : "Saved.");
        return Redirect("/superadmin/system");
    }

    /// <summary>Danger zone: delete every company. Super Admin only; the service re-checks role, password and 2FA.</summary>
    [HttpGet("reset")]
    public async Task<IActionResult> Reset([FromServices] IDataResetService reset, CancellationToken ct)
    {
        ViewBag.Status = await reset.StatusAsync(ct);
        return View(new DataResetInput());
    }

    [HttpPost("reset")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Reset(DataResetInput model, [FromServices] IDataResetService reset, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await reset.ResetAsync(model, ct);
            if (result.Succeeded)
            {
                var o = result.Value!;
                TempData["Success"] = $"All company data was deleted ({o.TablesEmptied} tables emptied). "
                    + (o.BackupFile is null ? "" : $"Backup: {o.BackupFile}. ")
                    + (o.UploadsMovedTo is null ? "" : $"Uploaded files moved to {o.UploadsMovedTo}. ")
                    + (o.Warning ?? "");
                return Redirect("/superadmin/system/reset");
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        model.Password = string.Empty;
        model.TotpCode = null;
        ViewBag.Status = await reset.StatusAsync(ct);
        return View(model);
    }
}
