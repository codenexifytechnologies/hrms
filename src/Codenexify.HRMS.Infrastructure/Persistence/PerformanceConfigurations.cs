using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> b)
    {
        TenantConfig.Base(b, "Goals");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Progress).HasPrecision(5, 1);
        b.Property(x => x.CreatedByName).HasMaxLength(150);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Goal>().WithMany().HasForeignKey(x => x.ParentGoalId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Level, x.DueOn });
        b.HasIndex(x => new { x.OrganizationId, x.OwnerEmployeeId });
    }
}

internal sealed class KeyResultConfiguration : IEntityTypeConfiguration<KeyResult>
{
    public void Configure(EntityTypeBuilder<KeyResult> b)
    {
        TenantConfig.Base(b, "KeyResults");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Unit).HasMaxLength(20);
        b.Property(x => x.StartValue).HasPrecision(18, 2);
        b.Property(x => x.TargetValue).HasPrecision(18, 2);
        b.Property(x => x.CurrentValue).HasPrecision(18, 2);
        b.HasOne<Goal>().WithMany(g => g.KeyResults).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GoalCheckInConfiguration : IEntityTypeConfiguration<GoalCheckIn>
{
    public void Configure(EntityTypeBuilder<GoalCheckIn> b)
    {
        TenantConfig.Base(b, "GoalCheckIns");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Progress).HasPrecision(5, 1);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.ByName).HasMaxLength(150);
        b.HasOne<Goal>().WithMany(g => g.CheckIns).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.GoalId, x.AtUtc });
    }
}

internal sealed class ReviewCycleConfiguration : IEntityTypeConfiguration<ReviewCycle>
{
    public void Configure(EntityTypeBuilder<ReviewCycle> b)
    {
        TenantConfig.Base(b, "ReviewCycles");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RatingLabels).HasMaxLength(400).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReviewQuestionConfiguration : IEntityTypeConfiguration<ReviewQuestion>
{
    public void Configure(EntityTypeBuilder<ReviewQuestion> b)
    {
        TenantConfig.Base(b, "ReviewQuestions");
        b.Property(x => x.Text).HasMaxLength(500).IsRequired();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
        b.HasOne<ReviewCycle>().WithMany(c => c.Questions).HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PerformanceReviewConfiguration : IEntityTypeConfiguration<PerformanceReview>
{
    public void Configure(EntityTypeBuilder<PerformanceReview> b)
    {
        TenantConfig.Base(b, "PerformanceReviews");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ReviewedByName).HasMaxLength(150);
        b.Property(x => x.AdjustmentNote).HasMaxLength(500);
        b.Property(x => x.EmployeeComment).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Cycle).WithMany().HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ReviewCycleId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.OrganizationId, x.ReviewerEmployeeId, x.Status });
    }
}

internal sealed class ReviewAnswerConfiguration : IEntityTypeConfiguration<ReviewAnswer>
{
    public void Configure(EntityTypeBuilder<ReviewAnswer> b)
    {
        TenantConfig.Base(b, "ReviewAnswers");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Text).HasMaxLength(4000);
        b.HasOne<PerformanceReview>().WithMany(r => r.Answers).HasForeignKey(x => x.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ReviewQuestion>().WithMany().HasForeignKey(x => x.ReviewQuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PerformanceReviewId, x.ReviewQuestionId, x.Role }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
