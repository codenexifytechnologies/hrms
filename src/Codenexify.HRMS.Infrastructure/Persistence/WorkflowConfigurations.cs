using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class WorkflowRuleConfiguration : IEntityTypeConfiguration<WorkflowRule>
{
    public void Configure(EntityTypeBuilder<WorkflowRule> b)
    {
        TenantConfig.Base(b, "WorkflowRules");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkflowActionConfiguration : IEntityTypeConfiguration<WorkflowAction>
{
    public void Configure(EntityTypeBuilder<WorkflowAction> b)
    {
        TenantConfig.Base(b, "WorkflowActions");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Recipient).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Message).HasMaxLength(1000);
        b.HasOne<WorkflowRule>().WithMany(r => r.Actions).HasForeignKey(x => x.WorkflowRuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> b)
    {
        TenantConfig.Base(b, "WorkflowRuns");
        b.Property(x => x.SubjectKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1000);
        b.HasOne<WorkflowRule>().WithMany().HasForeignKey(x => x.WorkflowRuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.WorkflowRuleId, x.SubjectKey }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> b)
    {
        TenantConfig.Base(b, "WorkTasks");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Details).HasMaxLength(1000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.AssigneeUserId, x.IsDone });
    }
}

internal sealed class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> b)
    {
        TenantConfig.Base(b, "SavedReports");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Definition).HasMaxLength(8000).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
