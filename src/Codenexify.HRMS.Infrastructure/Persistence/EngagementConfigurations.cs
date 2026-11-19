using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class WallPostConfiguration : IEntityTypeConfiguration<WallPost>
{
    public void Configure(EntityTypeBuilder<WallPost> b)
    {
        TenantConfig.Base(b, "WallPosts");
        b.Property(x => x.AuthorName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Badge).HasMaxLength(60);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Recipients).WithOne().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.OrganizationId, x.IsPinned });
    }
}

internal sealed class WallPostRecipientConfiguration : IEntityTypeConfiguration<WallPostRecipient>
{
    public void Configure(EntityTypeBuilder<WallPostRecipient> b)
    {
        TenantConfig.Base(b, "WallPostRecipients");
        b.Property(x => x.EmployeeName).HasMaxLength(150).IsRequired();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.EmployeeId);
    }
}

internal sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> b)
    {
        TenantConfig.Base(b, "PollOptions");
        b.Property(x => x.Text).HasMaxLength(150).IsRequired();
    }
}

internal sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> b)
    {
        TenantConfig.Base(b, "PollVotes");
        b.HasOne<WallPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PollOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
    }
}

internal sealed class WallCommentConfiguration : IEntityTypeConfiguration<WallComment>
{
    public void Configure(EntityTypeBuilder<WallComment> b)
    {
        TenantConfig.Base(b, "WallComments");
        b.Property(x => x.AuthorName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        b.HasOne<WallPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PostId);
    }
}

internal sealed class WallReactionConfiguration : IEntityTypeConfiguration<WallReaction>
{
    public void Configure(EntityTypeBuilder<WallReaction> b)
    {
        TenantConfig.Base(b, "WallReactions");
        b.HasOne<WallPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        // Un-liking soft-deletes the row, so uniqueness applies to live reactions only.
        b.HasIndex(x => new { x.PostId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class LetterTemplateConfiguration : IEntityTypeConfiguration<LetterTemplate>
{
    public void Configure(EntityTypeBuilder<LetterTemplate> b)
    {
        TenantConfig.Base(b, "LetterTemplates");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(8000).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class GeneratedLetterConfiguration : IEntityTypeConfiguration<GeneratedLetter>
{
    public void Configure(EntityTypeBuilder<GeneratedLetter> b)
    {
        TenantConfig.Base(b, "GeneratedLetters");
        b.Property(x => x.Number).HasMaxLength(40).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(8000).IsRequired();
        b.Property(x => x.IssuedByName).HasMaxLength(150).IsRequired();
        b.Property(x => x.SignatoryName).HasMaxLength(150);
        b.Property(x => x.SignatoryDesignation).HasMaxLength(150);
        b.Property(x => x.RevokedReason).HasMaxLength(300);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LetterTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        // Numbers never repeat inside an organization (sequence per financial year).
        b.HasIndex(x => new { x.OrganizationId, x.FinancialYear, x.Sequence }).IsUnique();
        b.HasIndex(x => x.EmployeeId);
    }
}

internal sealed class ProfileChangeRequestConfiguration : IEntityTypeConfiguration<ProfileChangeRequest>
{
    public void Configure(EntityTypeBuilder<ProfileChangeRequest> b)
    {
        TenantConfig.Base(b, "ProfileChangeRequests");
        b.Property(x => x.ChangesJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.PreviousJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.DecidedBy).HasMaxLength(150);
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}
