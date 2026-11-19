using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Billing;

public sealed class PlatformSettingsInput
{
    [Required, StringLength(200), Display(Name = "Legal name")] public string LegalName { get; set; } = string.Empty;
    [StringLength(15), RegularExpression("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "Enter a valid GSTIN."), Display(Name = "GSTIN")] public string? Gstin { get; set; }
    [StringLength(10), Display(Name = "PAN")] public string? Pan { get; set; }
    [StringLength(300), Display(Name = "Address")] public string? AddressLine { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [Required, StringLength(100)] public string State { get; set; } = "Maharashtra";
    [StringLength(10), Display(Name = "PIN code")] public string? PostalCode { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "Billing email")] public string? BillingEmail { get; set; }
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [Required, StringLength(6), RegularExpression("^[A-Z0-9]+$"), Display(Name = "Invoice prefix")] public string InvoicePrefix { get; set; } = "CNX";
    [Required, StringLength(6), RegularExpression("^[A-Z0-9]+$"), Display(Name = "Credit note prefix")] public string CreditNotePrefix { get; set; } = "CNXCN";
    [Required, StringLength(8), Display(Name = "SAC code")] public string SacCode { get; set; } = "998314";
    [Range(0, 50), Display(Name = "GST rate %")] public decimal GstRate { get; set; } = 18;
    [Range(0, 90), Display(Name = "Payment terms (days)")] public int InvoiceDueDays { get; set; } = 7;
    [Range(0, 60), Display(Name = "Raise renewal invoice (days before end)")] public int RenewalInvoiceDaysBefore { get; set; } = 7;
    [Required, StringLength(60), RegularExpression("^-?[0-9]{1,2}(,-?[0-9]{1,2})*$", ErrorMessage = "Comma separated whole numbers, e.g. 3,1,0,-3."), Display(Name = "Reminder schedule (days before due; negative = overdue)")]
    public string ReminderSchedule { get; set; } = "3,1,0,-3,-7";
    [StringLength(500), Display(Name = "Bank details printed on invoices")] public string? BankDetails { get; set; }

    [Display(Name = "Accept online payments (Razorpay)")] public bool RazorpayEnabled { get; set; }
    [StringLength(100), Display(Name = "Razorpay key id")] public string? RazorpayKeyId { get; set; }
    [StringLength(200), Display(Name = "Razorpay key secret (blank = keep)")] public string? RazorpayKeySecret { get; set; }
    [StringLength(200), Display(Name = "Razorpay webhook secret (blank = keep)")] public string? RazorpayWebhookSecret { get; set; }
    public bool HasRazorpaySecret { get; set; }
    public bool HasWebhookSecret { get; set; }

    [Display(Name = "Send email")] public bool SmtpEnabled { get; set; }
    [StringLength(200), Display(Name = "SMTP host")] public string? SmtpHost { get; set; }
    [Range(1, 65535), Display(Name = "Port")] public int SmtpPort { get; set; } = 587;
    [Display(Name = "Use TLS/SSL")] public bool SmtpUseSsl { get; set; } = true;
    [StringLength(200), Display(Name = "Username")] public string? SmtpUsername { get; set; }
    [StringLength(200), Display(Name = "Password (blank = keep)")] public string? SmtpPassword { get; set; }
    public bool HasSmtpPassword { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "From email")] public string? SmtpFromEmail { get; set; }
    [Required, StringLength(100), Display(Name = "From name")] public string SmtpFromName { get; set; } = "Codenexify HRMS";

    [StringLength(200), Display(Name = "Google client id")] public string? GoogleClientId { get; set; }
    [StringLength(200), Display(Name = "Google client secret (blank = keep)")] public string? GoogleClientSecret { get; set; }
    public bool HasGoogleSecret { get; set; }
    [StringLength(200), Display(Name = "Microsoft application (client) id")] public string? MicrosoftClientId { get; set; }
    [StringLength(200), Display(Name = "Microsoft client secret (blank = keep)")] public string? MicrosoftClientSecret { get; set; }
    public bool HasMicrosoftSecret { get; set; }

    [Display(Name = "Send SMS one-time codes (MSG91)")] public bool SmsEnabled { get; set; }
    [StringLength(200), Display(Name = "MSG91 auth key (blank = keep)")] public string? Msg91AuthKey { get; set; }
    public bool HasMsg91Key { get; set; }
    [StringLength(100), Display(Name = "DLT flow template id")] public string? Msg91TemplateId { get; set; }
    [Required, StringLength(30), RegularExpression("^[A-Za-z0-9_]+$"), Display(Name = "Template variable for the code")] public string Msg91OtpVariable { get; set; } = "otp";
}

public sealed class ManualInvoiceInput
{
    [Required, StringLength(300)] public string Description { get; set; } = string.Empty;
    [Range(0.01, 100_000_000)] public decimal Quantity { get; set; } = 1;
    [Range(0.01, 100_000_000), Display(Name = "Unit price (₹, before GST)")] public decimal UnitPrice { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class OfflinePaymentInput
{
    [Range(0.01, 100_000_000)] public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    [Required, StringLength(100), Display(Name = "Reference (UTR / cheque no.)")] public string Reference { get; set; } = string.Empty;
    [Required, Display(Name = "Received on")] public DateOnly ReceivedOn { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class CreditNoteInput
{
    [Range(0.01, 100_000_000), Display(Name = "Credit amount (incl. GST)")] public decimal Amount { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [Display(Name = "Refund to the customer")] public bool Refund { get; set; }
    [Display(Name = "Refund against payment")] public int? PaymentId { get; set; }
}

public sealed record InvoiceRow(int Id, string Number, int OrganizationId, string OrganizationName, InvoicePurpose Purpose, DateOnly InvoiceDate, DateOnly DueDate,
    decimal Total, decimal Balance, InvoiceStatus Status, bool Overdue);

public sealed record CheckoutData(string KeyId, string OrderId, long AmountPaise, string Currency, string InvoiceNumber, string CustomerName, string? CustomerEmail, string Description);

public sealed record RevenuePoint(string Label, decimal Collected, decimal Invoiced);

public sealed class BillingSummary
{
    public decimal Outstanding { get; init; }
    public decimal Overdue { get; init; }
    public int OverdueCount { get; init; }
    public decimal CollectedThisMonth { get; init; }
    public decimal CollectedThisYear { get; init; }
    public decimal RefundedThisYear { get; init; }
    public IReadOnlyList<RevenuePoint> Revenue { get; init; } = [];
}

public sealed class InvoiceQuery
{
    public InvoiceStatus? Status { get; set; }
    public bool OverdueOnly { get; set; }
    public int? OrganizationId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
}

public interface IBillingService
{
    // settings (platform)
    Task<PlatformSettingsInput> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(PlatformSettingsInput input, CancellationToken ct = default);
    Task<bool> OnlinePaymentsEnabledAsync(CancellationToken ct = default);

    // invoices
    Task<Result<int>> CreateSubscriptionInvoiceAsync(int organizationId, InvoicePurpose purpose, int planId, BillingCycle cycle, decimal? unitPrice, DateOnly periodStart, CancellationToken ct = default);
    Task<Result<int>> CreateManualInvoiceAsync(int organizationId, ManualInvoiceInput input, CancellationToken ct = default);
    Task<Result> CancelInvoiceAsync(int invoiceId, string reason, CancellationToken ct = default);
    Task<PagedResult<InvoiceRow>> InvoicesAsync(InvoiceQuery query, CancellationToken ct = default);
    /// <param name="organizationId">When set, the invoice must belong to that organization (tenant access).</param>
    Task<Invoice?> GetInvoiceAsync(int invoiceId, int? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<CreditNote>> CreditNotesForAsync(int invoiceId, CancellationToken ct = default);
    Task<PlatformSetting> SupplierAsync(CancellationToken ct = default);

    // payments
    Task<Result> RecordOfflinePaymentAsync(int invoiceId, OfflinePaymentInput input, CancellationToken ct = default);
    Task<Result<CheckoutData>> StartOnlinePaymentAsync(int invoiceId, int? organizationId, CancellationToken ct = default);
    Task<Result> ConfirmOnlinePaymentAsync(string orderId, string paymentId, string signature, int? organizationId, CancellationToken ct = default);
    Task<Result> HandleWebhookAsync(string body, string signature, CancellationToken ct = default);
    Task<Result> IssueCreditNoteAsync(int invoiceId, CreditNoteInput input, CancellationToken ct = default);

    // self-service purchase (tenant)
    Task<Result<int>> CreatePurchaseInvoiceAsync(int planId, BillingCycle cycle, CancellationToken ct = default);

    Task<PurchaseOptions> PurchaseOptionsAsync(CancellationToken ct = default);

    // analytics
    Task<BillingSummary> SummaryAsync(string granularity, CancellationToken ct = default);
    Task<IReadOnlyList<CoreHr.LookupItem>> OrganizationOptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PaymentRow>> RecentPaymentsAsync(int take, CancellationToken ct = default);
}

public sealed record PaymentRow(int Id, int InvoiceId, string InvoiceNumber, string OrganizationName, decimal Amount, decimal Refunded, PaymentMethod Method, PaymentStatus Status, string? Reference, DateTime? PaidAtUtc);

public sealed record PlanOption(int Id, string Name, string? Description, decimal MonthlyPrice, decimal YearlyPrice, bool PerEmployee);

public sealed record PurchaseOptions(bool CanPurchase, string? Reason, int Seats, IReadOnlyList<PlanOption> Plans, int? PendingInvoiceId, bool OnlinePayments);

public sealed class BillingService(
    IAppDbContext db, ICurrentContext context, IClock clock, ISecretProtector protector, IPaymentGateway gateway,
    ISubscriptionService subscriptions, IEmailSender email, IAuditWriter audit) : IBillingService
{
    private void EnsurePlatform()
    {
        if (!context.IsPlatform) throw new TenantIsolationException("This billing operation requires a platform principal.");
    }

    private async Task<PlatformSetting> SettingsEntityAsync(CancellationToken ct)
    {
        var s = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (s is not null) return s;
        s = new PlatformSetting();
        db.PlatformSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public Task<PlatformSetting> SupplierAsync(CancellationToken ct = default) => SettingsEntityAsync(ct);

    public async Task<bool> OnlinePaymentsEnabledAsync(CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return s is { RazorpayEnabled: true, RazorpayKeyId: not null, RazorpayKeySecretProtected: not null };
    }

    public async Task<PlatformSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        var s = await SettingsEntityAsync(ct);
        return new PlatformSettingsInput
        {
            LegalName = s.LegalName, Gstin = s.Gstin, Pan = s.Pan, AddressLine = s.AddressLine, City = s.City, State = s.State, PostalCode = s.PostalCode,
            BillingEmail = s.BillingEmail, Phone = s.Phone, InvoicePrefix = s.InvoicePrefix, CreditNotePrefix = s.CreditNotePrefix, SacCode = s.SacCode,
            GstRate = s.GstRate, InvoiceDueDays = s.InvoiceDueDays, RenewalInvoiceDaysBefore = s.RenewalInvoiceDaysBefore, ReminderSchedule = s.ReminderSchedule,
            BankDetails = s.BankDetails, RazorpayEnabled = s.RazorpayEnabled, RazorpayKeyId = s.RazorpayKeyId,
            HasRazorpaySecret = s.RazorpayKeySecretProtected is not null, HasWebhookSecret = s.RazorpayWebhookSecretProtected is not null,
            SmtpEnabled = s.SmtpEnabled, SmtpHost = s.SmtpHost, SmtpPort = s.SmtpPort, SmtpUseSsl = s.SmtpUseSsl, SmtpUsername = s.SmtpUsername,
            HasSmtpPassword = s.SmtpPasswordProtected is not null, SmtpFromEmail = s.SmtpFromEmail, SmtpFromName = s.SmtpFromName,
            GoogleClientId = s.GoogleClientId, HasGoogleSecret = s.GoogleClientSecretProtected is not null,
            MicrosoftClientId = s.MicrosoftClientId, HasMicrosoftSecret = s.MicrosoftClientSecretProtected is not null,
            SmsEnabled = s.SmsEnabled, HasMsg91Key = s.Msg91AuthKeyProtected is not null, Msg91TemplateId = s.Msg91TemplateId, Msg91OtpVariable = s.Msg91OtpVariable
        };
    }

    public async Task<Result> SaveSettingsAsync(PlatformSettingsInput i, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (GstStates.CodeFor(i.Gstin, i.State) is null) return Result.Fail("Choose a valid Indian state (needed for GST place of supply).");
        var s = await SettingsEntityAsync(ct);
        if (i.RazorpayEnabled && (string.IsNullOrWhiteSpace(i.RazorpayKeyId) || (string.IsNullOrWhiteSpace(i.RazorpayKeySecret) && s.RazorpayKeySecretProtected is null)))
            return Result.Fail("Enter the Razorpay key id and key secret to accept online payments.");
        if (i.SmtpEnabled && (string.IsNullOrWhiteSpace(i.SmtpHost) || string.IsNullOrWhiteSpace(i.SmtpFromEmail)))
            return Result.Fail("Enter the SMTP host and from-email to send email.");

        s.LegalName = i.LegalName.Trim(); s.Gstin = i.Gstin?.Trim().ToUpperInvariant(); s.Pan = i.Pan?.Trim().ToUpperInvariant();
        s.AddressLine = i.AddressLine?.Trim(); s.City = i.City?.Trim(); s.State = i.State.Trim(); s.PostalCode = i.PostalCode?.Trim();
        s.BillingEmail = i.BillingEmail?.Trim(); s.Phone = i.Phone?.Trim(); s.InvoicePrefix = i.InvoicePrefix.Trim().ToUpperInvariant();
        s.CreditNotePrefix = i.CreditNotePrefix.Trim().ToUpperInvariant(); s.SacCode = i.SacCode.Trim(); s.GstRate = i.GstRate;
        s.InvoiceDueDays = i.InvoiceDueDays; s.RenewalInvoiceDaysBefore = i.RenewalInvoiceDaysBefore; s.ReminderSchedule = i.ReminderSchedule.Replace(" ", "");
        s.BankDetails = i.BankDetails?.Trim();
        s.RazorpayEnabled = i.RazorpayEnabled; s.RazorpayKeyId = string.IsNullOrWhiteSpace(i.RazorpayKeyId) ? null : i.RazorpayKeyId.Trim();
        if (!string.IsNullOrWhiteSpace(i.RazorpayKeySecret)) s.RazorpayKeySecretProtected = protector.Protect(i.RazorpayKeySecret.Trim());
        if (!string.IsNullOrWhiteSpace(i.RazorpayWebhookSecret)) s.RazorpayWebhookSecretProtected = protector.Protect(i.RazorpayWebhookSecret.Trim());
        s.SmtpEnabled = i.SmtpEnabled; s.SmtpHost = i.SmtpHost?.Trim(); s.SmtpPort = i.SmtpPort; s.SmtpUseSsl = i.SmtpUseSsl;
        s.SmtpUsername = i.SmtpUsername?.Trim(); s.SmtpFromEmail = i.SmtpFromEmail?.Trim(); s.SmtpFromName = i.SmtpFromName.Trim();
        if (!string.IsNullOrWhiteSpace(i.SmtpPassword)) s.SmtpPasswordProtected = protector.Protect(i.SmtpPassword);
        if (i.SmsEnabled && (string.IsNullOrWhiteSpace(i.Msg91TemplateId) || (string.IsNullOrWhiteSpace(i.Msg91AuthKey) && s.Msg91AuthKeyProtected is null)))
            return Result.Fail("Enter the MSG91 auth key and DLT template id to send SMS codes.");
        s.GoogleClientId = string.IsNullOrWhiteSpace(i.GoogleClientId) ? null : i.GoogleClientId.Trim();
        if (!string.IsNullOrWhiteSpace(i.GoogleClientSecret)) s.GoogleClientSecretProtected = protector.Protect(i.GoogleClientSecret.Trim());
        s.MicrosoftClientId = string.IsNullOrWhiteSpace(i.MicrosoftClientId) ? null : i.MicrosoftClientId.Trim();
        if (!string.IsNullOrWhiteSpace(i.MicrosoftClientSecret)) s.MicrosoftClientSecretProtected = protector.Protect(i.MicrosoftClientSecret.Trim());
        s.SmsEnabled = i.SmsEnabled; s.Msg91TemplateId = i.Msg91TemplateId?.Trim(); s.Msg91OtpVariable = i.Msg91OtpVariable.Trim();
        if (!string.IsNullOrWhiteSpace(i.Msg91AuthKey)) s.Msg91AuthKeyProtected = protector.Protect(i.Msg91AuthKey.Trim());
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ numbering

    /// <summary>Next number in a per-financial-year series, e.g. CNX/26-27/00001 (≤ 16 characters as GST requires).</summary>
    private async Task<string> NextNumberAsync(string type, string prefix, DateOnly date, CancellationToken ct)
    {
        var fy = date.Month >= 4 ? date.Year : date.Year - 1;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var seq = await db.DocumentSequences.FirstOrDefaultAsync(s => s.DocumentType == type && s.FinancialYear == fy, ct);
            if (seq is null) { seq = new DocumentSequence { DocumentType = type, FinancialYear = fy }; db.DocumentSequences.Add(seq); }
            var number = seq.NextNumber;
            seq.NextNumber++;
            try
            {
                await db.SaveChangesAsync(ct);
                return $"{prefix}/{fy % 100:00}-{(fy + 1) % 100:00}/{number:00000}";
            }
            catch (DbUpdateException)
            {
                // Another document took the number concurrently: forget our copy, reload and try again.
                ((DbContext)db).Entry(seq).State = EntityState.Detached;
            }
        }
        throw new InvalidOperationException("Could not allocate a document number. Please retry.");
    }

    // ------------------------------------------------------------------ invoices

    private async Task<Invoice> NewInvoiceAsync(Organization org, InvoicePurpose purpose, IReadOnlyList<InvoiceLine> lines, string? notes, CancellationToken ct)
    {
        var s = await SettingsEntityAsync(ct);
        var today = clock.Today;
        var supplierCode = GstStates.CodeFor(s.Gstin, s.State) ?? "27";
        var customerCode = GstStates.CodeFor(org.Gstin, org.State) ?? supplierCode; // unknown customer location → supplier's state
        var taxable = lines.Sum(l => l.Amount);
        var gst = GstCalculator.Split(taxable, s.GstRate, supplierCode, customerCode);
        var number = await NextNumberAsync("INV", s.InvoicePrefix, today, ct);
        return new Invoice
        {
            Number = number, OrganizationId = org.Id, Purpose = purpose, InvoiceDate = today, DueDate = today.AddDays(s.InvoiceDueDays),
            BillToName = org.LegalName ?? org.Name, BillToGstin = org.Gstin,
            BillToAddress = string.Join(", ", new[] { org.AddressLine, org.City, org.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x))),
            BillToState = org.State, BillToStateCode = GstStates.CodeFor(org.Gstin, org.State), SupplierStateCode = supplierCode,
            PlaceOfSupply = $"{GstStates.StateFor(customerCode) ?? org.State} ({customerCode})",
            Subtotal = taxable, CgstRate = gst.CgstRate, CgstAmount = gst.Cgst, SgstRate = gst.SgstRate, SgstAmount = gst.Sgst,
            IgstRate = gst.IgstRate, IgstAmount = gst.Igst, Total = gst.Total, Notes = notes, Lines = lines.ToList()
        };
    }

    public async Task<Result<int>> CreateSubscriptionInvoiceAsync(int organizationId, InvoicePurpose purpose, int planId, BillingCycle cycle, decimal? unitPrice, DateOnly periodStart, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null) return Result<int>.Fail("Organization not found.");
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result<int>.Fail("Plan not found.");
        var price = unitPrice ?? (cycle == BillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice);
        if (price <= 0) return Result<int>.Fail("This plan has no price. Set an agreed price (custom plans are priced per customer).");
        var periodEnd = (cycle == BillingCycle.Yearly ? periodStart.AddYears(1) : periodStart.AddMonths(1)).AddDays(-1);
        if (await db.Invoices.AnyAsync(i => i.OrganizationId == organizationId && i.PeriodStart == periodStart && i.PlanId == planId && i.Status != InvoiceStatus.Cancelled, ct))
            return Result<int>.Fail("An invoice for this plan and period already exists.");

        var seats = plan.IsPerEmployeePricing
            ? Math.Max(1, await db.Employees.IgnoreQueryFilters().CountAsync(e => e.OrganizationId == organizationId && !e.IsDeleted && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive, ct))
            : 1;
        var s = await SettingsEntityAsync(ct);
        var line = new InvoiceLine
        {
            Description = $"{plan.Name} plan — {cycle} subscription, {periodStart:dd MMM yyyy} to {periodEnd:dd MMM yyyy}" + (plan.IsPerEmployeePricing ? $" ({seats} employee(s))" : ""),
            SacCode = s.SacCode, Quantity = seats, UnitPrice = price, Amount = Math.Round(seats * price, 2)
        };
        var invoice = await NewInvoiceAsync(org, purpose, [line], null, ct);
        invoice.PlanId = plan.Id; invoice.BillingCycle = cycle; invoice.Seats = seats; invoice.PeriodStart = periodStart; invoice.PeriodEnd = periodEnd;
        db.Invoices.Add(invoice);
        audit.Add("Invoice.Create", nameof(Invoice), invoice.Number, $"{purpose} {plan.Name} ₹{invoice.Total:N2}", organizationId);
        await db.SaveChangesAsync(ct);
        await NotifyInvoiceAsync(invoice, org, ct);
        return Result<int>.Ok(invoice.Id);
    }

    public async Task<Result<int>> CreateManualInvoiceAsync(int organizationId, ManualInvoiceInput input, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null) return Result<int>.Fail("Organization not found.");
        var s = await SettingsEntityAsync(ct);
        var line = new InvoiceLine { Description = input.Description.Trim(), SacCode = s.SacCode, Quantity = input.Quantity, UnitPrice = input.UnitPrice, Amount = Math.Round(input.Quantity * input.UnitPrice, 2) };
        var invoice = await NewInvoiceAsync(org, InvoicePurpose.Manual, [line], input.Notes?.Trim(), ct);
        db.Invoices.Add(invoice);
        audit.Add("Invoice.Create", nameof(Invoice), invoice.Number, $"Manual ₹{invoice.Total:N2}", organizationId);
        await db.SaveChangesAsync(ct);
        await NotifyInvoiceAsync(invoice, org, ct);
        return Result<int>.Ok(invoice.Id);
    }

    public async Task<Result> CancelInvoiceAsync(int invoiceId, string reason, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for cancelling.");
        var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv is null) return Result.Fail("Invoice not found.");
        if (inv.Status == InvoiceStatus.Cancelled) return Result.Fail("The invoice is already cancelled.");
        if (inv.AmountPaid > 0 || inv.AmountCredited > 0) return Result.Fail("This invoice has payments or credits. Issue a credit note instead.");
        inv.Status = InvoiceStatus.Cancelled;
        inv.CancelReason = reason.Trim();
        foreach (var p in await db.Payments.Where(p => p.InvoiceId == invoiceId && p.Status == PaymentStatus.Created).ToListAsync(ct))
        { p.Status = PaymentStatus.Failed; p.FailureReason = "Invoice cancelled"; }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<PagedResult<InvoiceRow>> InvoicesAsync(InvoiceQuery q, CancellationToken ct = default)
    {
        const int size = 25;
        var page = Math.Max(1, q.Page);
        // Tenants only ever see their own organization's invoices.
        var orgFilter = context.IsPlatform ? q.OrganizationId : context.OrganizationId ?? throw new TenantIsolationException("No organization.");
        var today = clock.Today;
        var query = db.Invoices.AsNoTracking();
        if (orgFilter is { } o) query = query.Where(i => i.OrganizationId == o);
        if (q.Status is { } s) query = query.Where(i => i.Status == s);
        if (q.OverdueOnly) query = query.Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid) && i.DueDate < today);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(i => i.Number.Contains(q.Search) || i.BillToName.Contains(q.Search));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.Id).Skip((page - 1) * size).Take(size)
            .Select(i => new { i.Id, i.Number, i.OrganizationId, Org = i.Organization.Name, i.Purpose, i.InvoiceDate, i.DueDate, i.Total, i.AmountPaid, i.AmountCredited, i.Status })
            .ToListAsync(ct);
        return new PagedResult<InvoiceRow>(rows.Select(r =>
        {
            var balance = Math.Max(0, r.Total - r.AmountPaid - r.AmountCredited);
            return new InvoiceRow(r.Id, r.Number, r.OrganizationId, r.Org, r.Purpose, r.InvoiceDate, r.DueDate, r.Total, balance, r.Status,
                balance > 0 && r.Status != InvoiceStatus.Cancelled && r.DueDate < today);
        }).ToList(), total, page, size);
    }

