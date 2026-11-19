using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

/// <summary>The organization's own invoices, online payment and self-service plan purchase.</summary>
[RequirePermission(TenantPermissions.BillingManage)]
[Route("org/billing")]
public sealed class BillingController(IBillingService billing, ICurrentContext context) : OrgController
{
    private int OrgId => context.OrganizationId!.Value;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken ct)
    {
        ViewBag.Options = await billing.PurchaseOptionsAsync(ct);
        return View(await billing.InvoicesAsync(new InvoiceQuery { Page = page }, ct));
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Buy(int planId, BillingCycle cycle, CancellationToken ct)
    {
        var result = await billing.CreatePurchaseInvoiceAsync(planId, cycle, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Redirect("/org/billing"); }
        TempData["Success"] = "Invoice created. Pay it to activate your plan.";
        return Redirect($"/org/billing/invoices/{result.Value}");
    }

    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> Invoice(int id, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, OrgId, ct);
        if (invoice is null) return NotFound();
        ViewBag.CreditNotes = await billing.CreditNotesForAsync(id, ct);
        ViewBag.Online = await billing.OnlinePaymentsEnabledAsync(ct);
        ViewBag.Supplier = await billing.SupplierAsync(ct);
        return View(invoice);
    }

    [HttpGet("invoices/{id:int}/print")]
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, OrgId, ct);
        if (invoice is null) return NotFound();
        ViewBag.Supplier = await billing.SupplierAsync(ct);
        ViewBag.CreditNotes = await billing.CreditNotesForAsync(id, ct);
        ViewBag.Back = $"/org/billing/invoices/{id}";
        return View("~/Views/Shared/InvoicePrint.cshtml", invoice);
    }

    [HttpGet("invoices/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, [FromServices] Application.Documents.IPdfRenderer pdf, CancellationToken ct)
    {
        var invoice = await billing.GetInvoiceAsync(id, OrgId, ct);
        if (invoice is null) return NotFound();
        var model = Application.Documents.PdfModels.FromInvoice(invoice, await billing.SupplierAsync(ct), await billing.CreditNotesForAsync(id, ct));
        return File(pdf.Invoice(model), "application/pdf", $"Invoice-{invoice.Number.Replace('/', '-')}.pdf");
    }

    /// <summary>Creates a gateway order and shows the checkout page.</summary>
    [HttpPost("invoices/{id:int}/pay")]
    public async Task<IActionResult> Pay(int id, CancellationToken ct)
    {
        var result = await billing.StartOnlinePaymentAsync(id, OrgId, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Redirect($"/org/billing/invoices/{id}"); }
        ViewBag.InvoiceId = id;
        return View("Checkout", result.Value);
    }

    /// <summary>Checkout success callback: the signature is verified on the server before anything is marked paid.</summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(int invoiceId, [FromForm(Name = "razorpay_order_id")] string orderId,
        [FromForm(Name = "razorpay_payment_id")] string paymentId, [FromForm(Name = "razorpay_signature")] string signature, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
        {
            TempData["Error"] = "The payment was not completed.";
            return Redirect($"/org/billing/invoices/{invoiceId}");
        }
        var result = await billing.ConfirmOnlinePaymentAsync(orderId, paymentId, signature, OrgId, ct);
        Flash(result, "Payment received — thank you!");
        return Redirect($"/org/billing/invoices/{invoiceId}");
    }
}
