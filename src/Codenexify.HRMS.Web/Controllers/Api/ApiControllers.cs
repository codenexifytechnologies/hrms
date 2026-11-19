using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Tenancy;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers.Api;

public sealed record TokenRequest([Required] string CompanyCode, [Required, EmailAddress] string Email, [Required] string Password);
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");

/// <summary>Mobile / integration API. Bearer tokens only — cookies are never accepted here, so no CSRF surface.</summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/v1/auth")]
public sealed class AuthApiController(ITenantAuthService auth, TokenService tokens) : ControllerBase
{
    /// <summary>Exchanges organization user credentials for a short-lived access token.</summary>
    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token(TokenRequest request, CancellationToken ct)
    {
        var result = await auth.SignInAsync(request.CompanyCode, request.Email, request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication failed", detail: result.Message);
        var (token, expires) = tokens.Issue(result.Principal!);
        return Ok(new TokenResponse(token, expires));
    }
}

[ApiController]
[IgnoreAntiforgeryToken]
[Authorize(Policy = Policies.TenantApi)]
[EnableRateLimiting("api")]
[Route("api/v1/me")]
public sealed class MeApiController(ITenantAdminService admin, IFeatureAccessService features) : ControllerBase
{
    /// <summary>Profile of the signed-in user with organization and enabled modules.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var me = await admin.GetMeAsync(ct);
        if (me is null) return NotFound();
        var org = await admin.GetOrganizationAsync(ct);
        var enabled = await features.GetEnabledFeaturesAsync(org.Id, ct);
        return Ok(new
        {
            user = new { me.Id, me.FullName, me.Email, me.Roles },
            organization = new { org.Id, org.Code, org.Name, status = org.Status.ToString(), org.BrandColor },
            permissions = User.FindAll(ClaimNames.Permission).Select(c => c.Value),
            features = enabled.OrderBy(f => f)
        });
    }
}
