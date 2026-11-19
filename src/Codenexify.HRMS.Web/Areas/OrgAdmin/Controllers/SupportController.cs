using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>The organization's support tickets and the consent it gives Codenexify for read-only access.</summary>
[Route("org/support")]
public sealed class SupportController(ISupportTicketService tickets, ISupportAccessService access) : OrgController
{
    private bool CanTickets => User.HasPermission(TenantPermissions.SupportTickets);
    private bool CanGrant => User.HasPermission(TenantPermissions.SupportAccessGrant);

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] TicketQuery query, CancellationToken ct)
    {
        if (!CanTickets && !CanGrant) return Forbid();
        ViewBag.Query = query;
        ViewBag.Grants = CanGrant ? await access.GrantsAsync(ct) : Array.Empty<GrantRow>();
        ViewBag.OpenTickets = CanGrant && CanTickets ? (await tickets.ListAsync(new TicketQuery { OpenOnly = true }, ct)).Items : Array.Empty<TicketRow>();
        return View(CanTickets ? await tickets.ListAsync(query, ct) : null);
    }

    [HttpGet("new"), RequirePermission(TenantPermissions.SupportTickets)]
    public IActionResult New() => View(new TicketInput());

    [HttpPost("new"), RequirePermission(TenantPermissions.SupportTickets)]
    public async Task<IActionResult> New(TicketInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await tickets.CreateAsync(model, ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(model); }
        TempData["Success"] = $"Ticket #{result.Value} raised. We'll reply here and by email.";
        return Redirect($"/org/support/tickets/{result.Value}");
    }

    [HttpGet("tickets/{id:int}"), RequirePermission(TenantPermissions.SupportTickets)]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
    {
        var view = await tickets.GetAsync(id, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("tickets/{id:int}/reply"), RequirePermission(TenantPermissions.SupportTickets)]
    public async Task<IActionResult> Reply(int id, string body, CancellationToken ct)
    {
        Flash(await tickets.ReplyAsync(id, body, ct: ct), "Reply sent.");
        return Redirect($"/org/support/tickets/{id}");
    }

    [HttpPost("tickets/{id:int}/close"), RequirePermission(TenantPermissions.SupportTickets)]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        Flash(await tickets.CloseAsync(id, ct), "Ticket closed.");
        return Redirect($"/org/support/tickets/{id}");
    }

    [HttpPost("tickets/{id:int}/reopen"), RequirePermission(TenantPermissions.SupportTickets)]
    public async Task<IActionResult> Reopen(int id, CancellationToken ct)
    {
        Flash(await tickets.ReopenAsync(id, ct), "Ticket reopened.");
        return Redirect($"/org/support/tickets/{id}");
    }

    [HttpPost("access/grant"), RequirePermission(TenantPermissions.SupportAccessGrant)]
    public async Task<IActionResult> Grant(GrantInput grant, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        else
            Flash(await access.GrantAsync(grant, ct), $"Codenexify support can view your data read-only for {grant.Hours} hour(s). You can revoke this at any time.");
        return Redirect("/org/support#access");
    }

    [HttpPost("access/{id:int}/revoke"), RequirePermission(TenantPermissions.SupportAccessGrant)]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        Flash(await access.RevokeAsync(id, ct), "Access revoked. Any open support session has ended.");
        return Redirect("/org/support#access");
    }
}

/// <summary>Lets a staff member leave a read-only support session.</summary>
[Authorize(Policy = Policies.Tenant)]
public sealed class SupportSessionController(ISupportAccessService access) : Controller
{
    [HttpPost(SupportSessionMiddleware.EndPath)]
    public async Task<IActionResult> End(CancellationToken ct)
    {
        if (User.GetSupportSessionId() is int id) await access.EndSessionAsync(id, ct);
        await HttpContext.SignOutAsync(Schemes.Tenant);
        return Redirect("/superadmin/support/access");
    }
}
