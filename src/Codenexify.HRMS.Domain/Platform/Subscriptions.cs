using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Domain.Platform;

/// <summary>A switchable module or capability. Codes are stable identifiers referenced by code.</summary>
public class Feature : BaseEntity, IAuditable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ModuleGroup { get; set; } = string.Empty;
    public FeatureTier Tier { get; set; }
    public bool IsGloballyEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Commercial plan. Plans form an inheritance chain through <see cref="ParentPlanId"/>.</summary>
public class SubscriptionPlan : BaseEntity, IAuditable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentPlanId { get; set; }
    public SubscriptionPlan? ParentPlan { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    /// <summary>Price basis: true = per employee per period, false = flat per organization.</summary>
    public bool IsPerEmployeePricing { get; set; } = true;
    public int TrialDays { get; set; } = 14;
    public int MaxEmployees { get; set; }
    public int MaxBranches { get; set; }
    public int StorageLimitGb { get; set; }
    public bool IsCustom { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}

public class PlanFeature
{
    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public int FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;
}

/// <summary>An organization's subscription period. Only one row per organization has IsCurrent = true.</summary>
public class OrganizationSubscription : BaseEntity, IAuditable
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public SubscriptionStatus Status { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>Set only on trial subscriptions.</summary>
    public DateOnly? TrialEndsOn { get; set; }
    /// <summary>Last day of access while payment is overdue.</summary>
    public DateOnly? GraceEndsOn { get; set; }
    public DateOnly? PausedOn { get; set; }
    public DateOnly? CancelledOn { get; set; }
    public string? CancellationReason { get; set; }
    public bool AutoRenew { get; set; } = true;
    /// <summary>Agreed price per billing period (per employee if the plan is per-employee).</summary>
    public decimal Price { get; set; }
    public int? MaxEmployeesOverride { get; set; }
    public int? MaxBranchesOverride { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Whether the subscription currently grants access on <paramref name="today"/>.</summary>
    public bool GrantsAccess(DateOnly today) => Status switch
    {
        SubscriptionStatus.Trial => (TrialEndsOn ?? EndDate) >= today,
        SubscriptionStatus.Active => EndDate >= today,
        SubscriptionStatus.PastDue => (GraceEndsOn ?? EndDate) >= today,
        _ => false
    };

    public bool IsTrial => TrialEndsOn.HasValue;

    /// <summary>Monthly-equivalent recurring revenue for this subscription at a given seat count.</summary>
    public decimal MonthlyRecurringRevenue(int seats, bool perEmployee)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue)) return 0m;
        var periodPrice = perEmployee ? Price * Math.Max(seats, 1) : Price;
        return BillingCycle == BillingCycle.Yearly ? Math.Round(periodPrice / 12m, 2) : periodPrice;
    }
}

/// <summary>Per-organization add-on (enable) or restriction (disable) that wins over the plan.</summary>
public class OrganizationFeatureOverride : BaseEntity, IAuditable
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public string? Reason { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}
