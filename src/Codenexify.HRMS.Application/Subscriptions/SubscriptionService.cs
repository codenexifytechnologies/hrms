using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Subscriptions;

public sealed class ActivateSubscriptionInput
{
    [Required, Display(Name = "Plan")]
    public int PlanId { get; set; }
    [Display(Name = "Billing cycle")]
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    [Range(0, 100_000_000), Display(Name = "Agreed price per period (blank = plan price)")]
    public decimal? Price { get; set; }
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }
    [Display(Name = "Auto renew")]
    public bool AutoRenew { get; set; } = true;
    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class SubscriptionTermsInput
{
    [Range(0, 100_000_000)]
    public decimal Price { get; set; }
    [Display(Name = "Renewal / end date")]
    public DateOnly EndDate { get; set; }
    [Display(Name = "Auto renew")]
    public bool AutoRenew { get; set; }
    [Range(1, 1_000_000), Display(Name = "Employee limit override")]
    public int? MaxEmployeesOverride { get; set; }
    [Range(1, 10_000), Display(Name = "Branch limit override")]
    public int? MaxBranchesOverride { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed record EffectiveLimits(int MaxEmployees, int MaxBranches, int StorageLimitGb);

public interface ISubscriptionService
{
    Task<OrganizationSubscription?> GetCurrentAsync(int organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationSubscription>> GetHistoryAsync(int organizationId, CancellationToken ct = default);
    Task<EffectiveLimits> GetLimitsAsync(int organizationId, CancellationToken ct = default);

    /// <summary>Creates the first subscription of a newly provisioned organization.</summary>
    OrganizationSubscription CreateInitial(Organization org, SubscriptionPlan plan, bool asTrial, BillingCycle cycle, DateOnly start);

    /// <summary>Starts a new paid period (trial conversion, renewal, or a plan change with a fresh period).</summary>
    Task<Result> ActivateAsync(int organizationId, ActivateSubscriptionInput input, CancellationToken ct = default);
    /// <summary>Upgrade/downgrade keeping the current period and status.</summary>
    Task<Result> ChangePlanAsync(int organizationId, int newPlanId, decimal? price, CancellationToken ct = default);
    Task<Result> ExtendAsync(int organizationId, int days, string reason, CancellationToken ct = default);
    Task<Result> PauseAsync(int organizationId, string reason, CancellationToken ct = default);
    Task<Result> ResumeAsync(int organizationId, CancellationToken ct = default);
    Task<Result> CancelAsync(int organizationId, string reason, bool immediately, CancellationToken ct = default);
    Task<Result> UpdateTermsAsync(int organizationId, SubscriptionTermsInput input, CancellationToken ct = default);
    /// <summary>Extends the current paid subscription by one billing cycle (a renewal was paid).</summary>
    Task<Result> RenewAsync(int organizationId, string reference, CancellationToken ct = default);

    /// <summary>Moves lapsed trials/periods to PastDue/Expired. Returns the number of subscriptions changed.</summary>
    Task<int> ProcessLifecycleAsync(CancellationToken ct = default);
}

public sealed class SubscriptionService(
    IAppDbContext db, IClock clock, IFeatureAccessService featureAccess, IAuditWriter audit) : ISubscriptionService
{
    /// <summary>Days of continued access after a paid period ends without renewal.</summary>
    public const int GraceDays = 7;

    public Task<OrganizationSubscription?> GetCurrentAsync(int organizationId, CancellationToken ct = default) =>
        db.OrganizationSubscriptions.Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.IsCurrent, ct);

    public async Task<IReadOnlyList<OrganizationSubscription>> GetHistoryAsync(int organizationId, CancellationToken ct = default) =>
        await db.OrganizationSubscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.Id).ToListAsync(ct);

    public async Task<EffectiveLimits> GetLimitsAsync(int organizationId, CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == organizationId, ct);
        var sub = await db.OrganizationSubscriptions.AsNoTracking().Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.IsCurrent, ct);
        return new EffectiveLimits(
            org.MaxEmployeesOverride ?? sub?.MaxEmployeesOverride ?? sub?.Plan.MaxEmployees ?? 0,
            org.MaxBranchesOverride ?? sub?.MaxBranchesOverride ?? sub?.Plan.MaxBranches ?? 0,
            org.StorageLimitGbOverride ?? sub?.Plan.StorageLimitGb ?? 0);
    }

    public OrganizationSubscription CreateInitial(Organization org, SubscriptionPlan plan, bool asTrial, BillingCycle cycle, DateOnly start)
    {
        var sub = new OrganizationSubscription
        {
            Organization = org,
            Plan = plan,
            PlanId = plan.Id,
            BillingCycle = cycle,
            StartDate = start,
            IsCurrent = true,
            Price = cycle == BillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice
        };
        if (asTrial && plan.TrialDays > 0)
        {
            sub.Status = SubscriptionStatus.Trial;
            sub.TrialEndsOn = start.AddDays(plan.TrialDays - 1);
            sub.EndDate = sub.TrialEndsOn.Value;
            org.Status = OrganizationStatus.Trial;
        }
        else
        {
            sub.Status = SubscriptionStatus.Active;
            sub.EndDate = PeriodEnd(start, cycle);
            org.Status = OrganizationStatus.Active;
        }
        org.StatusChangedAtUtc = clock.UtcNow;
        db.OrganizationSubscriptions.Add(sub);
        return sub;
    }

    public async Task<Result> ActivateAsync(int organizationId, ActivateSubscriptionInput input, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null) return Result.Fail("Organization not found.");
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == input.PlanId, ct);
        if (plan is null) return Result.Fail("Plan not found.");
        if (!plan.IsActive) return Result.Fail("This plan is inactive.");

        var current = await GetCurrentAsync(organizationId, ct);
        var start = input.StartDate ?? clock.Today;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (current is not null)
        {
            current.IsCurrent = false;
            if (current.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Paused)
            {
                current.Status = SubscriptionStatus.Cancelled;
                current.CancelledOn = clock.Today;
                current.CancellationReason = $"Superseded by {plan.Name} ({input.BillingCycle}) from {start:dd-MMM-yyyy}";
            }
            // Retire first: only one row per organization may be current (filtered unique index).
            await db.SaveChangesAsync(ct);
        }

        var sub = new OrganizationSubscription
        {
            OrganizationId = organizationId,
            PlanId = plan.Id,
            BillingCycle = input.BillingCycle,
            Status = SubscriptionStatus.Active,
            StartDate = start,
            EndDate = PeriodEnd(start, input.BillingCycle),
            Price = input.Price ?? (input.BillingCycle == BillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice),
            AutoRenew = input.AutoRenew,
            MaxEmployeesOverride = current?.MaxEmployeesOverride,
            MaxBranchesOverride = current?.MaxBranchesOverride,
            Notes = input.Notes,
            IsCurrent = true
        };
        db.OrganizationSubscriptions.Add(sub);
        SyncOrganizationStatus(org, sub);
        audit.Add(current?.PlanId == plan.Id ? "Subscription.Renew" : "Subscription.Activate", nameof(Organization),
            organizationId.ToString(), $"{plan.Name} {input.BillingCycle} {sub.StartDate:dd-MMM-yyyy}–{sub.EndDate:dd-MMM-yyyy} @ {sub.Price:0.00}", organizationId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> ChangePlanAsync(int organizationId, int newPlanId, decimal? price, CancellationToken ct = default)
    {
        var current = await GetCurrentAsync(organizationId, ct);
        if (current is null) return Result.Fail("Organization has no subscription. Activate one first.");
        if (current.PlanId == newPlanId) return Result.Fail("Organization is already on this plan.");
        if (current.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            return Result.Fail("The current subscription has ended. Use Activate to start a new period.");
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == newPlanId && p.IsActive, ct);
        if (plan is null) return Result.Fail("Plan not found or inactive.");

        var direction = plan.MonthlyPrice >= current.Plan.MonthlyPrice ? "Upgrade" : "Downgrade";
        var oldPlanName = current.Plan.Name;
        var previousStatus = current.Status;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        current.IsCurrent = false;
        current.Status = SubscriptionStatus.Cancelled;
        current.CancelledOn = clock.Today;
        current.CancellationReason = $"{direction} to {plan.Name}";
        await db.SaveChangesAsync(ct); // retire first (filtered unique index on current row)

        var next = new OrganizationSubscription
        {
            OrganizationId = organizationId,
            PlanId = plan.Id,
            BillingCycle = current.BillingCycle,
            Status = previousStatus,
            StartDate = clock.Today,
            EndDate = current.EndDate,
            TrialEndsOn = current.TrialEndsOn,
            GraceEndsOn = current.GraceEndsOn,
            PausedOn = current.PausedOn,
            Price = price ?? (current.BillingCycle == BillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice),
            AutoRenew = current.AutoRenew,
            MaxEmployeesOverride = current.MaxEmployeesOverride,
            MaxBranchesOverride = current.MaxBranchesOverride,
            IsCurrent = true
        };
        db.OrganizationSubscriptions.Add(next);
        audit.Add($"Subscription.{direction}", nameof(Organization), organizationId.ToString(), $"{oldPlanName} → {plan.Name}", organizationId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> ExtendAsync(int organizationId, int days, string reason, CancellationToken ct = default)
    {
        if (days is < 1 or > 3650) return Result.Fail("Extension must be between 1 and 3650 days.");
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null) return Result.Fail("Organization has no subscription.");
        if (sub.Status is SubscriptionStatus.Cancelled) return Result.Fail("Cancelled subscriptions cannot be extended. Activate a new one.");

        var baseDate = sub.EndDate < clock.Today ? clock.Today : sub.EndDate;
        sub.EndDate = baseDate.AddDays(days);
        sub.GraceEndsOn = null;
        if (sub.IsTrial)
        {
            sub.TrialEndsOn = sub.EndDate;
            sub.Status = SubscriptionStatus.Trial;
        }
        else if (sub.Status is SubscriptionStatus.Expired or SubscriptionStatus.PastDue)
        {
            sub.Status = SubscriptionStatus.Active;
        }

        var org = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
        SyncOrganizationStatus(org, sub);
        audit.Add("Subscription.Extend", nameof(Organization), organizationId.ToString(), $"+{days} days to {sub.EndDate:dd-MMM-yyyy}. {reason}", organizationId);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> PauseAsync(int organizationId, string reason, CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null) return Result.Fail("Organization has no subscription.");
        if (sub.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.PastDue))
            return Result.Fail($"A {sub.Status} subscription cannot be paused.");
        sub.Status = SubscriptionStatus.Paused;
        sub.PausedOn = clock.Today;
        audit.Add("Subscription.Pause", nameof(Organization), organizationId.ToString(), reason, organizationId);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> ResumeAsync(int organizationId, CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null || sub.Status != SubscriptionStatus.Paused || sub.PausedOn is null)
            return Result.Fail("Only a paused subscription can be resumed.");

        // The paused days are given back so the customer does not lose paid time.
        var pausedDays = Math.Max(0, clock.Today.DayNumber - sub.PausedOn.Value.DayNumber);
        sub.EndDate = sub.EndDate.AddDays(pausedDays);
        if (sub.IsTrial) sub.TrialEndsOn = sub.EndDate;
        sub.Status = sub.IsTrial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
        sub.PausedOn = null;
        var org = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
        SyncOrganizationStatus(org, sub);
        audit.Add("Subscription.Resume", nameof(Organization), organizationId.ToString(), $"Paused {pausedDays} day(s); new end {sub.EndDate:dd-MMM-yyyy}", organizationId);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int organizationId, string reason, bool immediately, CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null) return Result.Fail("Organization has no subscription.");
        if (sub.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired) return Result.Fail("Subscription has already ended.");

        sub.CancellationReason = reason;
        sub.AutoRenew = false;
        if (immediately)
        {
            sub.Status = SubscriptionStatus.Cancelled;
            sub.CancelledOn = clock.Today;
            sub.EndDate = clock.Today;
            var org = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
            SyncOrganizationStatus(org, sub);
        }
        audit.Add(immediately ? "Subscription.CancelNow" : "Subscription.CancelAtPeriodEnd", nameof(Organization), organizationId.ToString(), reason, organizationId);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> UpdateTermsAsync(int organizationId, SubscriptionTermsInput input, CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null) return Result.Fail("Organization has no subscription.");
        if (input.EndDate < sub.StartDate) return Result.Fail("End date cannot be before the start date.");

        sub.Price = input.Price;
        sub.EndDate = input.EndDate;
        if (sub.IsTrial) sub.TrialEndsOn = input.EndDate;
        sub.AutoRenew = input.AutoRenew;
        sub.MaxEmployeesOverride = input.MaxEmployeesOverride;
        sub.MaxBranchesOverride = input.MaxBranchesOverride;
        sub.Notes = input.Notes;
        if (sub.Status is SubscriptionStatus.Expired or SubscriptionStatus.PastDue && input.EndDate >= clock.Today)
        {
            sub.Status = sub.IsTrial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
            sub.GraceEndsOn = null;
            var org = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
            SyncOrganizationStatus(org, sub);
        }
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<Result> RenewAsync(int organizationId, string reference, CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(organizationId, ct);
        if (sub is null) return Result.Fail("Organization has no subscription.");
        if (sub.IsTrial || sub.Status is SubscriptionStatus.Cancelled) return Result.Fail("Only a paid subscription can be renewed; activate a new one instead.");
        var from = sub.EndDate;
        sub.EndDate = sub.BillingCycle == BillingCycle.Yearly ? sub.EndDate.AddYears(1) : sub.EndDate.AddMonths(1);
        sub.GraceEndsOn = null;
        if (sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Expired) sub.Status = SubscriptionStatus.Active;
        var org = await db.Organizations.FirstAsync(o => o.Id == organizationId, ct);
        SyncOrganizationStatus(org, sub);
        audit.Add("Subscription.Renew", nameof(Organization), organizationId.ToString(), $"{from:dd-MMM-yyyy} → {sub.EndDate:dd-MMM-yyyy} ({reference})", organizationId);
        await db.SaveChangesAsync(ct);
        featureAccess.Invalidate(organizationId);
        return Result.Ok();
    }

    public async Task<int> ProcessLifecycleAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var candidates = await db.OrganizationSubscriptions
            .Include(s => s.Organization)
            .Where(s => s.IsCurrent &&
                ((s.Status == SubscriptionStatus.Trial && s.EndDate < today) ||
                 (s.Status == SubscriptionStatus.Active && s.EndDate < today) ||
                 (s.Status == SubscriptionStatus.PastDue && (s.GraceEndsOn ?? s.EndDate) < today)))
            .ToListAsync(ct);

        foreach (var sub in candidates)
        {
            if (sub.Status == SubscriptionStatus.Active && sub.AutoRenew)
            {
                // Renewal payment is pending: keep access for the grace window.
                sub.Status = SubscriptionStatus.PastDue;
                sub.GraceEndsOn = sub.EndDate.AddDays(GraceDays);
                if (sub.GraceEndsOn >= today) { audit.Add("Subscription.PastDue", nameof(Organization), sub.OrganizationId.ToString(), $"Grace until {sub.GraceEndsOn:dd-MMM-yyyy}", sub.OrganizationId); continue; }
            }
            sub.Status = SubscriptionStatus.Expired;
            SyncOrganizationStatus(sub.Organization, sub);
            audit.Add("Subscription.Expire", nameof(Organization), sub.OrganizationId.ToString(), $"Ended {sub.EndDate:dd-MMM-yyyy}", sub.OrganizationId);
        }

        if (candidates.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var s in candidates) featureAccess.Invalidate(s.OrganizationId);
        }
        return candidates.Count;
    }

    private static DateOnly PeriodEnd(DateOnly start, BillingCycle cycle) =>
        (cycle == BillingCycle.Yearly ? start.AddYears(1) : start.AddMonths(1)).AddDays(-1);

    /// <summary>Organization status follows the subscription unless an administrator has suspended/deactivated it.</summary>
    private void SyncOrganizationStatus(Organization org, OrganizationSubscription sub)
    {
        if (org.Status is OrganizationStatus.Suspended or OrganizationStatus.Inactive or OrganizationStatus.PendingVerification) return;
        var target = sub.Status switch
        {
            SubscriptionStatus.Trial => OrganizationStatus.Trial,
            SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Paused => OrganizationStatus.Active,
            _ => OrganizationStatus.Expired
        };
        if (org.Status != target)
        {
            org.Status = target;
            org.StatusChangedAtUtc = clock.UtcNow;
        }
    }
}
