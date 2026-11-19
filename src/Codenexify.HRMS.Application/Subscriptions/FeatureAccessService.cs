using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Codenexify.HRMS.Application.Subscriptions;

public interface IFeatureAccessService
{
    /// <summary>Feature codes the organization may use right now.</summary>
    Task<IReadOnlySet<string>> GetEnabledFeaturesAsync(int organizationId, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(int organizationId, string featureCode, CancellationToken ct = default);
    /// <summary>Feature ids included in a plan, following the parent chain (Growth ⊇ Strength ⊇ Foundation).</summary>
    Task<IReadOnlySet<int>> GetEffectivePlanFeatureIdsAsync(int planId, CancellationToken ct = default);
    void Invalidate(int organizationId);
    void InvalidateAll();
    /// <summary>Changes whenever plans or modules change (InvalidateAll); other caches of plan data key on it.</summary>
    long CacheGeneration { get; }
}

public sealed class FeatureAccessService(IAppDbContext db, IClock clock, IMemoryCache cache) : IFeatureAccessService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private const string GenerationKey = "features:generation";

    public async Task<bool> IsEnabledAsync(int organizationId, string featureCode, CancellationToken ct = default)
        => (await GetEnabledFeaturesAsync(organizationId, ct)).Contains(featureCode);

    public async Task<IReadOnlySet<string>> GetEnabledFeaturesAsync(int organizationId, CancellationToken ct = default)
    {
        var key = $"features:{Generation}:{organizationId}";
        if (cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null) return cached;

        var result = await ComputeAsync(organizationId, ct);
        cache.Set(key, result, CacheDuration);
        return result;
    }

    private async Task<IReadOnlySet<string>> ComputeAsync(int organizationId, CancellationToken ct)
    {
        var empty = new HashSet<string>();
        var today = clock.Today;

        var org = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new { o.Status })
            .FirstOrDefaultAsync(ct);
        if (org is null || !new Organization { Status = org.Status }.IsOperational) return empty;

        var subscription = await db.OrganizationSubscriptions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.IsCurrent)
            .FirstOrDefaultAsync(ct);
        if (subscription is null || !subscription.GrantsAccess(today)) return empty;

        var planFeatureIds = await GetEffectivePlanFeatureIdsAsync(subscription.PlanId, ct);

        var overrides = await db.OrganizationFeatureOverrides.AsNoTracking()
            .Where(o => o.OrganizationId == organizationId && (o.ExpiresOn == null || o.ExpiresOn >= today))
            .Select(o => new { o.FeatureId, o.IsEnabled })
            .ToListAsync(ct);

        var enabledIds = new HashSet<int>(planFeatureIds);
        foreach (var o in overrides)
        {
            if (o.IsEnabled) enabledIds.Add(o.FeatureId);
            else enabledIds.Remove(o.FeatureId);
        }

        var codes = await db.Features.AsNoTracking()
            .Where(f => f.IsGloballyEnabled && enabledIds.Contains(f.Id))
            .Select(f => f.Code)
            .ToListAsync(ct);
        return new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<int>> GetEffectivePlanFeatureIdsAsync(int planId, CancellationToken ct = default)
    {
        var plans = await db.SubscriptionPlans.AsNoTracking()
            .Select(p => new { p.Id, p.ParentPlanId })
            .ToDictionaryAsync(p => p.Id, p => p.ParentPlanId, ct);

        var chain = new List<int>();
        int? current = planId;
        while (current is int id && plans.ContainsKey(id) && !chain.Contains(id)) // cycle-safe
        {
            chain.Add(id);
            current = plans[id];
        }

        var ids = await db.PlanFeatures.AsNoTracking()
            .Where(pf => chain.Contains(pf.PlanId))
            .Select(pf => pf.FeatureId)
            .Distinct()
            .ToListAsync(ct);
        return new HashSet<int>(ids);
    }

    private long Generation => cache.GetOrCreate(GenerationKey, _ => 0L);
    public long CacheGeneration => Generation;

    public void Invalidate(int organizationId) => cache.Remove($"features:{Generation}:{organizationId}");

    /// <summary>Plan or global feature changes affect many organizations: bump the cache generation.</summary>
    public void InvalidateAll() => cache.Set(GenerationKey, Generation + 1);
}
