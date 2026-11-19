using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Web.Models;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers;

/// <summary>Client-organization sign-in and self-service registration.</summary>
[AllowAnonymous]
public sealed class AccountController(ITenantAuthService auth, IProvisioningService provisioning, IPlanService plans) : Controller
{
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl, string? company)
    {
        if (User.IsTenant()) return Redirect(User.HasPermission(Application.Catalog.TenantPermissions.DashboardView) ? "/org" : "/me");
        return View(new TenantLoginViewModel { ReturnUrl = returnUrl, CompanyCode = company ?? string.Empty });
    }

    [HttpPost("/login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(TenantLoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await auth.SignInAsync(model.CompanyCode, model.Email, model.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Sign-in failed.");
            return View(model);
        }

        await HttpContext.SignInAsync(Schemes.Tenant, PrincipalFactory.Create(result.Principal!, Schemes.Tenant), AuthSetup.NewProperties(model.RememberMe));
        if (Url.IsLocalUrl(model.ReturnUrl) && !model.ReturnUrl!.StartsWith("/superadmin", StringComparison.OrdinalIgnoreCase))
            return LocalRedirect(model.ReturnUrl);
        return Redirect(result.Principal!.Permissions.Contains(Application.Catalog.TenantPermissions.DashboardView) ? "/org" : "/me");
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Schemes.Tenant);
        return Redirect("/login");
    }

    [HttpGet("/denied")]
    public IActionResult Denied() => View();

    [HttpGet("/register")]
    public async Task<IActionResult> Register(int? plan, CancellationToken ct)
    {
        await LoadPlansAsync(ct);
        var model = new OrganizationRegistrationInput();
        if (plan is { } p) model.PlanId = p;
        return View(model);
    }

    [HttpPost("/register"), EnableRateLimiting("login")]
    public async Task<IActionResult> Register(OrganizationRegistrationInput model, CancellationToken ct)
    {
        // A signed-in company user acts inside their own organization and can't create another one in the same session.
        if (User.IsTenant())
            ModelState.AddModelError(string.Empty, "You're signed in to a company workspace. Sign out first, then register the new company.");
        else if (ModelState.IsValid)
        {
            var result = await provisioning.ProvisionAsync(model, startAsTrial: true, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your organization is ready. Sign in to start your free trial.";
                return Redirect($"/login?company={Uri.EscapeDataString(model.Code.Trim().ToLowerInvariant())}");
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await LoadPlansAsync(ct);
        return View(model);
    }

    [HttpGet("/register/check-code")]
    public async Task<IActionResult> CheckCode(string code, CancellationToken ct) =>
        Json(new { available = !string.IsNullOrWhiteSpace(code) && await provisioning.IsCodeAvailableAsync(code, ct) });

    private async Task LoadPlansAsync(CancellationToken ct)
    {
        var list = (await plans.ListAsync(ct)).Where(p => p.IsActive && p.IsPublic && !p.IsCustom).ToList();
        ViewBag.PlanList = list;
        ViewBag.Plans = list.Select(p => new SelectListItem($"{p.Name} — ₹{p.MonthlyPrice:N0}{(p.IsPerEmployeePricing ? "/employee" : "")}/month · {p.TrialDays}-day trial", p.Id.ToString())).ToList();
    }
}
