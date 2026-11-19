using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class OnboardingTemplateConfiguration : IEntityTypeConfiguration<OnboardingTemplate>
{
    public void Configure(EntityTypeBuilder<OnboardingTemplate> b)
    {
        TenantConfig.Base(b, "OnboardingTemplates");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class OnboardingTemplateTaskConfiguration : IEntityTypeConfiguration<OnboardingTemplateTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTemplateTask> b)
    {
        TenantConfig.Base(b, "OnboardingTemplateTasks");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Assignee).HasMaxLength(20);
    }
}

internal sealed class OnboardingPlanConfiguration : IEntityTypeConfiguration<OnboardingPlan>
{
    public void Configure(EntityTypeBuilder<OnboardingPlan> b)
    {
        TenantConfig.Base(b, "OnboardingPlans");
        b.Property(x => x.TemplateName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.BuddyEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Tasks).WithOne(x => x.Plan).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        // One live (not cancelled) plan per employee is enforced in the service; this index keeps lookups fast.
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId });
    }
}

internal sealed class OnboardingTaskConfiguration : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTask> b)
    {
        TenantConfig.Base(b, "OnboardingTasks");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Assignee).HasMaxLength(20);
        b.Property(x => x.CompletedBy).HasMaxLength(150);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.PlanId, x.CompletedAtUtc });
    }
}

internal sealed class SignatureRequestConfiguration : IEntityTypeConfiguration<SignatureRequest>
{
    public void Configure(EntityTypeBuilder<SignatureRequest> b)
    {
        TenantConfig.Base(b, "SignatureRequests");
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.DocumentHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RequestedBy).HasMaxLength(150).IsRequired();
        b.Property(x => x.Message).HasMaxLength(500);
        b.Property(x => x.OtpHash).HasMaxLength(64);
        b.Property(x => x.SignedName).HasMaxLength(150);
        b.Property(x => x.SignedIp).HasMaxLength(64);
        b.Property(x => x.SignedUserAgent).HasMaxLength(300);
        b.Property(x => x.DeclineReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<GeneratedLetter>().WithMany().HasForeignKey(x => x.GeneratedLetterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.SignedDocumentId).OnDelete(DeleteBehavior.Restrict);
        // At most one pending or signed request per letter.
        b.HasIndex(x => x.GeneratedLetterId).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] IN ('Pending','Signed')");
        b.HasIndex(x => new { x.EmployeeId, x.Status });
    }
}

internal sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> b)
    {
        TenantConfig.Base(b, "NotificationRules");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Event).HasMaxLength(30);
    }
}

internal sealed class NotificationRuleLogConfiguration : IEntityTypeConfiguration<NotificationRuleLog>
{
    public void Configure(EntityTypeBuilder<NotificationRuleLog> b)
    {
        TenantConfig.Base(b, "NotificationRuleLogs");
        b.Property(x => x.SubjectKey).HasMaxLength(100).IsRequired();
        b.HasOne<NotificationRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RuleId, x.SubjectKey }).IsUnique();
    }
}

internal sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> b)
    {
        TenantConfig.Base(b, "ReportSchedules");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Kind).HasMaxLength(30);
        b.Property(x => x.Frequency).HasMaxLength(20);
        b.Property(x => x.RecipientUserIds).HasMaxLength(500).IsRequired();
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.NextRunOn });
    }
}

internal sealed class LoginOtpConfiguration : IEntityTypeConfiguration<LoginOtp>
{
    public void Configure(EntityTypeBuilder<LoginOtp> b)
    {
        TenantConfig.Base(b, "LoginOtps");
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}

internal sealed class TravelRequestConfiguration : IEntityTypeConfiguration<TravelRequest>
{
    public void Configure(EntityTypeBuilder<TravelRequest> b)
    {
        TenantConfig.Base(b, "TravelRequests");
        b.Property(x => x.Purpose).HasMaxLength(300).IsRequired();
        b.Property(x => x.FromCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.ToCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.Mode).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.DecidedBy).HasMaxLength(150);
        b.Property(x => x.DecisionNote).HasMaxLength(500);
        b.Property(x => x.BookingDetails).HasMaxLength(2000);
        b.Property(x => x.BookedBy).HasMaxLength(150);
        b.Property(x => x.TicketStorageKey).HasMaxLength(300);
        b.Property(x => x.TicketFileName).HasMaxLength(200);
        b.Property(x => x.TicketContentType).HasMaxLength(100);
        b.Property(x => x.EstimatedCost).HasPrecision(18, 2);
        b.Property(x => x.AdvanceRequested).HasPrecision(18, 2);
        b.Property(x => x.AdvancePaid).HasPrecision(18, 2);
        b.Property(x => x.BookedCost).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.EmployeeId, x.DepartOn });
    }
}
