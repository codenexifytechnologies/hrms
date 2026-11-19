using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class CoreValueConfiguration : IEntityTypeConfiguration<CoreValue>
{
    public void Configure(EntityTypeBuilder<CoreValue> b)
    {
        TenantConfig.Base(b, "CoreValues");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Icon).HasMaxLength(40).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class ValueShoutoutConfiguration : IEntityTypeConfiguration<ValueShoutout>
{
    public void Configure(EntityTypeBuilder<ValueShoutout> b)
    {
        TenantConfig.Base(b, "ValueShoutouts");
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.HasOne<CoreValue>().WithMany().HasForeignKey(x => x.CoreValueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.FromEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ToEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.AtUtc });
        b.HasIndex(x => new { x.OrganizationId, x.ToEmployeeId });
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        TenantConfig.Base(b, "Skills");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Category).HasMaxLength(60).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
    public void Configure(EntityTypeBuilder<EmployeeSkill> b)
    {
        TenantConfig.Base(b, "EmployeeSkills");
        b.Property(x => x.ManagerNote).HasMaxLength(500);
        b.Property(x => x.AssessedByName).HasMaxLength(150);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.SkillId }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.OrganizationId, x.SkillId });
    }
}

internal sealed class SkillRequirementConfiguration : IEntityTypeConfiguration<SkillRequirement>
{
    public void Configure(EntityTypeBuilder<SkillRequirement> b)
    {
        TenantConfig.Base(b, "SkillRequirements");
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.DesignationId, x.SkillId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
