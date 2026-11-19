using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Organizations;

public sealed class OrganizationListQuery
{
    public OrganizationStatus? Status { get; set; }
    public string? Search { get; set; }
    public int? PlanId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record OrganizationRow(
    int Id, string Code, string Name, string Email, string? City, OrganizationStatus Status,
    string? PlanName, SubscriptionStatus? SubscriptionStatus, DateOnly? EndDate, int UserCount, DateTime CreatedAtUtc);

public sealed class OrganizationEditInput
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [StringLength(200), Display(Name = "Legal name")]
    public string? LegalName { get; set; }
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;
    [Phone, StringLength(20)]
    public string? Phone { get; set; }
    [Url, StringLength(200)]
    public string? Website { get; set; }
    [StringLength(15), RegularExpression("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "Enter a valid 15 character GSTIN.")]
    [Display(Name = "GSTIN")]
    public string? Gstin { get; set; }
    [StringLength(10), RegularExpression("^[A-Z]{5}[0-9]{4}[A-Z]$", ErrorMessage = "Enter a valid PAN.")]
    [Display(Name = "PAN")]
    public string? Pan { get; set; }
    [StringLength(300), Display(Name = "Address")]
    public string? AddressLine { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? State { get; set; }
    [Required, StringLength(100)]
    public string Country { get; set; } = "India";
    [StringLength(10), Display(Name = "PIN code")]
    public string? PostalCode { get; set; }
    [StringLength(100)]
    public string? Industry { get; set; }
    [Required, StringLength(100), Display(Name = "Time zone")]
    public string TimeZone { get; set; } = "India Standard Time";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$"), Display(Name = "Brand colour")]
    public string BrandColor { get; set; } = "#1F5EFF";
}

public sealed class OrganizationLimitsInput
{
    [Range(1, 1_000_000), Display(Name = "Employee limit")]
    public int? MaxEmployees { get; set; }
    [Range(1, 10_000), Display(Name = "Branch limit")]
    public int? MaxBranches { get; set; }
    [Range(1, 100_000), Display(Name = "Storage limit (GB)")]
    public int? StorageLimitGb { get; set; }
}

public enum OverrideMode { Inherit, Enable, Disable }

public sealed record FeatureAccessRow(
    int FeatureId, string Code, string Name, string ModuleGroup, FeatureTier Tier, bool GloballyEnabled,
    bool InPlan, OverrideMode Override, string? OverrideReason, DateOnly? OverrideExpiresOn, bool Effective);

public sealed record OrganizationUserRow(int Id, string FullName, string Email, bool IsActive, bool IsOwner, string Roles, DateTime? LastLoginAtUtc);

public sealed class OrganizationDetails
{
    public required Organization Organization { get; init; }
    public OrganizationSubscription? Current { get; init; }
    public required IReadOnlyList<OrganizationSubscription> History { get; init; }
    public required EffectiveLimits Limits { get; init; }
    public required int UserCount { get; init; }
    public required int ActiveUserCount { get; init; }
    public required IReadOnlyList<OrganizationUserRow> Administrators { get; init; }
    public required IReadOnlyList<FeatureAccessRow> Features { get; init; }
    public required IReadOnlyList<AuditLog> RecentActivity { get; init; }
}

/// <summary>Platform-level management of client organizations. Reachable only by platform principals.</summary>
public interface IOrganizationService
{
    Task<PagedResult<OrganizationRow>> ListAsync(OrganizationListQuery query, CancellationToken ct = default);
    Task<IReadOnlyDictionary<OrganizationStatus, int>> CountByStatusAsync(CancellationToken ct = default);
    Task<OrganizationDetails?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<OrganizationEditInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, OrganizationEditInput input, CancellationToken ct = default);
    Task<Result> ChangeStatusAsync(int id, OrganizationStatus status, string reason, CancellationToken ct = default);
    Task<Result> UpdateLimitsAsync(int id, OrganizationLimitsInput input, CancellationToken ct = default);
    Task<Result> SetFeatureOverrideAsync(int id, int featureId, OverrideMode mode, string? reason, DateOnly? expiresOn, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, string confirmationCode, string reason, CancellationToken ct = default);
}

public sealed class OrganizationService(
    IAppDbContext db, ICurrentContext context, IClock clock, ISubscriptionService subscriptions,
    IFeatureAccessService featureAccess, IAuditWriter audit) : IOrganizationService
{
    public async Task<PagedResult<OrganizationRow>> ListAsync(OrganizationListQuery q, CancellationToken ct = default)
    {
        EnsurePlatform();
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 5, 100);

        var query = db.Organizations.AsNoTracking().AsQueryable();
        if (q.Status is { } status) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(o => o.Name.Contains(s) || o.Code.Contains(s) || o.Email.Contains(s) || (o.City != null && o.City.Contains(s)));
        }
        if (q.PlanId is { } planId)
            query = query.Where(o => o.Subscriptions.Any(s => s.IsCurrent && s.PlanId == planId));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .Select(o => new OrganizationRow(
                o.Id, o.Code, o.Name, o.Email, o.City, o.Status,
                o.Subscriptions.Where(s => s.IsCurrent).Select(s => s.Plan.Name).FirstOrDefault(),
                o.Subscriptions.Where(s => s.IsCurrent).Select(s => (SubscriptionStatus?)s.Status).FirstOrDefault(),
                o.Subscriptions.Where(s => s.IsCurrent).Select(s => (DateOnly?)s.EndDate).FirstOrDefault(),
                db.Users.Count(u => u.OrganizationId == o.Id),
                o.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<OrganizationRow>(items, total, page, size);
    }

    public async Task<IReadOnlyDictionary<OrganizationStatus, int>> CountByStatusAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        var rows = await db.Organizations.AsNoTracking().GroupBy(o => o.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<OrganizationStatus>().ToDictionary(s => s, s => rows.FirstOrDefault(r => r.Key == s)?.Count ?? 0);
    }

    public async Task<OrganizationDetails?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return null;

        var current = await db.OrganizationSubscriptions.AsNoTracking().Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == id && s.IsCurrent, ct);
        var users = db.Users.AsNoTracking().Where(u => u.OrganizationId == id);
        var admins = await users
            .Where(u => u.IsOrganizationOwner || u.UserRoles.Any(r => r.Role.Name == Catalog.TenantPermissions.OrgAdminRole))
            .Select(u => new OrganizationUserRow(u.Id, u.FullName, u.Email, u.IsActive, u.IsOrganizationOwner,
                string.Join(", ", u.UserRoles.Select(r => r.Role.Name)), u.LastLoginAtUtc))
            .ToListAsync(ct);

        return new OrganizationDetails
        {
            Organization = org,
            Current = current,
            History = await subscriptions.GetHistoryAsync(id, ct),
            Limits = await subscriptions.GetLimitsAsync(id, ct),
            UserCount = await users.CountAsync(ct),
            ActiveUserCount = await users.CountAsync(u => u.IsActive, ct),
            Administrators = admins,
            Features = await BuildFeatureMatrixAsync(id, current, ct),
            RecentActivity = await db.AuditLogs.AsNoTracking().Where(a => a.OrganizationId == id)
                .OrderByDescending(a => a.Id).Take(15).ToListAsync(ct)
        };
    }

