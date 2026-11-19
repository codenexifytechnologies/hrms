using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class HeadcountPlanConfiguration : IEntityTypeConfiguration<HeadcountPlan>
{
    public void Configure(EntityTypeBuilder<HeadcountPlan> b)
    {
        TenantConfig.Base(b, "HeadcountPlans");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HeadcountPositionConfiguration : IEntityTypeConfiguration<HeadcountPosition>
{
    public void Configure(EntityTypeBuilder<HeadcountPosition> b)
    {
        TenantConfig.Base(b, "HeadcountPositions");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AnnualBudgetPerHire).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne(x => x.Plan).WithMany(p => p.Positions).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HeadcountHireConfiguration : IEntityTypeConfiguration<HeadcountHire>
{
    public void Configure(EntityTypeBuilder<HeadcountHire> b)
    {
        TenantConfig.Base(b, "HeadcountHires");
        b.HasOne(x => x.Position).WithMany(p => p.Hires).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<HeadcountPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PlanId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class PreboardingSettingsConfiguration : IEntityTypeConfiguration<PreboardingSettings>
{
    public void Configure(EntityTypeBuilder<PreboardingSettings> b)
    {
        TenantConfig.Base(b, "PreboardingSettings");
        b.Property(x => x.WelcomeMessage).HasMaxLength(4000);
        b.Property(x => x.FirstDayInfo).HasMaxLength(4000);
        b.Property(x => x.ContactName).HasMaxLength(150);
        b.Property(x => x.ContactEmail).HasMaxLength(200);
        b.Property(x => x.ContactPhone).HasMaxLength(20);
        b.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class PreboardingItemConfiguration : IEntityTypeConfiguration<PreboardingItem>
{
    public void Configure(EntityTypeBuilder<PreboardingItem> b)
    {
        TenantConfig.Base(b, "PreboardingItems");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DocumentCategory).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class PreboardingInviteConfiguration : IEntityTypeConfiguration<PreboardingInvite>
{
    public void Configure(EntityTypeBuilder<PreboardingInvite> b)
    {
        TenantConfig.Base(b, "PreboardingInvites");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId });
    }
}

internal sealed class PreboardingResponseConfiguration : IEntityTypeConfiguration<PreboardingResponse>
{
    public void Configure(EntityTypeBuilder<PreboardingResponse> b)
    {
        TenantConfig.Base(b, "PreboardingResponses");
        b.HasOne<PreboardingInvite>().WithMany(i => i.Responses).HasForeignKey(x => x.InviteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.EmployeeDocumentId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.InviteId, x.ItemId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