    public async Task<Invoice?> GetInvoiceAsync(int invoiceId, int? organizationId, CancellationToken ct = default)
    {
        if (!context.IsPlatform && organizationId != context.OrganizationId) throw new TenantIsolationException("Invoice access outside the organization.");
        var q = db.Invoices.AsNoTracking().Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Organization).Where(i => i.Id == invoiceId);
        if (organizationId is int org) q = q.Where(i => i.OrganizationId == org);
        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CreditNote>> CreditNotesForAsync(int invoiceId, CancellationToken ct = default) =>
        await db.CreditNotes.AsNoTracking().Where(c => c.InvoiceId == invoiceId && (context.IsPlatform || c.OrganizationId == context.OrganizationId)).OrderBy(c => c.Id).ToListAsync(ct);

    // ------------------------------------------------------------------ payments

    private async Task ApplyPaymentAsync(Invoice inv, decimal amount, CancellationToken ct)
    {
        inv.AmountPaid += amount;
        inv.Status = inv.Balance == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        await db.SaveChangesAsync(ct);
        if (inv.Status == InvoiceStatus.Paid && !inv.Fulfilled) await FulfilAsync(inv, ct);
    }

    /// <summary>Applies the subscription effect of a fully paid invoice exactly once.</summary>
    private async Task FulfilAsync(Invoice inv, CancellationToken ct)
    {
        // Runs in the caller's scope: a tenant may only ever reach here for its own invoice (verified payment),
        // and the subscription change touches only that organization's records.
        Result result = Result.Ok();
        if (inv.Purpose == InvoicePurpose.NewSubscription && inv.PlanId is int planId && inv.BillingCycle is { } cycle)
            result = await subscriptions.ActivateAsync(inv.OrganizationId, new ActivateSubscriptionInput
            {
                PlanId = planId, BillingCycle = cycle, Price = inv.Lines.First().UnitPrice, StartDate = inv.PeriodStart, Notes = $"Paid invoice {inv.Number}"
            }, ct);
        else if (inv.Purpose == InvoicePurpose.Renewal)
            result = await subscriptions.RenewAsync(inv.OrganizationId, $"Paid invoice {inv.Number}", ct);
        inv.Fulfilled = result.Succeeded;
        if (!result.Succeeded) audit.Add("Invoice.FulfilFailed", nameof(Invoice), inv.Number, result.Error, inv.OrganizationId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result> RecordOfflinePaymentAsync(int invoiceId, OfflinePaymentInput input, CancellationToken ct = default)
    {
        EnsurePlatform();
        var inv = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv is null) return Result.Fail("Invoice not found.");
        if (inv.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid) return Result.Fail($"The invoice is {inv.Status.ToString().ToLowerInvariant()}.");
        if (input.Method == PaymentMethod.Razorpay) return Result.Fail("Razorpay payments are recorded automatically.");
        if (input.Amount > inv.Balance) return Result.Fail($"Amount exceeds the balance of ₹{inv.Balance:N2}.");
        if (input.ReceivedOn > clock.Today) return Result.Fail("The receipt date cannot be in the future.");
        db.Payments.Add(new Payment
        {
            InvoiceId = inv.Id, OrganizationId = inv.OrganizationId, Amount = input.Amount, Method = input.Method, Status = PaymentStatus.Captured,
            Reference = input.Reference.Trim(), PaidAtUtc = input.ReceivedOn.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), Notes = input.Notes?.Trim()
        });
        audit.Add("Payment.Record", nameof(Invoice), inv.Number, $"₹{input.Amount:N2} {input.Method} {input.Reference}", inv.OrganizationId);
        await ApplyPaymentAsync(inv, input.Amount, ct);
        return Result.Ok();
    }

