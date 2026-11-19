using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Domain.Platform;

/// <summary>A client company (tenant) subscribed to the platform.</summary>
public class Organization : BaseEntity, ISoftDelete, IAuditable
{
    /// <summary>Short unique code used at login (e.g. "acme").</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Gstin { get; set; }
    public string? Pan { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "India";
    public string? PostalCode { get; set; }
    public string TimeZone { get; set; } = "India Standard Time";
    public string? Industry { get; set; }

    public OrganizationStatus Status { get; set; } = OrganizationStatus.PendingVerification;
    public string? StatusReason { get; set; }
    public DateTime? StatusChangedAtUtc { get; set; }

    /// <summary>Organization-specific limits; when null the plan limits apply.</summary>
    public int? MaxEmployeesOverride { get; set; }
    public int? MaxBranchesOverride { get; set; }
    public int? StorageLimitGbOverride { get; set; }
    public int StorageUsedMb { get; set; }

    public string BrandColor { get; set; } = "#1F5EFF";
    public string? LogoUrl { get; set; }

    /// <summary>When the self-registering administrator accepted the terms and privacy policy (null if created by Codenexify staff).</summary>
    public DateTime? TermsAcceptedAtUtc { get; set; }
    /// <summary>Which versions were accepted, e.g. "terms v1, privacy v1".</summary>
    public string? TermsAcceptedVersions { get; set; }
    public string? TermsAcceptedIp { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<OrganizationSubscription> Subscriptions { get; set; } = new List<OrganizationSubscription>();
    public ICollection<OrganizationFeatureOverride> FeatureOverrides { get; set; } = new List<OrganizationFeatureOverride>();

    /// <summary>Statuses whose users may sign in and use enabled features.</summary>
    public bool IsOperational => Status is OrganizationStatus.Active or OrganizationStatus.Trial;
}
