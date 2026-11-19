using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

/// <summary>Employees claim expenses; reporting managers review their team's claims.</summary>
[RequireFeature(FeatureCodes.Expenses)]
[Route("me/expenses")]
public sealed class ExpensesController(IEmployeeService employees, IExpenseService expenses) : SelfServiceController(employees)
{
    private const int FormRows = 5;
    private int MeUser => User.GetUserId() ?? 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        ViewBag.Categories = await expenses.CategoriesAsync(true, ct);
        ViewBag.ToReview = await expenses.AwaitingManagerAsync(MeUser, ct);
        ViewBag.Rows = FormRows;
        ViewBag.HasEmployee = me is not null;
        return View(me is int id ? await expenses.MineAsync(id, ct) : Array.Empty<ExpenseClaimRow>());
    }

    [HttpPost(""), RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Submit(ExpenseClaimInput claim, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct()); return Redirect("/me/expenses"); }
        var receipts = new Dictionary<int, UploadedFile>();
        for (var i = 0; i < FormRows; i++)
            if (Request.Form.Files.GetFile($"receipt_{i}") is { Length: > 0 } f) receipts[i] = new UploadedFile(f.OpenReadStream(), f.FileName, f.Length);
        var r = await expenses.SubmitAsync(me, claim, receipts, ct);
        foreach (var f in receipts.Values) await f.Content.DisposeAsync();
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/me/expenses"); }
        TempData["Success"] = "Claim submitted for approval.";
        return Redirect($"/me/expenses/{r.Value}");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var view = await expenses.GetAsync(id, me, null, ct);
        if (view is null) return NotFound();
        ViewBag.Mode = "mine";
        return View("Claim", view);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        Flash(await expenses.CancelAsync(id, me, ct), "Claim cancelled.");
        return Redirect($"/me/expenses/{id}");
    }

    [HttpGet("review/{id:int}")]
    public async Task<IActionResult> Review(int id, CancellationToken ct)
    {
        var view = await expenses.GetAsync(id, null, MeUser, ct);
        if (view is null) return NotFound();
        ViewBag.Mode = "manager";
        return View("Claim", view);
    }

    [HttpPost("review/{id:int}")]
    public async Task<IActionResult> Decide(int id, bool approve, string? notes, CancellationToken ct)
    {
        Flash(await expenses.ManagerDecideAsync(id, MeUser, approve, notes, ct), approve ? "Approved and sent to finance." : "Claim rejected.");
        return Redirect("/me/expenses");
    }

    [HttpGet("receipt/{itemId:int}")]
    public async Task<IActionResult> Receipt(int itemId, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        var file = (me is int id ? await expenses.ReceiptAsync(itemId, id, null, ct) : null) ?? await expenses.ReceiptAsync(itemId, null, MeUser, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
