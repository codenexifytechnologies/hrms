using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Tenancy;
using Codenexify.HRMS.Web.Models;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Personal area for every signed-in organization user: own profile, documents and resignation.</summary>
[Area("Employee")]
[Authorize(Policy = Policies.Tenant)]
public sealed class HomeController(
    ITenantAdminService admin, IFeatureAccessService features, IEmployeeService employees,
    IDocumentService documents, IExitService exits, Application.Time.IAttendanceService attendance,
    Application.Time.ILeaveService leave, Application.Time.IGamificationService gamification,
    Application.Engagement.ISocialWallService wall, Application.Engagement.IPeopleService people) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await admin.GetMeAsync(ct);
        if (me is null) return Redirect("/login");
        var enabled = await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct);
        ViewBag.Organization = await admin.GetOrganizationAsync(ct);
        ViewBag.Features = enabled;
        ViewBag.Preferences = await people.PreferencesAsync(ct);
        if (enabled.Contains(FeatureCodes.SocialWall)) ViewBag.Announcements = await wall.PinnedAnnouncementsAsync(3, ct);
        if (enabled.Contains(FeatureCodes.NotificationsBasic)) ViewBag.Celebrations = await people.CelebrationsAsync(7, ct);

        if (await employees.GetMyEmployeeIdAsync(ct) is int employeeId)
        {
            // Own profile, never with decrypted bank/PAN values (masked only).
            ViewBag.Profile = await employees.GetProfileAsync(employeeId, includeSensitive: false, ct);
            if (enabled.Contains(FeatureCodes.DocumentsLetters)) ViewBag.Documents = await documents.ForEmployeeAsync(employeeId, visibleOnly: true, ct);
            if (enabled.Contains(FeatureCodes.EmployeeExit)) ViewBag.Exit = await exits.LatestForEmployeeAsync(employeeId, ct);
            if (enabled.Contains(FeatureCodes.Attendance) && ((Application.CoreHr.EmployeeProfile)ViewBag.Profile).Employee.IsCurrent)
            {
                ViewBag.Clock = await attendance.TodayAsync(employeeId, ct);
                if (enabled.Contains(FeatureCodes.AttendanceGamification)) ViewBag.Score = await gamification.ForEmployeeAsync(employeeId, ct);
            }
            if (enabled.Contains(FeatureCodes.Leave))
                ViewBag.Balances = await leave.BalancesForEmployeeAsync(employeeId, (await attendance.TodayForOrganizationAsync(ct)).Year, ct);
        }
        return View(me);
    }

    [RequireFeature(FeatureCodes.DocumentsLetters)]
    public async Task<IActionResult> Document(int docId, CancellationToken ct)
    {
        if (await employees.GetMyEmployeeIdAsync(ct) is not int employeeId) return NotFound();
        var doc = await documents.OpenAsync(docId, restrictToEmployeeId: employeeId, ct);
        return doc is null ? NotFound() : File(doc.Content, doc.ContentType, doc.FileName);
    }

    [HttpPost, RequireFeature(FeatureCodes.EmployeeExit)]
    public async Task<IActionResult> Resign(ExitInitiateInput resignation, CancellationToken ct)
    {
        if (await employees.GetMyEmployeeIdAsync(ct) is not int employeeId) return NotFound();
        resignation.Type = Domain.CoreHr.ExitType.Resignation;
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/me#exit");
        }
        var result = await exits.InitiateAsync(employeeId, resignation, byEmployee: true, ct);
        if (result.Succeeded) TempData["Success"] = "Your resignation has been submitted to HR."; else TempData["Error"] = result.Error;
        return Redirect("/me#exit");
    }

    [HttpPost, RequireFeature(FeatureCodes.EmployeeExit)]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        if (await employees.GetMyEmployeeIdAsync(ct) is not int employeeId) return NotFound();
        var result = await exits.WithdrawAsync(id, employeeId, ct);
        if (result.Succeeded) TempData["Success"] = "Your resignation has been withdrawn."; else TempData["Error"] = result.Error;
        return Redirect("/me#exit");
    }
}

[Area("Employee")]
[Authorize(Policy = Policies.Tenant)]
public sealed class AccountController(ITenantAdminService admin) : Controller
{
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await admin.ChangeOwnPasswordAsync(model.CurrentPassword, model.NewPassword, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }
        var company = User.FindFirst(ClaimNames.OrganizationCode)?.Value;
        await HttpContext.SignOutAsync(Schemes.Tenant);
        TempData["Success"] = "Password changed. Please sign in with your new password.";
        return Redirect($"/login?company={Uri.EscapeDataString(company ?? string.Empty)}");
    }
}
