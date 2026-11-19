using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class SubscriptionEngineTests : IAsyncLifetime
{
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.CreateAsync();
    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<IReadOnlySet<string>> FeaturesOf(int orgId) =>
        _host.RunAsync(sp => sp.GetRequiredService<IFeatureAccessService>().GetEnabledFeaturesAsync(orgId));

    private Task<T> AsPlatform<T>(Func<IServiceProvider, Task<T>> action)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(action);
    }

    private void ClearCache() => _host.Services.GetRequiredService<IFeatureAccessService>().InvalidateAll();

    [Fact]
    public async Task Plan_inheritance_growth_includes_strength_and_foundation()
    {
        var foundation = await _host.ProvisionAsync("found", "FOUNDATION");
        var strength = await _host.ProvisionAsync("stren", "STRENGTH");
        var growth = await _host.ProvisionAsync("growt", "GROWTH");

        var f = await FeaturesOf(foundation);
        var s = await FeaturesOf(strength);
        var g = await FeaturesOf(growth);

        Assert.Contains(FeatureCodes.Payroll, f);
        Assert.DoesNotContain(FeatureCodes.GeoFencing, f);
        Assert.DoesNotContain(FeatureCodes.Okrs, f);

        Assert.True(f.IsSubsetOf(s));
        Assert.Contains(FeatureCodes.GeoFencing, s);
        Assert.DoesNotContain(FeatureCodes.Okrs, s);

        Assert.True(s.IsSubsetOf(g));
        Assert.Contains(FeatureCodes.Okrs, g);
        Assert.DoesNotContain(FeatureCodes.Recruitment, g); // add-ons are not part of standard plans
    }

    [Fact]
    public async Task Add_on_override_enables_and_restriction_disables_features()
    {
        var org = await _host.ProvisionAsync("addon", "FOUNDATION");
        var (recruitment, payroll) = await AsPlatform(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (await db.Features.SingleAsync(x => x.Code == FeatureCodes.Recruitment), await db.Features.SingleAsync(x => x.Code == FeatureCodes.Payroll));
        });

        var orgs = (Func<IServiceProvider, IOrganizationService>)(sp => sp.GetRequiredService<IOrganizationService>());
        Assert.True((await AsPlatform(sp => orgs(sp).SetFeatureOverrideAsync(org, recruitment.Id, OverrideMode.Enable, "Recruitment add-on", null))).Succeeded);
        Assert.True((await AsPlatform(sp => orgs(sp).SetFeatureOverrideAsync(org, payroll.Id, OverrideMode.Disable, "Uses external payroll", null))).Succeeded);

        var enabled = await FeaturesOf(org);
        Assert.Contains(FeatureCodes.Recruitment, enabled);
        Assert.DoesNotContain(FeatureCodes.Payroll, enabled);

        // Override expiry: an add-on that expired yesterday no longer applies.
        Assert.True((await AsPlatform(sp => orgs(sp).SetFeatureOverrideAsync(org, recruitment.Id, OverrideMode.Enable, "Short add-on", _host.Clock.Today.AddDays(2)))).Succeeded);
        _host.Clock.AdvanceDays(3);
        ClearCache();
        Assert.DoesNotContain(FeatureCodes.Recruitment, await FeaturesOf(org));
    }

    [Fact]
    public async Task Globally_disabled_feature_is_off_for_everyone()
    {
        var org = await _host.ProvisionAsync("glob", "GROWTH");
        var okrId = await AsPlatform(sp => sp.GetRequiredService<AppDbContext>().Features.Where(f => f.Code == FeatureCodes.Okrs).Select(f => f.Id).SingleAsync());
        Assert.True((await AsPlatform(sp => sp.GetRequiredService<IPlanService>().SetFeatureGlobalStateAsync(okrId, false))).Succeeded);
        Assert.DoesNotContain(FeatureCodes.Okrs, await FeaturesOf(org));
    }

    [Fact]
    public async Task Suspended_organization_loses_all_features()
    {
        var org = await _host.ProvisionAsync("susp");
        Assert.NotEmpty(await FeaturesOf(org));
        var r = await AsPlatform(sp => sp.GetRequiredService<IOrganizationService>().ChangeStatusAsync(org, OrganizationStatus.Suspended, "Non-payment"));
        Assert.True(r.Succeeded, r.Error);
        Assert.Empty(await FeaturesOf(org));
    }

    [Fact]
    public async Task Trial_expires_after_trial_days_and_blocks_modules()
    {
        var org = await _host.ProvisionAsync("trial", "FOUNDATION", trial: true);
        Assert.NotEmpty(await FeaturesOf(org));

        _host.Clock.AdvanceDays(14); // 14-day trial: day 15 is past the end
        var changed = await AsPlatform(sp => sp.GetRequiredService<ISubscriptionService>().ProcessLifecycleAsync());
        Assert.Equal(1, changed);
        ClearCache();

        var (orgStatus, subStatus) = await AsPlatform(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return ((await db.Organizations.SingleAsync(o => o.Id == org)).Status,
                    (await db.OrganizationSubscriptions.SingleAsync(s => s.OrganizationId == org && s.IsCurrent)).Status);
        });
        Assert.Equal(OrganizationStatus.Expired, orgStatus);
        Assert.Equal(SubscriptionStatus.Expired, subStatus);
        Assert.Empty(await FeaturesOf(org));
    }

    [Fact]
    public async Task Unpaid_renewal_gets_grace_period_then_expires()
    {
        var org = await _host.ProvisionAsync("grace", trial: false);
        var svc = (Func<IServiceProvider, ISubscriptionService>)(sp => sp.GetRequiredService<ISubscriptionService>());
        var end = (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!.EndDate;

        _host.Clock.UtcNow = end.AddDays(1).ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
        await AsPlatform(sp => svc(sp).ProcessLifecycleAsync());
        ClearCache();
        Assert.Equal(SubscriptionStatus.PastDue, (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!.Status);
        Assert.NotEmpty(await FeaturesOf(org)); // still inside grace

        _host.Clock.AdvanceDays(SubscriptionService.GraceDays + 1);
        await AsPlatform(sp => svc(sp).ProcessLifecycleAsync());
        ClearCache();
        Assert.Equal(SubscriptionStatus.Expired, (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!.Status);
        Assert.Empty(await FeaturesOf(org));

        // Extending an expired subscription restores access.
        Assert.True((await AsPlatform(sp => svc(sp).ExtendAsync(org, 30, "Payment received late"))).Succeeded);
        Assert.NotEmpty(await FeaturesOf(org));
    }

    [Fact]
    public async Task Pause_blocks_access_and_resume_returns_paused_days()
    {
        var org = await _host.ProvisionAsync("pause", trial: false);
        var svc = (Func<IServiceProvider, ISubscriptionService>)(sp => sp.GetRequiredService<ISubscriptionService>());
        var originalEnd = (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!.EndDate;

        Assert.True((await AsPlatform(sp => svc(sp).PauseAsync(org, "Seasonal shutdown"))).Succeeded);
        Assert.Empty(await FeaturesOf(org));

        _host.Clock.AdvanceDays(5);
        Assert.True((await AsPlatform(sp => svc(sp).ResumeAsync(org))).Succeeded);
        var sub = (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!;
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Equal(originalEnd.AddDays(5), sub.EndDate);
        Assert.NotEmpty(await FeaturesOf(org));
    }

    [Fact]
    public async Task Upgrade_keeps_period_records_history_and_changes_modules_immediately()
    {
        var org = await _host.ProvisionAsync("upgr", "FOUNDATION", trial: false);
        var growthId = await _host.PlanIdAsync("GROWTH");
        var svc = (Func<IServiceProvider, ISubscriptionService>)(sp => sp.GetRequiredService<ISubscriptionService>());
        var before = (await AsPlatform(sp => svc(sp).GetCurrentAsync(org)))!;

        var r = await AsPlatform(sp => svc(sp).ChangePlanAsync(org, growthId, null));
        Assert.True(r.Succeeded, r.Error);

        var history = await AsPlatform(sp => svc(sp).GetHistoryAsync(org));
        Assert.Equal(2, history.Count);
        Assert.Single(history, h => h.IsCurrent);
        var current = history.Single(h => h.IsCurrent);
        Assert.Equal("GROWTH", current.Plan.Code);
        Assert.Equal(before.EndDate, current.EndDate);
        Assert.Equal(SubscriptionStatus.Active, current.Status);
        Assert.Contains(FeatureCodes.Okrs, await FeaturesOf(org));
    }

    [Fact]
    public async Task Trial_conversion_to_paid_starts_new_period()
    {
        var org = await _host.ProvisionAsync("convert", "STRENGTH", trial: true);
        var planId = await _host.PlanIdAsync("STRENGTH");
        var r = await AsPlatform(sp => sp.GetRequiredService<ISubscriptionService>().ActivateAsync(org,
            new ActivateSubscriptionInput { PlanId = planId, BillingCycle = BillingCycle.Yearly, Price = 1500m }));
        Assert.True(r.Succeeded, r.Error);

        var (sub, orgStatus) = await AsPlatform(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return (await db.OrganizationSubscriptions.SingleAsync(s => s.OrganizationId == org && s.IsCurrent),
                    (await db.Organizations.SingleAsync(o => o.Id == org)).Status);
        });
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Null(sub.TrialEndsOn);
        Assert.Equal(_host.Clock.Today.AddYears(1).AddDays(-1), sub.EndDate);
        Assert.Equal(1500m, sub.Price);
        Assert.Equal(OrganizationStatus.Active, orgStatus);
    }

    [Fact]
    public async Task Plans_cannot_form_inheritance_cycles_and_used_plans_cannot_be_deleted()
    {
        await _host.ProvisionAsync("cycle", "GROWTH");
        var foundationId = await _host.PlanIdAsync("FOUNDATION");
        var growthId = await _host.PlanIdAsync("GROWTH");
        var plans = (Func<IServiceProvider, IPlanService>)(sp => sp.GetRequiredService<IPlanService>());

        var input = (await AsPlatform(sp => plans(sp).GetForEditAsync(foundationId)))!;
        input.ParentPlanId = growthId;
        var cyc = await AsPlatform(sp => plans(sp).UpdateAsync(foundationId, input));
        Assert.False(cyc.Succeeded);

        var del = await AsPlatform(sp => plans(sp).DeleteAsync(growthId));
        Assert.False(del.Succeeded);
    }

    [Fact]
    public async Task User_creation_respects_plan_employee_limit()
    {
        var org = await _host.ProvisionAsync("limit");
        Assert.True((await AsPlatform(sp => sp.GetRequiredService<IOrganizationService>().UpdateLimitsAsync(org, new OrganizationLimitsInput { MaxEmployees = 1 }))).Succeeded);

        _host.Context.AsTenant(org);
        var roleId = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Roles.Where(r => r.Name == "Employee").Select(r => r.Id).SingleAsync());
        var result = await _host.RunAsync(sp => sp.GetRequiredService<Application.Tenancy.ITenantAdminService>().CreateUserAsync(
            new Application.Tenancy.TenantUserInput { FullName = "Second", Email = "second@limit.test", Password = "Passw0rd!x", RoleIds = [roleId] }));
        Assert.False(result.Succeeded);
        Assert.Contains("upgrade", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
