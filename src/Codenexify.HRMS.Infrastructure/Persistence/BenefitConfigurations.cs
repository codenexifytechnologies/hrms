using Codenexify.HRMS.Domain.Benefits;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class BenefitPlanConfiguration : IEntityTypeConfiguration<BenefitPlan>
{
    public void Configure(EntityTypeBuilder<BenefitPlan> b)
    {
        TenantConfig.Base(b, "BenefitPlans");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EnrollmentMode).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Provider).HasMaxLength(150);
        b.Property(x => x.PolicyNumber).HasMaxLength(100);
        b.Property(x => x.EligibleEmploymentTypes).HasMaxLength(200);
        b.Property(x => x.DocumentStorageKey).HasMaxLength(300);
        b.Property(x => x.DocumentFileName).HasMaxLength(200);
        b.Property(x => x.CoverageAmount).HasPrecision(18, 2);
        b.Property(x => x.EmployerCostPerMonth).HasPrecision(18, 2);
        b.Property(x => x.EmployeeContributionPerMonth).HasPrecision(18, 2);
        b.Property(x => x.DependentContributionPerMonth).HasPrecision(18, 2);
        b.Property(x => x.AllowancePerMonth).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.IsActive });
    }
}

internal sealed class BenefitEnrollmentConfiguration : IEntityTypeConfiguration<BenefitEnrollment>
{
    public void Configure(EntityTypeBuilder<BenefitEnrollment> b)
    {
        TenantConfig.Base(b, "BenefitEnrollments");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EndReason).HasMaxLength(300);
        b.Property(x => x.EnrolledBy).HasMaxLength(150);
        b.HasOne(x => x.Plan).WithMany(p => p.Enrollments).HasForeignKey(x => x.BenefitPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.Status });
        // At most one ongoing membership of a plan per employee.
        b.HasIndex(x => new { x.BenefitPlanId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] = 'Active'");
    }
}

internal sealed class BenefitDependentConfiguration : IEntityTypeConfiguration<BenefitDependent>
{
    public void Configure(EntityTypeBuilder<BenefitDependent> b)
    {
        TenantConfig.Base(b, "BenefitDependents");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Relation).HasConversion<string>().HasMaxLength(20);
        b.HasOne<BenefitEnrollment>().WithMany(e => e.Dependents).HasForeignKey(x => x.BenefitEnrollmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
