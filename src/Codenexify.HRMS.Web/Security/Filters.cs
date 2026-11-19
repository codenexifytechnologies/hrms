using Codenexify.HRMS.Application.Subscriptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Codenexify.HRMS.Web.Security;

/// <summary>Requires a permission claim (platform or tenant). Combine with an area-level policy.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public string Permission { get; } = permission;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return; // the policy produces the challenge
        if (!user.HasPermission(Permission)) context.Result = new ForbidResult();
    }
}

/// <summary>Blocks the action unless the signed-in organization's subscription includes the feature.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute(string featureCode) : Attribute, IAsyncActionFilter
{
    public string FeatureCode { get; } = featureCode;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var orgId = context.HttpContext.User.GetOrganizationId();
        var features = context.HttpContext.RequestServices.GetRequiredService<IFeatureAccessService>();
        if (orgId is null || !await features.IsEnabledAsync(orgId.Value, FeatureCode))
        {
            context.Result = context.Controller is ControllerBase { } c && c.GetType().IsDefined(typeof(ApiControllerAttribute), true)
                ? new ObjectResult(new ProblemDetails { Status = 402, Title = "Feature not included in your subscription", Detail = FeatureCode }) { StatusCode = 402 }
                : new RedirectToActionResult("Index", "Subscription", new { area = "OrgAdmin", feature = FeatureCode });
            return;
        }
        await next();
    }
}

/// <summary>Sends users who must change their password to the change-password page first.</summary>
public sealed class MustChangePasswordFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var action = context.RouteData.Values["action"]?.ToString();
        var controller = context.RouteData.Values["controller"]?.ToString();
        var isAccountPage = string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase);
        if (user.HasClaim(ClaimNames.MustChangePassword, "1") && !isAccountPage && context.Controller is Controller
            && !(action is "ChangePassword" or "Logout"))
        {
            var area = context.RouteData.Values["area"]?.ToString();
            context.Result = new RedirectToActionResult("ChangePassword", "Account", new { area = area == "SuperAdmin" ? "SuperAdmin" : "Employee" });
            return Task.CompletedTask;
        }
        return next();
    }
}
