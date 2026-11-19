using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;

namespace Codenexify.HRMS.Domain.Billing;

public enum InvoiceStatus { Issued, PartiallyPaid, Paid, Cancelled }
public enum InvoicePurpose { NewSubscription, Renewal, Manual }
public enum PaymentMethod { Razorpay, BankTransfer, Upi, Cheque, Cash, Other }
public enum PaymentStatus { Created, Captured, Failed, Refunded, PartiallyRefunded }

/// <summary>Platform owner's billing, payment-gateway and email configuration (single row).</summary>
public class PlatformSetting : BaseEntity, IAuditable
{
    // Supplier (Codenexify) details printed on invoices
    public string LegalName { get; set; } = "Codenexify Technologies Private Limited";
    public string? Gstin { get; set; }
    public string? Pan { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string State { get; set; } = "Maharashtra";
    public string? PostalCode { get; set; }
    public string? BillingEmail { get; set; }
    public string? Phone { get; set; }
    public string InvoicePrefix { get; set; } = "CNX";
    public string CreditNotePrefix { get; set; } = "CNXCN";
    public string SacCode { get; set; } = "998314";
    public decimal GstRate { get; set; } = 18;
    public int InvoiceDueDays { get; set; } = 7;
    public int RenewalInvoiceDaysBefore { get; set; } = 7;
    /// <summary>Comma separated days before the due date to send reminders (negative = after due), e.g. "3,1,0,-3,-7".</summary>
    public string ReminderSchedule { get; set; } = "3,1,0,-3,-7";
    public string? BankDetails { get; set; }

    // Razorpay (secrets protected with Data Protection)
    public bool RazorpayEnabled { get; set; }
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecretProtected { get; set; }
    public string? RazorpayWebhookSecretProtected { get; set; }

    // SMTP
    public bool SmtpEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPasswordProtected { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string SmtpFromName { get; set; } = "Codenexify HRMS";

    // Single sign-on (OpenID Connect app registrations owned by Codenexify; organizations switch them on)
    public string? GoogleClientId { get; set; }
    public string? GoogleClientSecretProtected { get; set; }
    public string? MicrosoftClientId { get; set; }
    public string? MicrosoftClientSecretProtected { get; set; }

    // SMS one-time passwords (MSG91 Flow API with a DLT-approved template)
    public bool SmsEnabled { get; set; }
    public string? Msg91AuthKeyProtected { get; set; }
    public string? Msg91TemplateId { get; set; }
    public string Msg91OtpVariable { get; set; } = "otp";

    // Support & system
    /// <summary>Inbox that receives new-ticket notifications.</summary>
    public string? SupportEmail { get; set; }

    // Legal pages (filled into the privacy policy, terms and contact page)
    /// <summary>Grievance Officer named in the privacy policy (IT Rules 2011 / DPDP Act 2023).</summary>
    public string? GrievanceOfficerName { get; set; }
    public string? GrievanceOfficerEmail { get; set; }
    /// <summary>City whose courts have jurisdiction under the terms of service.</summary>
    public string? LegalJurisdictionCity { get; set; }
    public bool MaintenanceEnabled { get; set; }
    public string? MaintenanceMessage { get; set; }
    /// <summary>Expected end, shown to users (informational; maintenance ends when switched off).</summary>
    public DateTime? MaintenanceUntilUtc { get; set; }
}

/// <summary>Sequence per document type and financial year (GST requires unique numbering per FY).</summary>
public class DocumentSequence : BaseEntity
{
    public string DocumentType { get; set; } = string.Empty;
    public int FinancialYear { get; set; }
    public int NextNumber { get; set; } = 1;
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Tax invoice raised by the platform owner to a client organization.</summary>
public class Invoice : BaseEntity, IAuditable
{
    public string Number { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public InvoicePurpose Purpose { get; set; }
    public int? PlanId { get; set; }
    public BillingCycle? BillingCycle { get; set; }
    public int? Seats { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;
    /// <summary>Set once the subscription effect of this invoice (activation / renewal) has been applied.</summary>
    public bool Fulfilled { get; set; }

    // Snapshots (an invoice never changes when the customer edits its profile later)
    public string BillToName { get; set; } = string.Empty;
    public string? BillToGstin { get; set; }
    public string? BillToAddress { get; set; }
    public string? BillToState { get; set; }
    public string? BillToStateCode { get; set; }
    public string SupplierStateCode { get; set; } = string.Empty;
    public string PlaceOfSupply { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }
    public decimal CgstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstRate { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstRate { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountCredited { get; set; }
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? LastReminderAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public decimal Balance => Math.Max(0, Total - AmountPaid - AmountCredited);
}

public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SacCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class Payment : BaseEntity, IAuditable
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int OrganizationId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? Reference { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? FailureReason { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? Notes { get; set; }
}

public class CreditNote : BaseEntity, IAuditable
{
    public string Number { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int OrganizationId { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal Total { get; set; }
    /// <summary>Money returned to the customer for this credit (0 = balance adjustment only).</summary>
    public decimal RefundAmount { get; set; }
    public int? RefundPaymentId { get; set; }
    public string? GatewayRefundId { get; set; }
}

/// <summary>Every email the platform attempted to send.</summary>
public class EmailLog
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? OrganizationId { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}
