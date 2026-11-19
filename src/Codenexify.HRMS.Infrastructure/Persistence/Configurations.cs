using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Website).HasMaxLength(200);
        b.Property(x => x.Gstin).HasMaxLength(15);
        b.Property(x => x.Pan).HasMaxLength(10);
        b.Property(x => x.AddressLine).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.TimeZone).HasMaxLength(100);
        b.Property(x => x.Industry).HasMaxLength(100);
        b.Property(x => x.StatusReason).HasMaxLength(500);
        b.Property(x => x.BrandColor).HasMaxLength(7);
        b.Property(x => x.TermsAcceptedVersions).HasMaxLength(200);
        b.Property(x => x.TermsAcceptedIp).HasMaxLength(64);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.DeletedBy).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Status);
        b.Ignore(x => x.IsOperational);
    }
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> b)
    {
        b.ToTable("Features");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.ModuleGroup).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
    }
}

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("SubscriptionPlans");
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.ParentPlan).WithMany().HasForeignKey(x => x.ParentPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> b)
    {
        b.ToTable("PlanFeatures");
        b.HasKey(x => new { x.PlanId, x.FeatureId });
        b.HasOne(x => x.Plan).WithMany(p => p.PlanFeatures).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Feature).WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> b)
    {
        b.ToTable("OrganizationSubscriptions");
        b.HasOne(x => x.Organization).WithMany(o => o.Subscriptions).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.IsTrial);
        b.HasQueryFilter(x => !x.Organization.IsDeleted); // follows its (soft-deletable) organization
        // At most one current subscription per organization.
        b.HasIndex(x => x.OrganizationId).HasFilter("[IsCurrent] = 1").IsUnique().HasDatabaseName("UX_OrganizationSubscriptions_Current");
        b.HasIndex(x => new { x.Status, x.EndDate });
    }
}

internal sealed class OrganizationFeatureOverrideConfiguration : IEntityTypeConfiguration<OrganizationFeatureOverride>
{
    public void Configure(EntityTypeBuilder<OrganizationFeatureOverride> b)
    {
        b.ToTable("OrganizationFeatureOverrides");
        b.HasOne(x => x.Organization).WithMany(o => o.FeatureOverrides).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Feature).WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.FeatureId }).IsUnique();
        b.Property(x => x.Reason).HasMaxLength(300);
        b.HasQueryFilter(x => !x.Organization.IsDeleted);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
    }
}

internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> b)
    {
        b.ToTable("PlatformUsers");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.TwoFactorSecret).HasMaxLength(1000);
        b.Property(x => x.LastLoginIp).HasMaxLength(64);
        b.Property(x => x.AllowedIps).HasMaxLength(500);
        b.Property(x => x.SecurityStamp).HasMaxLength(64);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
    }
}

internal sealed class PlatformUserPermissionConfiguration : IEntityTypeConfiguration<PlatformUserPermission>
{
    public void Configure(EntityTypeBuilder<PlatformUserPermission> b)
    {
        b.ToTable("PlatformUserPermissions");
        b.HasKey(x => new { x.PlatformUserId, x.Permission });
        b.Property(x => x.Permission).HasMaxLength(100);
        b.HasOne(x => x.PlatformUser).WithMany(u => u.Permissions).HasForeignKey(x => x.PlatformUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.SecurityStamp).HasMaxLength(64);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.DeletedBy).HasMaxLength(200);
        b.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique().HasDatabaseName("UX_Users_OrganizationId_Email");
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.DeletedBy).HasMaxLength(200);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions");
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Module).HasMaxLength(100).IsRequired();
        b.Property(x => x.FeatureCode).HasMaxLength(50);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        // Join rows follow the visibility of their user, so they never leak across tenants.
        b.HasQueryFilter(x => !x.User.IsDeleted);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions");
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.Role.IsDeleted);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.ActorName).HasMaxLength(200);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.Details).HasMaxLength(1000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => new { x.OrganizationId, x.TimestampUtc });
        b.HasIndex(x => x.TimestampUtc);
    }
}

internal sealed class LegalPageConfiguration : IEntityTypeConfiguration<LegalPage>
{
    public void Configure(EntityTypeBuilder<LegalPage> b)
    {
        b.ToTable("LegalPages");
        b.Property(x => x.Slug).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(300);
        b.Property(x => x.Body).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<Domain.Identity.PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<Domain.Identity.PasswordResetToken> b)
    {
        b.ToTable("PasswordResetTokens");
        b.Property(x => x.PrincipalType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(x => x.RequestIp).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.PrincipalType, x.UserId, x.CreatedAtUtc });
    }
}

internal sealed class LoginAuditConfiguration : IEntityTypeConfiguration<LoginAudit>
{
    public void Configure(EntityTypeBuilder<LoginAudit> b)
    {
        b.ToTable("LoginAudits");
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.FailureReason).HasMaxLength(300);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.HasIndex(x => x.TimestampUtc);
        b.HasIndex(x => new { x.PrincipalType, x.Succeeded, x.TimestampUtc });
    }
}
