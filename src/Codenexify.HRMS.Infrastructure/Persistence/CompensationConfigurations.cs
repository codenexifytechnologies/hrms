using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class CompensationCycleConfiguration : IEntityTypeConfiguration<CompensationCycle>
{
    public void Configure(EntityTypeBuilder<CompensationCycle> b)
    {
        TenantConfig.Base(b, "CompensationCycles");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.BudgetPercent).HasPrecision(6, 2);
        b.Property(x => x.ApprovedByName).HasMaxLength(150);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CompensationGuidelineConfiguration : IEntityTypeConfiguration<CompensationGuideline>
{
    public void Configure(EntityTypeBuilder<CompensationGuideline> b)
    {
        TenantConfig.Base(b, "CompensationGuidelines");
        b.Property(x => x.MinPercent).HasPrecision(6, 2);
        b.Property(x => x.MaxPercent).HasPrecision(6, 2);
        b.HasOne<CompensationCycle>().WithMany(c => c.Guidelines).HasForeignKey(x => x.CompensationCycleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CompensationCycleId, x.Rating }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class CompensationLineConfiguration : IEntityTypeConfiguration<CompensationLine>
{
    public void Configure(EntityTypeBuilder<CompensationLine> b)
    {
        TenantConfig.Base(b, "CompensationLines");
        b.Property(x => x.CurrentAnnualGross).HasPrecision(18, 2);
        b.Property(x => x.ProposedPercent).HasPrecision(6, 2);
        b.Property(x => x.ProposedAnnualGross).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.UpdatedByName).HasMaxLength(150);
        b.Property(x => x.ApplyError).HasMaxLength(500);
        b.HasOne<CompensationCycle>().WithMany().HasForeignKey(x => x.CompensationCycleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.PlannerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompensationCycleId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.OrganizationId, x.PlannerEmployeeId });
    }
}

internal sealed class CalibrationTargetConfiguration : IEntityTypeConfiguration<CalibrationTarget>
{
    public void Configure(EntityTypeBuilder<CalibrationTarget> b)
    {
        TenantConfig.Base(b, "CalibrationTargets");
        b.Property(x => x.Percent).HasPrecision(5, 2);
        b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ReviewCycleId, x.Rating }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
