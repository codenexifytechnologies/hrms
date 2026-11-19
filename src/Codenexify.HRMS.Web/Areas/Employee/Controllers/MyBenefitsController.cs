using Codenexify.HRMS.Application.Benefits;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>An employee's perks & benefits: what they have, what they can join, and the family members covered.</summary>
[RequireFeature(FeatureCodes.Benefits)]
[Route("me/benefits")]
public sealed class MyBenefitsController(IEmployeeService employees, IBenefitService benefits) : SelfServiceController(employees)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        ViewBag.HasEmployee = me is not null;
        return View(me is int id ? await benefits.MineAsync(id, ct) : []);
    }

    [HttpPost("{planId:int}/join")]
    public async Task<IActionResult> Join(int planId, List<DependentInput> dependents, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await benefits.JoinAsync(planId, me, BenefitsController.Filled(dependents), ct), "You've joined the plan. Your share is deducted from your salary from this month.");
        return Redirect("/me/benefits");
    }

    [HttpPost("{planId:int}/leave")]
    public async Task<IActionResult> Leave(int planId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await benefits.LeaveAsync(planId, me, ct), "You've left the plan. Cover continues to the end of this month.");
        return Redirect("/me/benefits");
    }

    [HttpPost("enrollments/{enrollmentId:int}/dependents")]
    public async Task<IActionResult> Dependents(int enrollmentId, List<DependentInput> dependents, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await benefits.SaveDependentsAsync(enrollmentId, me, BenefitsController.Filled(dependents), ct), "Family members saved.");
        return Redirect("/me/benefits");
    }

    [HttpGet("{planId:int}/document")]
    public async Task<IActionResult> Document(int planId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotFound();
        var doc = await benefits.DocumentAsync(planId, me, ct);
        return doc is null ? NotFound() : File(doc.Content, "application/pdf", doc.FileName);
    }
}
