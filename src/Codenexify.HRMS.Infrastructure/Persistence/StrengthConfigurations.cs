using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Strength;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        TenantConfig.Base(b, "Assets");
        b.Property(x => x.AssetTag).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Category).HasMaxLength(60).IsRequired();
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.CurrentEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.AssetTag }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> b)
    {
        TenantConfig.Base(b, "AssetAssignments");
        b.Property(x => x.AssignedBy).HasMaxLength(150).IsRequired();
        b.Property(x => x.ReturnedTo).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.ReturnedOn });
    }
}

internal sealed class OfficeLocationConfiguration : IEntityTypeConfiguration<OfficeLocation>
{
    public void Configure(EntityTypeBuilder<OfficeLocation> b)
    {
        TenantConfig.Base(b, "OfficeLocations");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> b)
    {
        TenantConfig.Base(b, "Surveys");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class SurveyQuestionConfiguration : IEntityTypeConfiguration<SurveyQuestion>
{
    public void Configure(EntityTypeBuilder<SurveyQuestion> b)
    {
        TenantConfig.Base(b, "SurveyQuestions");
        b.Property(x => x.Text).HasMaxLength(300).IsRequired();
        b.Property(x => x.Options).HasMaxLength(1000);
    }
}

internal sealed class SurveyParticipationConfiguration : IEntityTypeConfiguration<SurveyParticipation>
{
    public void Configure(EntityTypeBuilder<SurveyParticipation> b)
    {
        b.ToTable("SurveyParticipations");
        b.HasKey(x => new { x.SurveyId, x.UserId }); // no identity column: insertion order is not recorded
        b.HasOne<Domain.Platform.Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DeletedBy).HasMaxLength(200);
    }
}

internal sealed class SurveyResponseConfiguration : IEntityTypeConfiguration<SurveyResponse>
{
    public void Configure(EntityTypeBuilder<SurveyResponse> b)
    {
        TenantConfig.Base(b, "SurveyResponses");
        b.Property(x => x.RespondentName).HasMaxLength(150);
        b.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SurveyId);
    }
}

internal sealed class SurveyAnswerConfiguration : IEntityTypeConfiguration<SurveyAnswer>
{
    public void Configure(EntityTypeBuilder<SurveyAnswer> b)
    {
        TenantConfig.Base(b, "SurveyAnswers");
        b.Property(x => x.Text).HasMaxLength(2000);
        b.HasOne<SurveyQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FeedbackThreadConfiguration : IEntityTypeConfiguration<FeedbackThread>
{
    public void Configure(EntityTypeBuilder<FeedbackThread> b)
    {
        TenantConfig.Base(b, "FeedbackThreads");
        b.Property(x => x.Category).HasMaxLength(60).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(150).IsRequired();
        b.Property(x => x.AccessCodeHash).HasMaxLength(64).IsRequired();
        b.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AccessCodeHash).IsUnique();
    }
}

internal sealed class FeedbackMessageConfiguration : IEntityTypeConfiguration<FeedbackMessage>
{
    public void Configure(EntityTypeBuilder<FeedbackMessage> b)
    {
        TenantConfig.Base(b, "FeedbackMessages");
        b.Property(x => x.AuthorName).HasMaxLength(150);
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
    }
}
