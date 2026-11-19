using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Codenexify.HRMS.Web.Controllers;

public sealed class ForgotPasswordViewModel
{
    /// <summary>Required on the company page; not used on the Control Center page.</summary>
    [StringLength(30), Display(Name = "Company code")]
    public string? CompanyCode { get; set; }
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;
    [Required, StringLength(PasswordResetService.MaxPasswordLength, MinimumLength = PasswordResetService.MinPasswordLength), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>"Forgot password" for company admins and employees.</summary>
[AllowAnonymous]
public sealed class PasswordResetController(IPasswordResetService resets, IConfiguration config, IHostEnvironment env, ILogger<PasswordResetController> logger) : Controller
{
    [HttpGet("/forgot-password")]
    public IActionResult Forgot(string? company) => View(new ForgotPasswordViewModel { CompanyCode = company ?? string.Empty });

    [HttpPost("/forgot-password"), EnableRateLimiting("login")]
    public async Task<IActionResult> Forgot(ForgotPasswordViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.CompanyCode)) ModelState.AddModelError(nameof(model.CompanyCode), "Enter your company code.");
        if (!ModelState.IsValid) return View(model);
        var baseUrl = PublicUrl.For(Request, config, env);
        if (baseUrl is null) logger.LogError("Password reset email not sent: set Platform:PublicUrl so emailed links point at this site.");
        else await resets.RequestForCompanyUserAsync(model.CompanyCode!, model.Email, baseUrl, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        // Same answer whether or not the account exists.
        return View("ForgotSent", model);
    }

    [HttpGet("/reset-password")]
    public async Task<IActionResult> Reset(string? token, CancellationToken ct)
    {
        var link = await resets.CheckAsync(token ?? string.Empty, ct);
        if (link is null || link.Type != PrincipalType.Tenant) return View("ResetInvalid");
        ViewBag.Link = link;
        return View(new ResetPasswordViewModel { Token = token! });
    }

    [HttpPost("/reset-password"), EnableRateLimiting("login")]
    public async Task<IActionResult> Reset(ResetPasswordViewModel model, CancellationToken ct)
    {
        var link = await resets.CheckAsync(model.Token, ct);
        if (link is null || link.Type != PrincipalType.Tenant) return View("ResetInvalid");
        if (ModelState.IsValid)
        {
            var result = await resets.ResetAsync(model.Token, model.NewPassword, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Your password was changed. Sign in with the new password.";
                return Redirect($"/login?company={Uri.EscapeDataString(link.OrganizationCode ?? string.Empty)}");
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        ViewBag.Link = link;
        return View(new ResetPasswordViewModel { Token = model.Token });
    }
}
