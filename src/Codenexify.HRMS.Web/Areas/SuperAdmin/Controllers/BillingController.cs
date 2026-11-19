using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Codenexify.HRMS.Web.Areas.SuperAdmin.Controllers;

/// <summary>Subscription billing: GST invoices, payments, credit notes and revenue.</summary>
[RequirePermission(PlatformPermissions.BillingManage)]
[Route("superadmin/billing")]
public sealed class BillingController(IBillingService billing, IPlanService plans) : PlatformController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? view, CancellationToken ct)
    {
        var granularity = view is "quarter" or "year" ? view : "month";
        ViewBag.Granularity = granularity;
        ViewBag.Payments = await billing.RecentPaymentsAsync(10, ct);
        ViewBag.Overdue = (await billing.InvoicesAsync(new InvoiceQuery { OverdueOnly = true }, ct)).Items;
        return View(await billing.SummaryAsync(granularity, ct));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] InvoiceQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        ViewBag.Organizations = (await billing.OrganizationOptionsAsync(ct)).Select(o => new SelectListItem(o.Name, o.Id.ToString())).ToList();
        ViewBag.Plans = (await plans.ListAsync(ct)).Where(p => p.IsActive)
            .Select(p => new SelectListItem($"{p.Name} (₹{p.MonthlyPrice:N0}/m · ₹{p.YearlyPrice:N0}/y{(p.IsPerEmployeePricing ? " per employee" : "")})", p.Id.ToString())).ToList();
        return View(await billing.InvoicesAsync(query, ct));
    }

    [HttpPost("invoices/subscription")]
    public async Task<IActionResult> CreateSubscriptionInvoice(int organizationId, int planId, BillingCycle cycle, decimal? price, DateOnly startDate, InvoicePurpose purpose, CancellationToken ct)
    {
        if (purpose == InvoicePurpose.Manual) purpose = InvoicePurpose.NewSubscription;
        var result = await billing.CreateSubscriptionInvoiceAsync(organizationId, purpose, planId, cycle, price is > 0 ? price : null, startDate, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Redirect("/superadmin/billing/invoices"); }
        TempData["Success"] = "Invoice issued.";
        return Redirect($"/superadmin/billing/invoices/{result.Value}");
    }

    [HttpPost("invoices/manual")]
    public async Task<IActionResult> CreateManualInvoice(int organizationId, ManualInvoiceInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a description, quantity and unit price."; return Redirect("/superadmin/billing/invoices"); }
        var result = await billing.CreateManualInvoiceAsync(organizationId, input, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Redirect("/superadmin/billing/invoices"); }
        TempData["Success"] = "Invoice issued.";
        return Redirect($"/superadmin/billing/invoices/{result.Value}");
    }

    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, null, ct);
        if (invoice is null) return NotFound();
        ViewBag.CreditNotes = await billing.CreditNotesForAsync(id, ct);
        ViewBag.Today = DateOnly.FromDateTime(DateTime.Today);
        return View(invoice);
    }

    [HttpGet("invoices/{id:int}/print")]
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, null, ct);
        if (invoice is null) return NotFound();
        ViewBag.Supplier = await billing.SupplierAsync(ct);
        ViewBag.CreditNotes = await billing.CreditNotesForAsync(id, ct);
        ViewBag.Back = $"/superadmin/billing/invoices/{id}";
        return View("~/Views/Shared/InvoicePrint.cshtml", invoice);
    }

    [HttpGet("invoices/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, [FromServices] Application.Documents.IPdfRenderer pdf, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, null, ct);
        if (invoice is null) return NotFound();
        var model = Application.Documents.PdfModels.FromInvoice(invoice, await billing.SupplierAsync(ct), await billing.CreditNotesForAsync(id, ct));
        return File(pdf.Invoice(model), "application/pdf", $"Invoice-{invoice.Number.Replace('/', '-')}.pdf");
    }

    [HttpPost("invoices/{id:int}/payment")]
    public async Task<IActionResult> RecordPayment(int id, OfflinePaymentInput payment, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Enter the amount, reference and receipt date."; return Redirect($"/superadmin/billing/invoices/{id}"); }
        return Done(await billing.RecordOfflinePaymentAsync(id, payment, ct), "Payment recorded.", Redirect($"/superadmin/billing/invoices/{id}"));
    }

    [HttpPost("invoices/{id:int}/credit-note")]
    public async Task<IActionResult> CreditNote(int id, CreditNoteInput credit, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Enter the amount and reason."; return Redirect($"/superadmin/billing/invoices/{id}"); }
        return Done(await billing.IssueCreditNoteAsync(id, credit, ct), credit.Refund ? "Credit note issued and refund initiated." : "Credit note issued.",
            Redirect($"/superadmin/billing/invoices/{id}"));
    }

    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken ct) =>
        Done(await billing.CancelInvoiceAsync(id, reason, ct), "Invoice cancelled.", Redirect($"/superadmin/billing/invoices/{id}"));

    [HttpGet("settings"), RequirePermission(PlatformPermissions.SettingsManage)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        ViewBag.States = GstStates.CodeByState.Keys.Order().ToList();
        return View(await billing.GetSettingsAsync(ct));
    }

    [HttpPost("settings"), RequirePermission(PlatformPermissions.SettingsManage)]
    public async Task<IActionResult> Settings(PlatformSettingsInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await billing.SaveSettingsAsync(model, ct);
            if (result.Succeeded) { TempData["Success"] = "Settings saved."; return Redirect("/superadmin/billing/settings"); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        var saved = await billing.GetSettingsAsync(ct);
        model.HasRazorpaySecret = saved.HasRazorpaySecret; model.HasWebhookSecret = saved.HasWebhookSecret; model.HasSmtpPassword = saved.HasSmtpPassword;
        ViewBag.States = GstStates.CodeByState.Keys.Order().ToList();
        return View(model);
    }
}