    public async Task<Result<CheckoutData>> StartOnlinePaymentAsync(int invoiceId, int? organizationId, CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is not { RazorpayEnabled: true, RazorpayKeyId: not null }) return Result<CheckoutData>.Fail("Online payment is not available. Please pay by bank transfer.");
        var inv = await GetInvoiceAsync(invoiceId, organizationId ?? (context.IsPlatform ? null : context.OrganizationId), ct);
        if (inv is null) return Result<CheckoutData>.Fail("Invoice not found.");
        if (inv.Status is InvoiceStatus.Cancelled or InvoiceStatus.Paid || inv.Balance <= 0) return Result<CheckoutData>.Fail("Nothing is due on this invoice.");

        GatewayOrder order;
        try
        {
            order = await gateway.CreateOrderAsync(inv.Balance, inv.Number, new Dictionary<string, string> { ["invoice"] = inv.Number, ["organization"] = inv.OrganizationId.ToString() }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return Result<CheckoutData>.Fail("The payment gateway is unavailable right now. Please try again shortly.");
        }
        db.Payments.Add(new Payment
        {
            InvoiceId = inv.Id, OrganizationId = inv.OrganizationId, Amount = inv.Balance, Method = PaymentMethod.Razorpay,
            Status = PaymentStatus.Created, GatewayOrderId = order.OrderId
        });
        await db.SaveChangesAsync(ct);
        var ownerEmail = await db.Organizations.Where(o => o.Id == inv.OrganizationId).Select(o => o.Email).FirstAsync(ct);
        return Result<CheckoutData>.Ok(new CheckoutData(s.RazorpayKeyId!, order.OrderId, order.AmountPaise, order.Currency, inv.Number, inv.BillToName, ownerEmail, $"Invoice {inv.Number}"));
    }

