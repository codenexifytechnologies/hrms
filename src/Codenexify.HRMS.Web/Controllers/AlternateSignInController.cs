using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers;

/// <summary>Single sign-on (Google / Microsoft) and mobile OTP sign-in for organization users.</summary>
[AllowAnonymous]
public sealed class AlternateSignInController(IAlternateSignInService signIn) : Controller
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Lists the sign-in methods a company offers (asks for the company code first).</summary>
    [HttpGet("/login/methods")]
    public async Task<IActionResult> Methods(string? company, CancellationToken ct)
    {
        ViewBag.Company = company?.Trim().ToLowerInvariant();
        ViewBag.Options = string.IsNullOrWhiteSpace(company) ? null : await signIn.OptionsAsync(company, ct);
        return View();
    }

    [HttpGet("/sso/{provider}")]
    public async Task<IActionResult> Start(string provider, string? company, CancellationToken ct)
    {
        if (!SsoProviders.IsKnown(provider) || string.IsNullOrWhiteSpace(company)) return Redirect("/login/methods");
        var options = await signIn.OptionsAsync(company, ct);
        if (!(provider == SsoProviders.Google ? options.Google : options.Microsoft))
        {
            TempData["Error"] = "That sign-in option isn't available for this company.";
            return Redirect($"/login/methods?company={Uri.EscapeDataString(company)}");
        }
        var props = new AuthenticationProperties { RedirectUri = "/sso/complete" };
        props.Items["org"] = company.Trim().ToLowerInvariant();
        props.Items["provider"] = provider;
        return Challenge(props, provider == SsoProviders.Google ? SsoSetup.GoogleScheme : SsoSetup.MicrosoftScheme);
    }

    [HttpGet("/sso/complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        var external = await HttpContext.AuthenticateAsync(SsoSetup.ExternalScheme);
        await HttpContext.SignOutAsync(SsoSetup.ExternalScheme);
        if (!external.Succeeded || external.Properties?.Items.TryGetValue("org", out var org) != true || org is null
            || external.Properties.Items.TryGetValue("provider", out var provider) != true || provider is null)
            return Redirect("/login?sso=failed");

        var claims = external.Principal!;
        string? email; bool verified;
        if (provider == SsoProviders.Google)
        {
            email = claims.FindFirst("email")?.Value;
            verified = string.Equals(claims.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Work accounts: the UPN's domain must be verified in the user's Entra tenant, unlike the optional "email" claim.
            email = claims.FindFirst("preferred_username")?.Value;
            verified = email is not null && claims.FindFirst("tid") is not null;
        }

        var result = await signIn.SignInExternalAsync(org, provider, email ?? "", verified, Ip, Request.Headers.UserAgent.ToString(), ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Message;
            return Redirect($"/login?company={Uri.EscapeDataString(org)}");
        }
        await HttpContext.SignInAsync(Schemes.Tenant, PrincipalFactory.Create(result.Principal!, Schemes.Tenant), AuthSetup.NewProperties(false));
        return Redirect(result.Principal!.Permissions.Contains(Application.Catalog.TenantPermissions.DashboardView) ? "/org" : "/me");
    }

    [HttpGet("/login/otp")]
    public IActionResult Otp(string? company)
    {
        ViewBag.Company = company?.Trim().ToLowerInvariant();
        ViewBag.Sent = false;
        return View();
    }

    [HttpPost("/login/otp/send"), EnableRateLimiting("login")]
    public async Task<IActionResult> SendOtp(string? company, string? mobile, CancellationToken ct)
    {
        ViewBag.Company = company?.Trim().ToLowerInvariant();
        ViewBag.Mobile = mobile;
        if (AlternateSignInService.NormalizeMobile(mobile) is null)
        {
            ModelState.AddModelError(string.Empty, "Enter a 10-digit Indian mobile number.");
            ViewBag.Sent = false;
            return View("Otp");
        }
        await signIn.RequestOtpAsync(company ?? "", mobile!, Ip, ct);
        ViewBag.Sent = true;
        return View("Otp");
    }

    [HttpPost("/login/otp"), EnableRateLimiting("login")]
    public async Task<IActionResult> VerifyOtp(string? company, string? mobile, string? code, CancellationToken ct)
    {
        var result = await signIn.SignInWithOtpAsync(company ?? "", mobile ?? "", code ?? "", Ip, Request.Headers.UserAgent.ToString(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Sign-in failed.");
            ViewBag.Company = company; ViewBag.Mobile = mobile; ViewBag.Sent = true;
            return View("Otp");
        }
        await HttpContext.SignInAsync(Schemes.Tenant, PrincipalFactory.Create(result.Principal!, Schemes.Tenant), AuthSetup.NewProperties(false));
        return Redirect(result.Principal!.Permissions.Contains(Application.Catalog.TenantPermissions.DashboardView) ? "/org" : "/me");
    }
}
