using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal static class TenantConfig
{
    /// <summary>Common shape of every tenant table: FK to Organizations, audit columns.</summary>
    public static void Base<T>(EntityTypeBuilder<T> b, string table) where T : TenantEntity
    {
        b.ToTable(table);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.Property(x => x.UpdatedBy).HasMaxLength(200);
        b.Property(x => x.DeletedBy).HasMaxLength(200);
    }
}

internal sealed class OrganizationSettingConfiguration : IEntityTypeConfiguration<OrganizationSetting>
{
    public void Configure(EntityTypeBuilder<OrganizationSetting> b)
    {
        TenantConfig.Base(b, "OrganizationSettings");
        b.HasIndex(x => x.OrganizationId).IsUnique();
        b.Property(x => x.EmployeeIdPrefix).HasMaxLength(10).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.GeoMode).HasMaxLength(20).HasDefaultValue(Domain.Strength.GeoAttendanceMode.Off);
        b.Property(x => x.SelfieRetentionDays).HasDefaultValue(90);
        b.Property(x => x.SsoDomains).HasMaxLength(300);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        TenantConfig.Base(b, "Branches");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.AddressLine).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        TenantConfig.Base(b, "Departments");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne(x => x.ParentDepartment).WithMany().HasForeignKey(x => x.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.HeadEmployee).WithMany().HasForeignKey(x => x.HeadEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
    }
}

internal sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> b)
    {
        TenantConfig.Base(b, "Designations");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.Name });
    }
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        TenantConfig.Base(b, "Employees");
        b.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeCode }).IsUnique().HasDatabaseName("UX_Employees_OrganizationId_Code");
        b.HasIndex(x => new { x.OrganizationId, x.WorkEmail });
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.MiddleName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.WorkEmail).HasMaxLength(200).IsRequired();
        b.Property(x => x.PersonalEmail).HasMaxLength(200);
        b.Property(x => x.MobilePhone).HasMaxLength(20);
        b.Property(x => x.BloodGroup).HasMaxLength(5);
        b.Property(x => x.Nationality).HasMaxLength(60);
        b.Property(x => x.CurrentAddress).HasMaxLength(500);
        b.Property(x => x.PermanentAddress).HasMaxLength(500);
        b.Property(x => x.EmergencyContactName).HasMaxLength(150);
        b.Property(x => x.EmergencyContactRelation).HasMaxLength(50);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
        b.Property(x => x.PanEncrypted).HasMaxLength(1000);
        b.Property(x => x.PanMasked).HasMaxLength(10);
        b.Property(x => x.Uan).HasMaxLength(12);
        b.Ignore(x => x.FullName);
        b.Ignore(x => x.IsCurrent);
        b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReportingManager).WithMany().HasForeignKey(x => x.ReportingManagerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BankDetail).WithOne().HasForeignKey<EmployeeBankDetail>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Education).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Experience).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Documents).WithOne(d => d.Employee).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmployeeBankDetailConfiguration : IEntityTypeConfiguration<EmployeeBankDetail>
{
    public void Configure(EntityTypeBuilder<EmployeeBankDetail> b)
    {
        TenantConfig.Base(b, "EmployeeBankDetails");
        b.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        b.Property(x => x.AccountHolderName).HasMaxLength(150).IsRequired();
        b.Property(x => x.AccountNumberEncrypted).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AccountNumberLast4).HasMaxLength(4).IsRequired();
        b.Property(x => x.Ifsc).HasMaxLength(11).IsRequired();
        b.Property(x => x.BranchName).HasMaxLength(150);
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class EmployeeEducationConfiguration : IEntityTypeConfiguration<EmployeeEducation>
{
    public void Configure(EntityTypeBuilder<EmployeeEducation> b)
    {
        TenantConfig.Base(b, "EmployeeEducation");
        b.Property(x => x.Qualification).HasMaxLength(150).IsRequired();
        b.Property(x => x.Institution).HasMaxLength(200).IsRequired();
        b.Property(x => x.Specialization).HasMaxLength(150);
        b.Property(x => x.Grade).HasMaxLength(30);
    }
}

internal sealed class EmployeeExperienceConfiguration : IEntityTypeConfiguration<EmployeeExperience>
{
    public void Configure(EntityTypeBuilder<EmployeeExperience> b)
    {
        TenantConfig.Base(b, "EmployeeExperience");
        b.Property(x => x.Company).HasMaxLength(200).IsRequired();
        b.Property(x => x.Designation).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);
    }
}

internal sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        TenantConfig.Base(b, "EmployeeDocuments");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.VerifiedBy).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.OrganizationId, x.ExpiresOn });
    }
}

internal sealed class ExitRequestConfiguration : IEntityTypeConfiguration<ExitRequest>
{
    public void Configure(EntityTypeBuilder<ExitRequest> b)
    {
        TenantConfig.Base(b, "ExitRequests");
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Checklist).WithOne().HasForeignKey(x => x.ExitRequestId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DecidedBy).HasMaxLength(200);
        b.Property(x => x.DecisionNotes).HasMaxLength(1000);
        b.Property(x => x.ExitInterviewNotes).HasMaxLength(2000);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class ExitChecklistItemConfiguration : IEntityTypeConfiguration<ExitChecklistItem>
{
    public void Configure(EntityTypeBuilder<ExitChecklistItem> b)
    {
        TenantConfig.Base(b, "ExitChecklistItems");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Owner).HasMaxLength(50).IsRequired();
        b.Property(x => x.CompletedBy).HasMaxLength(200);
    }
}

internal sealed class ExitChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ExitChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ExitChecklistTemplateItem> b)
    {
        TenantConfig.Base(b, "ExitChecklistTemplateItems");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Owner).HasMaxLength(50).IsRequired();
    }
}
