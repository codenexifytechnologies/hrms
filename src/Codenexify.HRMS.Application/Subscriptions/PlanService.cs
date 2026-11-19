using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Subscriptions;

public sealed class PlanInput
{
    [Required, StringLength(40), RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "Use upper-case letters, digits and underscores.")]
    public string Code { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
    [Display(Name = "Inherits features from")]
    public int? ParentPlanId { get; set; }
    [Range(0, 10_000_000), Display(Name = "Monthly price (₹)")]
    public decimal MonthlyPrice { get; set; }
    [Range(0, 100_000_000), Display(Name = "Yearly price (₹)")]
    public decimal YearlyPrice { get; set; }
    [Display(Name = "Price is per employee")]
    public bool IsPerEmployeePricing { get; set; } = true;
    [Range(0, 365), Display(Name = "Trial days")]
    public int TrialDays { get; set; } = 14;
    [Range(1, 1_000_000), Display(Name = "Max employees")]
    public int MaxEmployees { get; set; } = 50;
    [Range(1, 10_000), Display(Name = "Max branches")]
    public int MaxBranches { get; set; } = 1;
    [Range(1, 100_000), Display(Name = "Storage limit (GB)")]
    public int StorageLimitGb { get; set; } = 5;
    [Display(Name = "Custom / enterprise plan")]
    public bool IsCustom { get; set; }
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    [Display(Name = "Show on public pricing / registration")]
    public bool IsPublic { get; set; } = true;
    [Range(0, 1000), Display(Name = "Sort order")]
    public int SortOrder { get; set; }
    /// <summary>Features added directly by this plan (inherited ones come from the parent).</summary>
    public List<int> FeatureIds { get; set; } = [];
}

public sealed record PlanSummary(
    int Id, string Code, string Name, string? ParentName, decimal MonthlyPrice, decimal YearlyPrice,
    bool IsPerEmployeePricing, int TrialDays, int MaxEmployees, int MaxBranches, int StorageLimitGb,
    bool IsCustom, bool IsActive, bool IsPublic, int OwnFeatureCount, int EffectiveFeatureCount, int ActiveSubscriptions);

/// <summary>Modules (enabled platform-wide) that at least one of the given plans includes, and each plan's effective set.</summary>
public sealed record PlanModuleCatalog(IReadOnlyList<Feature> Modules, IReadOnlyDictionary<int, IReadOnlySet<int>> PlanModuleIds);

public interface IPlanService
{
    Task<IReadOnlyList<PlanSummary>> ListAsync(CancellationToken ct = default);
    /// <summary>For the public pricing page: every module per plan (inherited ones included), in catalogue order.</summary>
    Task<PlanModuleCatalog> ModuleCatalogAsync(IEnumerable<int> planIds, CancellationToken ct = default);
    Task<PlanInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Feature>> GetFeaturesAsync(CancellationToken ct = default);
    Task<Result<int>> CreateAsync(PlanInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, PlanInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result> SetFeatureGlobalStateAsync(int featureId, bool enabled, CancellationToken ct = default);
}

public sealed class PlanService(IAppDbContext db, IFeatureAccessService featureAccess) : IPlanService
{
    public async Task<IReadOnlyList<PlanSummary>> ListAsync(CancellationToken ct = default)
    {
        var plans = await db.SubscriptionPlans.AsNoTracking()
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Code, p.Name, ParentName = p.ParentPlan != null ? p.ParentPlan.Name : null,
                p.MonthlyPrice, p.YearlyPrice, p.IsPerEmployeePricing, p.TrialDays, p.MaxEmployees, p.MaxBranches,
                p.StorageLimitGb, p.IsCustom, p.IsActive, p.IsPublic,
                Own = p.PlanFeatures.Count,
                Subs = db.OrganizationSubscriptions.Count(s => s.PlanId == p.Id && s.IsCurrent &&
                    (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.PastDue))
            })
            .ToListAsync(ct);

