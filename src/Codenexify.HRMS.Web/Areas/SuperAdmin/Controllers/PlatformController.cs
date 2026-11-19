using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

/// <summary>Base for every Super Admin controller: platform principals only.</summary>
[Area("SuperAdmin")]
[Authorize(Policy = Policies.Platform)]
public abstract class PlatformController : Controller
{
    protected int CurrentUserId => User.GetUserId() ?? 0;

    protected IActionResult Done(Result result, string successMessage, IActionResult next)
    {
        if (result.Succeeded) TempData["Success"] = successMessage;
        else TempData["Error"] = result.Error;
        return next;
    }

    protected void Flash(Result result, string successMessage)
    {
        if (result.Succeeded) TempData["Success"] = successMessage;
        else TempData["Error"] = result.Error;
    }
}