    private async Task<IReadOnlyList<FeatureAccessRow>> BuildFeatureMatrixAsync(int orgId, OrganizationSubscription? current, CancellationToken ct)
    {
        var features = await db.Features.AsNoTracking().OrderBy(f => f.Tier).ThenBy(f => f.SortOrder).ToListAsync(ct);
        var inPlan = current is null ? new HashSet<int>() : await featureAccess.GetEffectivePlanFeatureIdsAsync(current.PlanId, ct);
        var overrides = await db.OrganizationFeatureOverrides.AsNoTracking()
            .Where(o => o.OrganizationId == orgId).ToDictionaryAsync(o => o.FeatureId, ct);
        var enabled = await featureAccess.GetEnabledFeaturesAsync(orgId, ct);

        return features.Select(f =>
        {
            overrides.TryGetValue(f.Id, out var o);
            var mode = o is null ? OverrideMode.Inherit : o.IsEnabled ? OverrideMode.Enable : OverrideMode.Disable;
            return new FeatureAccessRow(f.Id, f.Code, f.Name, f.ModuleGroup, f.Tier, f.IsGloballyEnabled,
                inPlan.Contains(f.Id), mode, o?.Reason, o?.ExpiresOn, enabled.Contains(f.Code));
        }).ToList();
    }

