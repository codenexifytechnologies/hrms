using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> b)
    {
        TenantConfig.Base(b, "PayrollAdjustments");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(300);
        b.Ignore(x => x.IsDeduction);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.SettledRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.SettledRunId });
        b.HasIndex(x => new { x.OrganizationId, x.Year, x.Month });
    }
}

internal sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        TenantConfig.Base(b, "ExpenseCategories");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class ExpenseClaimConfiguration : IEntityTypeConfiguration<ExpenseClaim>
{
    public void Configure(EntityTypeBuilder<ExpenseClaim> b)
    {
        TenantConfig.Base(b, "ExpenseClaims");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.ManagerDecisionBy).HasMaxLength(150);
        b.Property(x => x.DecidedBy).HasMaxLength(150);
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.Property(x => x.PaymentReference).HasMaxLength(100);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollAdjustment>().WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.ManagerUserId, x.Status });
    }
}

internal sealed class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
    public void Configure(EntityTypeBuilder<ExpenseItem> b)
    {
        TenantConfig.Base(b, "ExpenseItems");
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.ReceiptKey).HasMaxLength(300);
        b.Property(x => x.ReceiptFileName).HasMaxLength(200);
        b.Property(x => x.ReceiptContentType).HasMaxLength(100);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
