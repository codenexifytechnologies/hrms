using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Engagement;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>HR review of employees' requested corrections to their personal details.</summary>
[RequireFeature(FeatureCodes.CoreHr), RequirePermission(TenantPermissions.EmployeesManage)]
[Route("org/employees/requests")]
public sealed class ProfileRequestsController(IProfileChangeService changes) : OrgController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(ProfileChangeStatus? status, int page, bool all, CancellationToken ct)
    {
        var filter = all ? status : status ?? ProfileChangeStatus.Pending;
        ViewBag.Status = filter;
        return View(await changes.ListAsync(filter, page, ct));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, string? notes, CancellationToken ct)
    {
        Flash(await changes.ApproveAsync(id, notes, ct), "Approved — the employee's details are updated.");
        return Redirect("/org/employees/requests");
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, string notes, CancellationToken ct)
    {
        Flash(await changes.RejectAsync(id, notes, ct), "Rejected — the employee has been told why.");
        return Redirect("/org/employees/requests");
    }
}
