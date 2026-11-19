using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class FeedbackNoteConfiguration : IEntityTypeConfiguration<FeedbackNote>
{
    public void Configure(EntityTypeBuilder<FeedbackNote> b)
    {
        TenantConfig.Base(b, "FeedbackNotes");
        b.Property(x => x.Tone).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AuthorEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AboutEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FeedbackRequest>().WithMany().HasForeignKey(x => x.FeedbackRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.AboutEmployeeId, x.AtUtc });
        b.HasIndex(x => new { x.OrganizationId, x.AuthorEmployeeId, x.AtUtc });
    }
}

internal sealed class FeedbackRequestConfiguration : IEntityTypeConfiguration<FeedbackRequest>
{
    public void Configure(EntityTypeBuilder<FeedbackRequest> b)
    {
        TenantConfig.Base(b, "FeedbackRequests");
        b.Property(x => x.Question).HasMaxLength(500).IsRequired();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.RequestedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AboutEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FeedbackRequestRecipientConfiguration : IEntityTypeConfiguration<FeedbackRequestRecipient>
{
    public void Configure(EntityTypeBuilder<FeedbackRequestRecipient> b)
    {
        TenantConfig.Base(b, "FeedbackRequestRecipients");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<FeedbackRequest>().WithMany(r => r.Recipients).HasForeignKey(x => x.FeedbackRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.Status });
        b.HasIndex(x => new { x.FeedbackRequestId, x.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class OneOnOneConfiguration : IEntityTypeConfiguration<OneOnOne>
{
    public void Configure(EntityTypeBuilder<OneOnOne> b)
    {
        TenantConfig.Base(b, "OneOnOnes");
        b.Property(x => x.Repeat).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.SharedNotes).HasMaxLength(8000);
        b.Property(x => x.ManagerPrivateNotes).HasMaxLength(4000);
        b.Property(x => x.EmployeePrivateNotes).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ManagerEmployeeId, x.OnDate });
        b.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.OnDate });
    }
}

internal sealed class MeetingItemConfiguration : IEntityTypeConfiguration<MeetingItem>
{
    public void Configure(EntityTypeBuilder<MeetingItem> b)
    {
        TenantConfig.Base(b, "MeetingItems");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Text).HasMaxLength(500).IsRequired();
        b.HasOne<OneOnOne>().WithMany(m => m.Items).HasForeignKey(x => x.OneOnOneId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
