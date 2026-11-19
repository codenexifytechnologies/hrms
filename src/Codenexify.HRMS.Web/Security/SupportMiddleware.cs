using System.Security.Claims;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Domain.Enums;
using Microsoft.AspNetCore.Authentication;

namespace Codenexify.HRMS.Web.Security;

/// <summary>
/// Enforces a staff member's consented support session: it must still be active (grant not revoked or expired),
/// nothing but reads are allowed, and every page view goes into the organization's audit trail.
/// </summary>
public sealed class SupportSessionMiddleware(RequestDelegate next)
{
    public const string EndPath = "/support-session/end";

    public async Task InvokeAsync(HttpContext ctx, ISupportAccessService access)
    {
        var sessionId = ctx.User.GetSupportSessionId();
        if (sessionId is null) { await next(ctx); return; }

        if (!await access.IsSessionActiveAsync(sessionId.Value, ctx.RequestAborted))
        {
            await ctx.SignOutAsync(Schemes.Tenant);
            ctx.Response.Redirect("/superadmin/support/access?ended=1");
            return;
        }

        var path = ctx.Request.Path;
        var isRead = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method);
        var isEnd = HttpMethods.IsPost(ctx.Request.Method) && path.Equals(EndPath, StringComparison.OrdinalIgnoreCase);
        if (path.StartsWithSegments("/api") || (!isRead && !isEnd))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync("<!doctype html><title>Read-only</title><p style=\"font-family:sans-serif;padding:2rem\">This is a read-only support session: changes are disabled. <a href=\"/org\">Back to the portal</a></p>");
            return;
        }

        if (isRead && !path.StartsWithSegments("/support-session"))
            await access.RecordViewAsync(sessionId.Value, path + ctx.Request.QueryString, ctx.RequestAborted);
        await next(ctx);
    }

    /// <summary>Builds the read-only tenant principal for a started session.</summary>
    public static ClaimsPrincipal CreatePrincipal(SupportSessionTicket t)
    {
        var principal = PrincipalFactory.Create(new AuthenticatedPrincipal(
            PrincipalType.Tenant, 0, t.StaffEmail, $"{t.StaffName} (Codenexify support)", "support-session",
            t.OrganizationId, t.OrganizationCode, t.OrganizationName, "Codenexify support · read-only", t.Permissions, false), Schemes.Tenant);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(ClaimNames.SupportSession, t.SessionId.ToString()));
        identity.AddClaim(new Claim(ClaimNames.SupportUntil, t.ExpiresAtUtc.ToString("O")));
        return principal;
    }
}

/// <summary>While maintenance mode is on, organization portals and the API answer 503; the Control Center and webhooks keep working.</summary>
public sealed class MaintenanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IAnnouncementService system)
    {
        var path = ctx.Request.Path;
        if (path.StartsWithSegments("/superadmin") || path.StartsWithSegments("/webhooks") || path.StartsWithSegments("/error")
            || path.StartsWithSegments("/maintenance"))
        {
            await next(ctx);
            return;
        }
        var state = await system.MaintenanceAsync(ctx.RequestAborted);
        if (!state.Enabled) { await next(ctx); return; }

        ctx.Response.Headers.RetryAfter = "600";
        if (path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new { status = 503, title = "Scheduled maintenance", detail = state.Message });
            return;
        }
        ctx.Request.Path = "/maintenance";
        await next(ctx);
    }
}