    public async Task<OrganizationEditInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        EnsurePlatform();
        var o = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? null : ToEditInput(o);
    }

    public static OrganizationEditInput ToEditInput(Organization o) => new()
    {
        Name = o.Name, LegalName = o.LegalName, Email = o.Email, Phone = o.Phone, Website = o.Website,
        Gstin = o.Gstin, Pan = o.Pan, AddressLine = o.AddressLine, City = o.City, State = o.State,
        Country = o.Country, PostalCode = o.PostalCode, Industry = o.Industry, TimeZone = o.TimeZone, BrandColor = o.BrandColor
    };

    public static void ApplyEditInput(Organization o, OrganizationEditInput i)
    {
        o.Name = i.Name.Trim();
        o.LegalName = i.LegalName?.Trim();
        o.Email = i.Email.Trim().ToLowerInvariant();
        o.Phone = i.Phone?.Trim();
        o.Website = i.Website?.Trim();
        o.Gstin = i.Gstin?.Trim().ToUpperInvariant();
        o.Pan = i.Pan?.Trim().ToUpperInvariant();
        o.AddressLine = i.AddressLine?.Trim();
        o.City = i.City?.Trim();
        o.State = i.State?.Trim();
        o.Country = i.Country.Trim();
        o.PostalCode = i.PostalCode?.Trim();
        o.Industry = i.Industry?.Trim();
        o.TimeZone = i.TimeZone;
        o.BrandColor = i.BrandColor;
    }

    public async Task<Result> UpdateAsync(int id, OrganizationEditInput input, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return Result.Fail("Organization not found.");
        ApplyEditInput(org, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ChangeStatusAsync(int id, OrganizationStatus status, string reason, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("A reason is required to change organization status.");
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return Result.Fail("Organization not found.");
        if (org.Status == status) return Result.Fail($"Organization is already {status}.");

        if (status is OrganizationStatus.Active or OrganizationStatus.Trial)
        {
            var sub = await subscriptions.GetCurrentAsync(id, ct);
            if (sub is null || !sub.GrantsAccess(clock.Today))
                return Result.Fail("The organization has no valid subscription. Activate or extend its subscription first.");
            // Re-activation follows the subscription state rather than the requested label.
            status = sub.Status == SubscriptionStatus.Trial ? OrganizationStatus.Trial : OrganizationStatus.Active;
        }

        var old = org.Status;
        org.Status = status;
        org.StatusReason = reason.Trim();
        org.StatusChangedAtUtc = clock.UtcNow;
        audit.Add("Organization.StatusChange", nameof(Organization), id.ToString(), $"{old} → {status}: {reason}", id);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(id);
        return Result.Ok();
    }

    public async Task<Result> UpdateLimitsAsync(int id, OrganizationLimitsInput input, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return Result.Fail("Organization not found.");
        org.MaxEmployeesOverride = input.MaxEmployees;
        org.MaxBranchesOverride = input.MaxBranches;
        org.StorageLimitGbOverride = input.StorageLimitGb;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetFeatureOverrideAsync(int id, int featureId, OverrideMode mode, string? reason, DateOnly? expiresOn, CancellationToken ct = default)
    {
        EnsurePlatform();
        if (!await db.Organizations.AnyAsync(o => o.Id == id, ct)) return Result.Fail("Organization not found.");
        if (!await db.Features.AnyAsync(f => f.Id == featureId, ct)) return Result.Fail("Feature not found.");
        if (expiresOn is { } e && e < clock.Today) return Result.Fail("Expiry date cannot be in the past.");

        var existing = await db.OrganizationFeatureOverrides.FirstOrDefaultAsync(o => o.OrganizationId == id && o.FeatureId == featureId, ct);
        if (mode == OverrideMode.Inherit)
        {
            if (existing is not null) db.OrganizationFeatureOverrides.Remove(existing);
        }
        else
        {
            existing ??= db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverride { OrganizationId = id, FeatureId = featureId }).Entity;
            existing.IsEnabled = mode == OverrideMode.Enable;
            existing.Reason = reason?.Trim();
            existing.ExpiresOn = expiresOn;
        }
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(id);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, string confirmationCode, string reason, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null) return Result.Fail("Organization not found.");
        if (!string.Equals(org.Code, confirmationCode?.Trim(), StringComparison.Ordinal))
            return Result.Fail("Confirmation code does not match the organization code.");
        if (org.Status is OrganizationStatus.Active or OrganizationStatus.Trial)
            return Result.Fail("Suspend or deactivate the organization before deleting it.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("A reason is required.");

        // Soft delete: data stays recoverable for the retention period and the code stays reserved.
        org.IsDeleted = true;
        org.DeletedAtUtc = clock.UtcNow;
        org.DeletedBy = context.UserName;
        org.StatusReason = reason.Trim();
        var sub = await subscriptions.GetCurrentAsync(id, ct);
        if (sub is not null && sub.Status is not (SubscriptionStatus.Cancelled or SubscriptionStatus.Expired))
        {
            sub.Status = SubscriptionStatus.Cancelled;
            sub.CancelledOn = clock.Today;
            sub.CancellationReason = "Organization deleted";
            sub.AutoRenew = false;
        }
        audit.Add("Organization.Delete", nameof(Organization), id.ToString(), reason, id);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(id);
        return Result.Ok();
    }

    private void EnsurePlatform()
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Organization management requires a platform principal.");
    }
}
