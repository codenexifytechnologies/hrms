using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Codenexify.HRMS.Web.Models;

public sealed class PlatformLoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorViewModel
{
    [Required, StringLength(7, MinimumLength = 6), Display(Name = "Authentication code")]
    public string Code { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public sealed class TenantLoginViewModel
{
    [Required, StringLength(30), Display(Name = "Company code")]
    public string CompanyCode { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 10), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class TwoFactorSetupViewModel
{
    public bool Enabled { get; set; }
    public string? Secret { get; set; }
    public string? ProvisioningUri { get; set; }
    [StringLength(7, MinimumLength = 6), Display(Name = "Code from your authenticator app")]
    public string? Code { get; set; }
    [DataType(DataType.Password), Display(Name = "Password")]
    public string? Password { get; set; }
}

public sealed class SelectOptions
{
    public IEnumerable<SelectListItem> Plans { get; set; } = [];
}

/// <summary>Pagination info rendered by the shared _Pager partial.</summary>
public sealed record PagerModel(int Page, int TotalPages, int TotalCount);

public static class PagedExtensions
{
    public static PagerModel ToPager<T>(this Application.Common.PagedResult<T> r) => new(r.Page, r.TotalPages, r.TotalCount);
}
