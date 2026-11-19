using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>Finance review of expense claims, payout and categories.</summary>
[RequireFeature(FeatureCodes.Expenses), RequirePermission(TenantPermissions.ExpensesManage)]
[Route("org/expenses")]
public sealed class ExpensesController(IExpenseService expenses) : OrgController
{
    private static readonly Dictionary<string, ExpenseStatus[]> Tabs = new()
    {
        ["awaiting"] = [ExpenseStatus.Submitted, ExpenseStatus.ManagerApproved],
        ["topay"] = [ExpenseStatus.Approved],
        ["paid"] = [ExpenseStatus.Paid],
        ["closed"] = [ExpenseStatus.Rejected, ExpenseStatus.Cancelled],
    };

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab, int page, CancellationToken ct)
    {
        tab = tab is not null && Tabs.ContainsKey(tab) ? tab : "awaiting";
        ViewBag.Tab = tab;
        return View(await expenses.ListAsync(Tabs[tab], page, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var view = await expenses.GetAsync(id, null, null, ct);
        return view is null ? NotFound() : View(view);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ExpensePayout payout, string? notes, CancellationToken ct)
    {
        Flash(await expenses.FinanceApproveAsync(id, payout, notes, ct),
            payout == ExpensePayout.Payroll ? "Approved — it will be reimbursed with the next payroll." : "Approved for direct payment. Mark it paid once transferred.");
        return Redirect($"/org/expenses/{id}");
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, string notes, CancellationToken ct)
    {
        Flash(await expenses.FinanceRejectAsync(id, notes, ct), "Claim rejected.");
        return Redirect($"/org/expenses/{id}");
    }

    [HttpPost("{id:int}/paid")]
    public async Task<IActionResult> Paid(int id, DateOnly paidOn, string reference, CancellationToken ct)
    {
        Flash(await expenses.MarkPaidAsync(id, paidOn, reference, ct), "Marked as paid.");
        return Redirect($"/org/expenses/{id}");
    }

    [HttpGet("receipt/{itemId:int}")]
    public async Task<IActionResult> Receipt(int itemId, CancellationToken ct)
    {
        var file = await expenses.ReceiptAsync(itemId, null, null, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) => View(await expenses.CategoriesAsync(false, ct));

    [HttpPost("categories")]
    public async Task<IActionResult> SaveCategory(int? id, string name, decimal? maxPerItem, bool receiptRequired, bool isActive, CancellationToken ct)
    {
        Flash(await expenses.SaveCategoryAsync(id, name, maxPerItem, receiptRequired, isActive, ct), "Category saved.");
        return Redirect("/org/expenses/categories");
    }
}