    public async Task<Result> ConfirmOnlinePaymentAsync(string orderId, string paymentId, string signature, int? organizationId, CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s?.RazorpayKeySecretProtected is null) return Result.Fail("Online payments are not configured.");
        if (!RazorpaySignature.VerifyPayment(orderId, paymentId, signature, protector.Unprotect(s.RazorpayKeySecretProtected)))
            return Result.Fail("Payment verification failed. If money was deducted it will be reconciled automatically.");
        return await CaptureAsync(orderId, paymentId, organizationId, ct);
    }

    /// <summary>Marks a gateway payment captured and applies it once (callback and webhook may both arrive).</summary>
    private async Task<Result> CaptureAsync(string orderId, string paymentId, int? organizationId, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.GatewayOrderId == orderId && (organizationId == null || p.OrganizationId == organizationId), ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status is PaymentStatus.Captured or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded) return Result.Ok(); // idempotent
        var inv = await db.Invoices.Include(i => i.Lines).FirstAsync(i => i.Id == payment.InvoiceId, ct);
        payment.Status = PaymentStatus.Captured;
        payment.GatewayPaymentId = paymentId;
        payment.PaidAtUtc = clock.UtcNow;
        payment.Amount = Math.Min(payment.Amount, inv.Balance > 0 ? inv.Balance : payment.Amount);
        audit.Add("Payment.Captured", nameof(Invoice), inv.Number, $"₹{payment.Amount:N2} Razorpay {paymentId}", inv.OrganizationId);
        await ApplyPaymentAsync(inv, payment.Amount, ct);
        return Result.Ok();
    }

    public async Task<Result> HandleWebhookAsync(string body, string signature, CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s?.RazorpayWebhookSecretProtected is null) return Result.Fail("Webhook secret not configured.");
        if (!RazorpaySignature.VerifyWebhook(body, signature, protector.Unprotect(s.RazorpayWebhookSecretProtected))) return Result.Fail("Invalid signature.");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var evt = root.GetProperty("event").GetString();
        if (evt is "payment.captured" or "payment.failed" or "order.paid")
        {
            var entity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var paymentId = entity.GetProperty("id").GetString()!;
            var orderId = entity.TryGetProperty("order_id", out var o) ? o.GetString() : null;
            if (orderId is null) return Result.Ok();
            if (evt == "payment.failed")
            {
                var p = await db.Payments.FirstOrDefaultAsync(x => x.GatewayOrderId == orderId && x.Status == PaymentStatus.Created, ct);
                if (p is not null)
                {
                    p.Status = PaymentStatus.Failed;
                    p.GatewayPaymentId = paymentId;
                    p.FailureReason = entity.TryGetProperty("error_description", out var d) ? d.GetString()?[..Math.Min(300, d.GetString()!.Length)] : "Payment failed";
                    await db.SaveChangesAsync(ct);
                }
                return Result.Ok();
            }
            return await CaptureAsync(orderId, paymentId, null, ct);
        }
        return Result.Ok(); // other events are acknowledged and ignored
    }

    public async Task<Result> IssueCreditNoteAsync(int invoiceId, CreditNoteInput input, CancellationToken ct = default)
    {
        EnsurePlatform();
        var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv is null) return Result.Fail("Invoice not found.");
        if (inv.Status == InvoiceStatus.Cancelled) return Result.Fail("The invoice is cancelled.");
        var creditable = inv.Total - inv.AmountCredited;
        if (input.Amount > creditable) return Result.Fail($"At most ₹{creditable:N2} can still be credited on this invoice.");

        Payment? refundFrom = null;
        if (input.Refund)
        {
            refundFrom = await db.Payments.FirstOrDefaultAsync(p => p.Id == input.PaymentId && p.InvoiceId == invoiceId
                && (p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.PartiallyRefunded), ct);
            if (refundFrom is null) return Result.Fail("Choose the payment to refund.");
            if (input.Amount > refundFrom.Amount - refundFrom.RefundedAmount) return Result.Fail($"At most ₹{refundFrom.Amount - refundFrom.RefundedAmount:N2} can be refunded from that payment.");
        }
        else if (input.Amount > inv.Balance)
            return Result.Fail($"Without a refund, the credit can be at most the unpaid balance (₹{inv.Balance:N2}).");

        // Split the credit back into taxable value and tax in the invoice's proportions.
        var ratio = inv.Total == 0 ? 0 : input.Amount / inv.Total;
        var cgst = Math.Round(inv.CgstAmount * ratio, 2);
        var sgst = Math.Round(inv.SgstAmount * ratio, 2);
        var igst = Math.Round(inv.IgstAmount * ratio, 2);
        var s = await SettingsEntityAsync(ct);

        string? refundId = null;
        if (refundFrom is { Method: PaymentMethod.Razorpay, GatewayPaymentId: not null })
        {
            try { refundId = (await gateway.RefundAsync(refundFrom.GatewayPaymentId, input.Amount, $"Credit note for {inv.Number}", ct)).RefundId; }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            { return Result.Fail("The gateway refused the refund. Nothing was changed."); }
        }

        var note = new CreditNote
        {
            Number = await NextNumberAsync("CN", s.CreditNotePrefix, clock.Today, ct), InvoiceId = inv.Id, OrganizationId = inv.OrganizationId, Date = clock.Today,
            Reason = input.Reason.Trim(), TaxableAmount = input.Amount - cgst - sgst - igst, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst, Total = input.Amount,
            RefundAmount = refundFrom is null ? 0 : input.Amount, RefundPaymentId = refundFrom?.Id, GatewayRefundId = refundId
        };
        db.CreditNotes.Add(note);
        // The invoice document itself never changes; the credit note reduces what is owed.
        inv.AmountCredited += input.Amount;
        if (refundFrom is not null)
        {
            // Money went back, so that part of the payment no longer counts towards the invoice.
            refundFrom.RefundedAmount += input.Amount;
            refundFrom.Status = refundFrom.RefundedAmount >= refundFrom.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
            inv.AmountPaid -= input.Amount;
        }
        inv.Status = inv.Balance == 0 ? InvoiceStatus.Paid : inv.AmountPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Issued;
        audit.Add("CreditNote.Issue", nameof(Invoice), inv.Number, $"{note.Number} ₹{input.Amount:N2}{(refundFrom is null ? "" : " refunded")}: {input.Reason}", inv.OrganizationId);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ self-service purchase

    public async Task<Result<int>> CreatePurchaseInvoiceAsync(int planId, BillingCycle cycle, CancellationToken ct = default)
    {
        var orgId = context.OrganizationId ?? throw new TenantIsolationException("No organization.");
        var plan = await db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId && p.IsActive && p.IsPublic && !p.IsCustom, ct);
        if (plan is null) return Result<int>.Fail("Choose an available plan.");
        var current = await db.OrganizationSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.IsCurrent, ct);
        if (current is { Status: SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Paused, IsTrial: false })
            return Result<int>.Fail("You already have a paid subscription. Contact support to change plans mid-term.");
        var open = await db.Invoices.Where(i => i.OrganizationId == orgId && i.Purpose == InvoicePurpose.NewSubscription
            && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);
        if (open is int existing) return Result<int>.Ok(existing); // reuse the pending purchase invoice
        // Paid service starts today (a remaining trial simply converts).
        return await CreateSubscriptionInvoiceAsync(orgId, InvoicePurpose.NewSubscription, plan.Id, cycle, null, clock.Today, ct);
    }

    public async Task<PurchaseOptions> PurchaseOptionsAsync(CancellationToken ct = default)
    {
        var orgId = context.OrganizationId ?? throw new TenantIsolationException("No organization.");
        var current = await db.OrganizationSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.IsCurrent, ct);
        var paid = current is { Status: SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Paused, IsTrial: false };
        var seats = Math.Max(1, await db.Employees.CountAsync(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive, ct));
        var plans = await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive && p.IsPublic && !p.IsCustom && (p.MonthlyPrice > 0 || p.YearlyPrice > 0))
            .OrderBy(p => p.SortOrder).Select(p => new PlanOption(p.Id, p.Name, p.Description, p.MonthlyPrice, p.YearlyPrice, p.IsPerEmployeePricing)).ToListAsync(ct);
        var pending = await db.Invoices.AsNoTracking().Where(i => i.OrganizationId == orgId && i.Purpose == InvoicePurpose.NewSubscription
            && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);
        return new PurchaseOptions(!paid, paid ? "You already have a paid subscription. Renewal invoices are raised automatically before it ends." : null,
            seats, plans, pending, await OnlinePaymentsEnabledAsync(ct));
    }

    public async Task<IReadOnlyList<CoreHr.LookupItem>> OrganizationOptionsAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        return await db.Organizations.AsNoTracking().OrderBy(o => o.Name).Select(o => new CoreHr.LookupItem(o.Id, o.Name)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentRow>> RecentPaymentsAsync(int take, CancellationToken ct = default)
    {
        EnsurePlatform();
        return await db.Payments.AsNoTracking().Where(p => p.Status != PaymentStatus.Created).OrderByDescending(p => p.Id).Take(take)
            .Select(p => new PaymentRow(p.Id, p.InvoiceId, p.Invoice.Number, p.Invoice.BillToName, p.Amount, p.RefundedAmount, p.Method, p.Status,
                p.Reference ?? p.GatewayPaymentId, p.PaidAtUtc)).ToListAsync(ct);
    }

    // ------------------------------------------------------------------ analytics

    public async Task<BillingSummary> SummaryAsync(string granularity, CancellationToken ct = default)
    {
        EnsurePlatform();
        var today = clock.Today;
        var open = await db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .Select(i => new { Balance = i.Total - i.AmountPaid - i.AmountCredited, i.DueDate }).ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().Where(p => p.PaidAtUtc != null && (p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded))
            .Select(p => new { p.Amount, p.RefundedAmount, p.PaidAtUtc }).ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Cancelled).Select(i => new { i.Total, i.InvoiceDate }).ToListAsync(ct);
        var fyStart = new DateOnly(today.Month >= 4 ? today.Year : today.Year - 1, 4, 1);

        string Key(DateOnly d) => granularity switch
        {
            "year" => $"FY {(d.Month >= 4 ? d.Year : d.Year - 1) % 100:00}-{((d.Month >= 4 ? d.Year : d.Year - 1) + 1) % 100:00}",
            "quarter" => $"{d.Year} Q{(d.Month - 1) / 3 + 1}",
            _ => d.ToString("MMM yy")
        };
        var buckets = granularity switch
        {
            "year" => Enumerable.Range(0, 5).Select(i => fyStart.AddYears(i - 4)).ToList(),
            "quarter" => Enumerable.Range(0, 8).Select(i => new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1).AddMonths(3 * (i - 7))).ToList(),
            _ => Enumerable.Range(0, 12).Select(i => new DateOnly(today.Year, today.Month, 1).AddMonths(i - 11)).ToList()
        };
        var revenue = buckets.Select(b => Key(b)).Distinct().Select(label => new RevenuePoint(label,
            payments.Where(p => Key(DateOnly.FromDateTime(p.PaidAtUtc!.Value)) == label).Sum(p => p.Amount - p.RefundedAmount),
            invoices.Where(i => Key(i.InvoiceDate) == label).Sum(i => i.Total))).ToList();

        return new BillingSummary
        {
            Outstanding = open.Sum(o => o.Balance),
            Overdue = open.Where(o => o.DueDate < today).Sum(o => o.Balance),
            OverdueCount = open.Count(o => o.DueDate < today && o.Balance > 0),
            CollectedThisMonth = payments.Where(p => p.PaidAtUtc!.Value.Year == today.Year && p.PaidAtUtc.Value.Month == today.Month).Sum(p => p.Amount - p.RefundedAmount),
            CollectedThisYear = payments.Where(p => DateOnly.FromDateTime(p.PaidAtUtc!.Value) >= fyStart).Sum(p => p.Amount - p.RefundedAmount),
            RefundedThisYear = payments.Where(p => DateOnly.FromDateTime(p.PaidAtUtc!.Value) >= fyStart).Sum(p => p.RefundedAmount),
            Revenue = revenue
        };
    }

    // ------------------------------------------------------------------ email

    private async Task NotifyInvoiceAsync(Invoice inv, Organization org, CancellationToken ct)
    {
        await email.SendAsync(new EmailMessage(org.Email, $"Invoice {inv.Number} — ₹{inv.Total:N2} due {inv.DueDate:dd MMM yyyy}",
            BillingEmails.InvoiceIssued(inv, org.Name), "invoice", org.Id), ct);
    }
}

