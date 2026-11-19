using Codenexify.HRMS.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Codenexify.HRMS.Infrastructure.Persistence;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        TenantConfig.Base(b, "SupportTickets");
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.RaisedByName).HasMaxLength(150).IsRequired();
        b.Property(x => x.RaisedByEmail).HasMaxLength(200);
        b.Property(x => x.AssignedToName).HasMaxLength(150);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Domain.Platform.PlatformUser>().WithMany().HasForeignKey(x => x.AssignedToPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasIndex(x => new { x.Status, x.LastActivityAtUtc });
    }
}

internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> b)
    {
        TenantConfig.Base(b, "TicketMessages");
        b.Property(x => x.AuthorName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.HasIndex(x => x.TicketId);
    }
}

internal sealed class SupportAccessGrantConfiguration : IEntityTypeConfiguration<SupportAccessGrant>
{
    public void Configure(EntityTypeBuilder<SupportAccessGrant> b)
    {
        TenantConfig.Base(b, "SupportAccessGrants");
        b.Property(x => x.GrantedByName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(300).IsRequired();
        b.Property(x => x.RevokedBy).HasMaxLength(150);
        b.HasOne<SupportTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ExpiresAtUtc });
    }
}

internal sealed class SupportSessionConfiguration : IEntityTypeConfiguration<SupportSession>
{
    public void Configure(EntityTypeBuilder<SupportSession> b)
    {
        TenantConfig.Base(b, "SupportSessions");
        b.Property(x => x.PlatformUserName).HasMaxLength(150).IsRequired();
        b.HasOne(x => x.Grant).WithMany().HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Platform.PlatformUser>().WithMany().HasForeignKey(x => x.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlatformAnnouncementConfiguration : IEntityTypeConfiguration<PlatformAnnouncement>
{
    public void Configure(EntityTypeBuilder<PlatformAnnouncement> b)
    {
        b.ToTable("PlatformAnnouncements");
        PlatformConfig.Audit(b);
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        b.HasOne<Domain.Platform.SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.IsActive, x.StartsAtUtc });
    }
}
