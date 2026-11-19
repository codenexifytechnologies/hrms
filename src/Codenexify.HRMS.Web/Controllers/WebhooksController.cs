using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Controllers;

/// <summary>Server-to-server callbacks. Authenticity comes from the HMAC signature, not from a session.</summary>
[AllowAnonymous, IgnoreAntiforgeryToken]
public sealed class WebhooksController(IBillingService billing, ICurrentContext context, ILogger<WebhooksController> logger) : Controller
{
    private const int MaxBodyBytes = 256 * 1024;

    [HttpPost("/webhooks/razorpay")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Razorpay(CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        // Never act on a browser session that happens to accompany the call.
        HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        try
        {
            using var platform = context.BeginPlatformScope();
            var result = await billing.HandleWebhookAsync(body, signature, ct);
            if (!result.Succeeded)
            {
                logger.LogWarning("Razorpay webhook rejected: {Reason}", result.Error);
                return BadRequest();
            }
            return Ok();
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest();
        }
    }
}