/// <summary>Plain, inline-styled HTML emails (email clients ignore external CSS).</summary>
public static class BillingEmails
{
    private static string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    private static string Wrap(string title, string body) =>
        $"<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;color:#1e293b\"><h2 style=\"color:#4f46e5\">{E(title)}</h2>{body}" +
        "<p style=\"color:#64748b;font-size:12px;margin-top:24px\">Codenexify HRMS · This is an automated message.</p></div>";

    public static string InvoiceIssued(Invoice inv, string orgName) => Wrap($"Invoice {inv.Number}",
        $"<p>Dear {E(orgName)},</p><p>A new invoice has been issued for your subscription.</p>" +
        $"<p><strong>Amount:</strong> ₹{inv.Total:N2}<br><strong>Due date:</strong> {inv.DueDate:dd MMM yyyy}</p>" +
        "<p>You can view and pay it from <em>Subscription → Billing</em> in your HRMS administration.</p>");

    public static string Reminder(Invoice inv, string orgName, int daysToDue) => Wrap(daysToDue >= 0 ? "Payment reminder" : "Payment overdue",
        $"<p>Dear {E(orgName)},</p><p>Invoice <strong>{E(inv.Number)}</strong> for ₹{inv.Balance:N2} is " +
        (daysToDue > 0 ? $"due in {daysToDue} day(s) ({inv.DueDate:dd MMM yyyy})." : daysToDue == 0 ? "due today." : $"overdue by {-daysToDue} day(s).") +
        "</p><p>Please pay from <em>Subscription → Billing</em> to avoid interruption of service.</p>");

    public static string TrialEnding(string orgName, DateOnly endsOn) => Wrap("Your trial is ending",
        $"<p>Dear {E(orgName)},</p><p>Your free trial ends on <strong>{endsOn:dd MMM yyyy}</strong>.</p><p>Choose a plan under <em>Subscription → Billing</em> to keep using all modules.</p>");
}
