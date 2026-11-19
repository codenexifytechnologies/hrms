using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class PromotionCaseConfiguration : IEntityTypeConfiguration<PromotionCase>
{
    public void Configure(EntityTypeBuilder<PromotionCase> b)
    {
        TenantConfig.Base(b, "PromotionCases");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ProposedByName).HasMaxLength(150);
        b.Property(x => x.Justification).HasMaxLength(4000).IsRequired();
        b.Property(x => x.DecidedByName).HasMaxLength(150);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.Property(x => x.ApplyError).HasMaxLength(500);
        b.Property(x => x.CurrentAnnualGross).HasPrecision(18, 2);
        b.Property(x => x.ProposedAnnualGross).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ProposedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.FromDesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.ToDesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PerformanceReview>().WithMany().HasForeignKey(x => x.PerformanceReviewId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.EffectiveOn });
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId });
    }
}

internal sealed class ImprovementPlanConfiguration : IEntityTypeConfiguration<ImprovementPlan>
{
    public void Configure(EntityTypeBuilder<ImprovementPlan> b)
    {
        TenantConfig.Base(b, "ImprovementPlans");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.CreatedByName).HasMaxLength(150);
        b.Property(x => x.Concerns).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Support).HasMaxLength(2000);
        b.Property(x => x.EmployeeComment).HasMaxLength(2000);
        b.Property(x => x.OutcomeNote).HasMaxLength(2000);
        b.Property(x => x.ClosedByName).HasMaxLength(150);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.Status });
    }
}

internal sealed class PipObjectiveConfiguration : IEntityTypeConfiguration<PipObjective>
{
    public void Configure(EntityTypeBuilder<PipObjective> b)
    {
        TenantConfig.Base(b, "PipObjectives");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Measure).HasMaxLength(1000).IsRequired();
        b.HasOne<ImprovementPlan>().WithMany(p => p.Objectives).HasForeignKey(x => x.ImprovementPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PipCheckInConfiguration : IEntityTypeConfiguration<PipCheckIn>
{
    public void Configure(EntityTypeBuilder<PipCheckIn> b)
    {
        TenantConfig.Base(b, "PipCheckIns");
        b.Property(x => x.Progress).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ByName).HasMaxLength(150);
        b.HasOne<ImprovementPlan>().WithMany(p => p.CheckIns).HasForeignKey(x => x.ImprovementPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}