        var effective = await EffectiveFeatureSetsAsync(plans.Select(p => p.Id), ct);
        return plans.Select(p => new PlanSummary(p.Id, p.Code, p.Name, p.ParentName, p.MonthlyPrice, p.YearlyPrice,
            p.IsPerEmployeePricing, p.TrialDays, p.MaxEmployees, p.MaxBranches, p.StorageLimitGb, p.IsCustom,
            p.IsActive, p.IsPublic, p.Own, effective[p.Id].Count, p.Subs)).ToList();
    }

    /// <summary>
    /// Each plan's modules including those inherited from parent plans, for many plans at once: two queries in total
    /// (previously two per plan, which made the public pricing page slow on a remote database).
    /// </summary>
    private async Task<Dictionary<int, IReadOnlySet<int>>> EffectiveFeatureSetsAsync(IEnumerable<int> planIds, CancellationToken ct)
    {
        var parents = await db.SubscriptionPlans.AsNoTracking().Select(p => new { p.Id, p.ParentPlanId }).ToDictionaryAsync(p => p.Id, p => p.ParentPlanId, ct);
        var own = (await db.PlanFeatures.AsNoTracking().Select(pf => new { pf.PlanId, pf.FeatureId }).ToListAsync(ct))
            .GroupBy(pf => pf.PlanId).ToDictionary(g => g.Key, g => g.Select(x => x.FeatureId).ToList());
        var result = new Dictionary<int, IReadOnlySet<int>>();
        foreach (var planId in planIds.Distinct())
        {
            var set = new HashSet<int>();
            var seen = new HashSet<int>();
            int? current = planId;
            while (current is int id && parents.ContainsKey(id) && seen.Add(id)) // cycle-safe, same rule as FeatureAccessService
            {
                if (own.TryGetValue(id, out var features)) set.UnionWith(features);
                current = parents[id];
            }
            result[planId] = set;
        }
        return result;
    }

    public async Task<PlanModuleCatalog> ModuleCatalogAsync(IEnumerable<int> planIds, CancellationToken ct = default)
    {
        var sets = new Dictionary<int, IReadOnlySet<int>>(await EffectiveFeatureSetsAsync(planIds, ct));
        var used = sets.Values.SelectMany(s => s).ToHashSet();
        var modules = await db.Features.AsNoTracking().Where(f => f.IsGloballyEnabled && used.Contains(f.Id))
            .OrderBy(f => f.Tier).ThenBy(f => f.SortOrder).ToListAsync(ct);
        return new PlanModuleCatalog(modules, sets);
    }

    public async Task<PlanInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.SubscriptionPlans.AsNoTracking().Include(x => x.PlanFeatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        return new PlanInput
        {
            Code = p.Code, Name = p.Name, Description = p.Description, ParentPlanId = p.ParentPlanId,
            MonthlyPrice = p.MonthlyPrice, YearlyPrice = p.YearlyPrice, IsPerEmployeePricing = p.IsPerEmployeePricing,
            TrialDays = p.TrialDays, MaxEmployees = p.MaxEmployees, MaxBranches = p.MaxBranches,
            StorageLimitGb = p.StorageLimitGb, IsCustom = p.IsCustom, IsActive = p.IsActive, IsPublic = p.IsPublic,
            SortOrder = p.SortOrder, FeatureIds = p.PlanFeatures.Select(f => f.FeatureId).ToList()
        };
    }

    public async Task<IReadOnlyList<Feature>> GetFeaturesAsync(CancellationToken ct = default) =>
        await db.Features.AsNoTracking().OrderBy(f => f.Tier).ThenBy(f => f.SortOrder).ToListAsync(ct);

    public async Task<Result<int>> CreateAsync(PlanInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.SubscriptionPlans.AnyAsync(p => p.Code == code, ct))
            return Result<int>.Fail($"A plan with code {code} already exists.");
        if (input.ParentPlanId is int parentId && !await db.SubscriptionPlans.AnyAsync(p => p.Id == parentId, ct))
            return Result<int>.Fail("Selected parent plan does not exist.");

        var plan = new SubscriptionPlan { Code = code };
        Map(input, plan);
        var featureIds = await ValidFeatureIds(input.FeatureIds, ct);
        plan.PlanFeatures = featureIds.Select(fid => new PlanFeature { FeatureId = fid }).ToList();
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        featureAccess.InvalidateAll();
        return Result<int>.Ok(plan.Id);
    }

    public async Task<Result> UpdateAsync(int id, PlanInput input, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.Include(p => p.PlanFeatures).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return Result.Fail("Plan not found.");

        if (input.ParentPlanId is int parentId)
        {
            if (parentId == id) return Result.Fail("A plan cannot inherit from itself.");
            if (await CreatesCycleAsync(id, parentId, ct)) return Result.Fail("This parent would create a circular inheritance chain.");
        }

        Map(input, plan); // Code is immutable after creation: it may be referenced by integrations.
        var wanted = await ValidFeatureIds(input.FeatureIds, ct);
        foreach (var pf in plan.PlanFeatures.Where(pf => !wanted.Contains(pf.FeatureId)).ToList())
            plan.PlanFeatures.Remove(pf);
        foreach (var fid in wanted.Where(fid => plan.PlanFeatures.All(pf => pf.FeatureId != fid)))
            plan.PlanFeatures.Add(new PlanFeature { PlanId = id, FeatureId = fid });

        await db.SaveChangesAsync(ct);
        featureAccess.InvalidateAll();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.Include(p => p.PlanFeatures).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return Result.Fail("Plan not found.");
        if (await db.OrganizationSubscriptions.AnyAsync(s => s.PlanId == id, ct))
            return Result.Fail("This plan has (or had) subscriptions. Deactivate it instead of deleting.");
        if (await db.SubscriptionPlans.AnyAsync(p => p.ParentPlanId == id, ct))
            return Result.Fail("Other plans inherit from this plan. Change their parent first.");

        db.PlanFeatures.RemoveRange(plan.PlanFeatures);
        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
        featureAccess.InvalidateAll();
        return Result.Ok();
    }

    public async Task<Result> SetFeatureGlobalStateAsync(int featureId, bool enabled, CancellationToken ct = default)
    {
        var feature = await db.Features.FirstOrDefaultAsync(f => f.Id == featureId, ct);
        if (feature is null) return Result.Fail("Feature not found.");
        feature.IsGloballyEnabled = enabled;
        await db.SaveChangesAsync(ct);
        featureAccess.InvalidateAll();
        return Result.Ok();
    }

    private async Task<bool> CreatesCycleAsync(int planId, int newParentId, CancellationToken ct)
    {
        var parents = await db.SubscriptionPlans.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.ParentPlanId, ct);
        int? cursor = newParentId;
        var guard = 0;
        while (cursor is int c && guard++ < 100)
        {
            if (c == planId) return true;
            cursor = parents.GetValueOrDefault(c);
        }
        return false;
    }

    private async Task<HashSet<int>> ValidFeatureIds(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var valid = await db.Features.Where(f => list.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        return valid.ToHashSet();
    }

    private static void Map(PlanInput i, SubscriptionPlan p)
    {
        p.Name = i.Name.Trim();
        p.Description = i.Description?.Trim();
        p.ParentPlanId = i.ParentPlanId;
        p.MonthlyPrice = i.MonthlyPrice;
        p.YearlyPrice = i.YearlyPrice;
        p.IsPerEmployeePricing = i.IsPerEmployeePricing;
        p.TrialDays = i.TrialDays;
        p.MaxEmployees = i.MaxEmployees;
        p.MaxBranches = i.MaxBranches;
        p.StorageLimitGb = i.StorageLimitGb;
        p.IsCustom = i.IsCustom;
        p.IsActive = i.IsActive;
        p.IsPublic = i.IsPublic;
        p.SortOrder = i.SortOrder;
    }
}
