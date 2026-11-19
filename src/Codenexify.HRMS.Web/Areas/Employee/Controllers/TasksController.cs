using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Performance;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Tasks that automation rules assign to the signed-in user (they belong to the user, not to an employee record).</summary>
[RequireFeature(FeatureCodes.WorkflowAutomation)]
[Route("me/tasks")]
public sealed class TasksController(IEmployeeService employees, IWorkflowService workflows) : SelfServiceController(employees)
{
    private int UserId => User.GetUserId() ?? 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(bool done, CancellationToken ct)
    {
        ViewBag.ShowDone = done;
        return View(await workflows.TasksAsync(UserId, done, ct));
    }

    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, bool done, CancellationToken ct)
    {
        Flash(await workflows.ToggleTaskAsync(id, UserId, ct), "Task updated.");
        return Redirect(done ? "/me/tasks?done=true" : "/me/tasks");
    }
}
