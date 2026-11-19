using Codenexify.HRMS.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> b)
    {
        b.ToTable("PlatformSettings");
        PlatformConfig.Audit(b);
        b.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Gstin).HasMaxLength(15);
        b.Property(x => x.Pan).HasMaxLength(10);
        b.Property(x => x.AddressLine).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100).IsRequired();
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.BillingEmail).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.InvoicePrefix).HasMaxLength(6).IsRequired();
        b.Property(x => x.CreditNotePrefix).HasMaxLength(6).IsRequired();
        b.Property(x => x.SacCode).HasMaxLength(8).IsRequired();
        b.Property(x => x.GstRate).HasPrecision(5, 2);
        b.Property(x => x.ReminderSchedule).HasMaxLength(60).IsRequired();
        b.Property(x => x.BankDetails).HasMaxLength(500);
        b.Property(x => x.RazorpayKeyId).HasMaxLength(100);
        b.Property(x => x.RazorpayKeySecretProtected).HasMaxLength(1000);
        b.Property(x => x.RazorpayWebhookSecretProtected).HasMaxLength(1000);
        b.Property(x => x.SmtpHost).HasMaxLength(200);
        b.Property(x => x.SmtpUsername).HasMaxLength(200);
        b.Property(x => x.SmtpPasswordProtected).HasMaxLength(1000);
        b.Property(x => x.SmtpFromEmail).HasMaxLength(200);
        b.Property(x => x.SmtpFromName).HasMaxLength(100);
        b.Property(x => x.SupportEmail).HasMaxLength(200);
        b.Property(x => x.GrievanceOfficerName).HasMaxLength(150);
        b.Property(x => x.GrievanceOfficerEmail).HasMaxLength(200);
        b.Property(x => x.LegalJurisdictionCity).HasMaxLength(100);
        b.Property(x => x.MaintenanceMessage).HasMaxLength(500);
    }
}

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> b)
    {
        b.ToTable("DocumentSequences");
        PlatformConfig.Audit(b);
        b.Property(x => x.DocumentType).HasMaxLength(20).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.DocumentType, x.FinancialYear }).IsUnique();
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        PlatformConfig.Audit(b);
        b.Property(x => x.Number).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Platform.SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.BillToName).HasMaxLength(200).IsRequired();
        b.Property(x => x.BillToGstin).HasMaxLength(15);
        b.Property(x => x.BillToAddress).HasMaxLength(500);
        b.Property(x => x.BillToState).HasMaxLength(100);
        b.Property(x => x.BillToStateCode).HasMaxLength(2);
        b.Property(x => x.SupplierStateCode).HasMaxLength(2);
        b.Property(x => x.PlaceOfSupply).HasMaxLength(120);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.CgstRate).HasPrecision(5, 2);
        b.Property(x => x.SgstRate).HasPrecision(5, 2);
        b.Property(x => x.IgstRate).HasPrecision(5, 2);
        b.Ignore(x => x.Balance);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.Status, x.DueDate });
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(p => p.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        // Follows its organization's visibility (organizations are soft-deletable).
        b.HasQueryFilter(x => !x.Organization.IsDeleted);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("InvoiceLines");
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.SacCode).HasMaxLength(8).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(12, 2);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        PlatformConfig.Audit(b);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.GatewayOrderId).HasMaxLength(60);
        b.Property(x => x.GatewayPaymentId).HasMaxLength(60);
        b.Property(x => x.FailureReason).HasMaxLength(300);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.GatewayOrderId);
        b.HasIndex(x => x.GatewayPaymentId).IsUnique().HasFilter("[GatewayPaymentId] IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.PaidAtUtc });
        b.HasOne<Domain.Platform.Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.Invoice.Organization.IsDeleted);
    }
}

internal sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> b)
    {
        b.ToTable("CreditNotes");
        PlatformConfig.Audit(b);
        b.Property(x => x.Number).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.GatewayRefundId).HasMaxLength(60);
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Platform.Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.RefundPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.Invoice.Organization.IsDeleted);
    }
}

internal sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> b)
    {
        b.ToTable("EmailLogs");
        b.Property(x => x.To).HasMaxLength(200).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        b.Property(x => x.Category).HasMaxLength(50).IsRequired();
        b.Property(x => x.Error).HasMaxLength(1000);
        b.HasIndex(x => x.CreatedAtUtc);
    }
}
