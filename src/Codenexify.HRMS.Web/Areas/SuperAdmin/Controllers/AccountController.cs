using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Web.Models;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

public sealed class AccountController(IPlatformAuthService auth, IPlatformUserService users) : PlatformController
{
    private const string PendingKey = "cnx.2fa.pending";

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.IsPlatform()) return Redirect("/superadmin");
        return View(new PlatformLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(PlatformLoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await auth.SignInAsync(model.Email, model.Password, null, Ip, Ua, ct);
        if (result.Status == AuthStatus.RequiresTwoFactor)
        {
            // Password verified; the second factor is completed on the next page within 5 minutes.
            // Only the verified user id is carried forward (TempData cookies are encrypted by Data Protection).
            TempData[PendingKey] = System.Text.Json.JsonSerializer.Serialize(new PendingLogin(result.PendingUserId!.Value, model.RememberMe, DateTime.UtcNow));
            return Redirect("/superadmin/account/twofactor" + (string.IsNullOrEmpty(model.ReturnUrl) ? "" : "?returnUrl=" + Uri.EscapeDataString(model.ReturnUrl)));
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Sign-in failed.");
            return View(model);
        }
        return await CompleteAsync(result.Principal!, model.RememberMe, model.ReturnUrl);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult TwoFactor(string? returnUrl)
    {
        if (TempData.Peek(PendingKey) is null) return Redirect("/superadmin/account/login");
        return View(new TwoFactorViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> TwoFactor(TwoFactorViewModel model, CancellationToken ct)
    {
        if (TempData.Peek(PendingKey) is not string json) return Redirect("/superadmin/account/login");
        var pending = System.Text.Json.JsonSerializer.Deserialize<PendingLogin>(json)!;
        if (DateTime.UtcNow - pending.StartedUtc > TimeSpan.FromMinutes(5))
        {
            TempData.Remove(PendingKey);
            TempData["Error"] = "The sign-in attempt expired. Please sign in again.";
            return Redirect("/superadmin/account/login");
        }
        if (!ModelState.IsValid) return View(model);

        var result = await auth.CompleteTwoFactorAsync(pending.UserId, model.Code, Ip, Ua, ct);
        if (!result.Succeeded)
        {
            if (result.Status is not AuthStatus.InvalidTwoFactorCode) { TempData.Remove(PendingKey); TempData["Error"] = result.Message; return Redirect("/superadmin/account/login"); }
            ModelState.AddModelError(string.Empty, result.Message ?? "Invalid code.");
            return View(model);
        }
        TempData.Remove(PendingKey);
        return await CompleteAsync(result.Principal!, pending.RememberMe, model.ReturnUrl);
    }

    private async Task<IActionResult> CompleteAsync(AuthenticatedPrincipal principal, bool remember, string? returnUrl)
    {
        await HttpContext.SignInAsync(Schemes.Platform, PrincipalFactory.Create(principal, Schemes.Platform), AuthSetup.NewProperties(remember));
        if (principal.MustChangePassword) return Redirect("/superadmin/account/changepassword");
        return Url.IsLocalUrl(returnUrl) && returnUrl!.StartsWith("/superadmin", StringComparison.OrdinalIgnoreCase)
            ? LocalRedirect(returnUrl) : Redirect("/superadmin");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Schemes.Platform);
        return Redirect("/superadmin/account/login");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Denied() => View();

    // ------------------------------------------------------------------ forgot password (same pages as companies)

    private const string ResetViews = "~/Views/PasswordReset/";

    [AllowAnonymous, HttpGet("/superadmin/account/forgot-password")]
    public IActionResult ForgotPassword()
    {
        ViewData["Portal"] = "platform";
        return View(ResetViews + "Forgot.cshtml", new Web.Controllers.ForgotPasswordViewModel());
    }

    [AllowAnonymous, HttpPost("/superadmin/account/forgot-password"), EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(Web.Controllers.ForgotPasswordViewModel model,
        [FromServices] IPasswordResetService resets, [FromServices] IConfiguration config, [FromServices] IHostEnvironment env,
        [FromServices] ILogger<AccountController> logger, CancellationToken ct)
    {
        ViewData["Portal"] = "platform";
        model.CompanyCode = null;
        if (!ModelState.IsValid) return View(ResetViews + "Forgot.cshtml", model);
        var baseUrl = PublicUrl.For(Request, config, env);
        if (baseUrl is null) logger.LogError("Password reset email not sent: set Platform:PublicUrl so emailed links point at this site.");
        else await resets.RequestForPlatformUserAsync(model.Email, baseUrl, Ip, ct);
        return View(ResetViews + "ForgotSent.cshtml", model);
    }

    [AllowAnonymous, HttpGet("/superadmin/account/reset-password")]
    public async Task<IActionResult> ResetPassword(string? token, [FromServices] IPasswordResetService resets, CancellationToken ct)
    {
        ViewData["Portal"] = "platform";
        var link = await resets.CheckAsync(token ?? string.Empty, ct);
        if (link is null || link.Type != Domain.Enums.PrincipalType.Platform) return View(ResetViews + "ResetInvalid.cshtml");
        ViewBag.Link = link;
        return View(ResetViews + "Reset.cshtml", new Web.Controllers.ResetPasswordViewModel { Token = token! });
    }

    [AllowAnonymous, HttpPost("/superadmin/account/reset-password"), EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(Web.Controllers.ResetPasswordViewModel model, [FromServices] IPasswordResetService resets, CancellationToken ct)
    {
        ViewData["Portal"] = "platform";
        var link = await resets.CheckAsync(model.Token, ct);
        if (link is null || link.Type != Domain.Enums.PrincipalType.Platform) return View(ResetViews + "ResetInvalid.cshtml");
        if (ModelState.IsValid)
        {
            var result = await resets.ResetAsync(model.Token, model.NewPassword, Ip, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your password was changed. Sign in with the new password.";
                return Redirect("/superadmin/account/login");
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Link = link;
        return View(ResetViews + "Reset.cshtml", new Web.Controllers.ResetPasswordViewModel { Token = model.Token });
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await users.ChangeOwnPasswordAsync(CurrentUserId, model.CurrentPassword, model.NewPassword, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        await HttpContext.SignOutAsync(Schemes.Platform);
        TempData["Success"] = "Password changed. Please sign in with your new password.";
        return Redirect("/superadmin/account/login");
    }

    [HttpGet]
    public async Task<IActionResult> Security(CancellationToken ct)
    {
        var me = await users.GetForEditAsync(CurrentUserId, ct);
        var list = await users.ListAsync(ct);
        var enabled = list.FirstOrDefault(u => u.Id == CurrentUserId)?.TwoFactorEnabled ?? false;
        var model = new TwoFactorSetupViewModel { Enabled = enabled };
        if (!enabled)
        {
            var (secret, uri) = await users.BeginTwoFactorSetupAsync(CurrentUserId, ct);
            model.Secret = secret;
            model.ProvisioningUri = uri;
        }
        ViewBag.Email = me?.Email;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EnableTwoFactor(string secret, string code, CancellationToken ct)
    {
        var result = await users.ConfirmTwoFactorSetupAsync(CurrentUserId, secret, code ?? string.Empty, ct);
        Flash(result, "Two-factor authentication is now enabled.");
        return Redirect("/superadmin/account/security");
    }

    [HttpPost]
    public async Task<IActionResult> DisableTwoFactor(string password, CancellationToken ct)
    {
        var result = await users.DisableOwnTwoFactorAsync(CurrentUserId, password ?? string.Empty, ct);
        Flash(result, "Two-factor authentication disabled.");
        return Redirect("/superadmin/account/security");
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string Ua => Request.Headers.UserAgent.ToString();

    private sealed record PendingLogin(int UserId, bool RememberMe, DateTime StartedUtc);
}
