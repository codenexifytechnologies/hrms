using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Persistence;

/// <summary>
/// Idempotent reference-data seeding: feature catalog, tenant permission catalog, default plans and the
/// first Super Admin. Existing rows are never overwritten, so changes made in the Super Admin panel persist.
/// </summary>
public sealed class DbSeeder(AppDbContext db, ICurrentContext context, IPasswordService passwords, IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        using var scope = context.BeginPlatformScope();
        await SeedFeaturesAsync(ct);
        await SeedPermissionsAsync(ct);
        await SeedPlansAsync(ct);
        await SeedSuperAdminAsync(ct);
        await SeedStatutoryAsync(ct);
        await SeedOrganizationHrDefaultsAsync(ct);
        if (!await db.PlatformSettings.AnyAsync(ct))
        {
            // Supplier defaults; GSTIN, bank, gateway and SMTP are entered in the Control Center.
            db.PlatformSettings.Add(new Domain.Billing.PlatformSetting());
            await db.SaveChangesAsync(ct);
        }
        // Starter drafts of the public policy pages (only added when missing; edits in the Control Center are kept).
        await Application.Support.LegalPageDefaults.EnsureAsync(db, DateOnly.FromDateTime(DateTime.UtcNow), ct);
    }

    /// <summary>Organizations created before Core HR existed get their HR settings and exit checklist.</summary>
    private async Task SeedOrganizationHrDefaultsAsync(CancellationToken ct)
    {
        var missing = await db.Organizations.Where(o => !db.OrganizationSettings.Any(s => s.OrganizationId == o.Id))
            .Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in missing) Application.CoreHr.HrDefaults.AddTo(db, orgId);
        if (missing.Count > 0) await db.SaveChangesAsync(ct);

        // Organizations created before Time & Attendance existed get default leave types and a shift.
        var withoutTime = await db.Organizations.Where(o => !db.Shifts.Any(s => s.OrganizationId == o.Id) && !db.LeaveTypes.Any(t => t.OrganizationId == o.Id))
            .Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in withoutTime) Application.Time.TimeDefaults.AddTo(db, orgId);
        if (withoutTime.Count > 0) await db.SaveChangesAsync(ct);

        // Organizations created before Payroll existed get default components, a structure and settings.
        var withoutPayroll = await db.Organizations.Where(o => !db.PayrollSettings.Any(s => s.OrganizationId == o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in withoutPayroll) Application.Payroll.PayrollDefaults.AddTo(db, orgId);
        if (withoutPayroll.Count > 0) await db.SaveChangesAsync(ct);

        // Organizations created before letters existed get the starting templates (once: deleted templates are not re-added).
        var withoutLetters = await db.Organizations.Where(o => !db.LetterTemplates.IgnoreQueryFilters().Any(t => t.OrganizationId == o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in withoutLetters) Application.Documents.LetterDefaults.AddTo(db, orgId);
        if (withoutLetters.Count > 0) await db.SaveChangesAsync(ct);

        var withoutExpenses = await db.Organizations.Where(o => !db.ExpenseCategories.IgnoreQueryFilters().Any(c => c.OrganizationId == o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in withoutExpenses) Application.Payroll.ExpenseDefaults.AddTo(db, orgId);
        if (withoutExpenses.Count > 0) await db.SaveChangesAsync(ct);

        var withoutOnboarding = await db.Organizations.Where(o => !db.OnboardingTemplates.IgnoreQueryFilters().Any(t => t.OrganizationId == o.Id)).Select(o => o.Id).ToListAsync(ct);
        foreach (var orgId in withoutOnboarding) Application.Strength.OnboardingDefaults.AddTo(db, orgId);
        if (withoutOnboarding.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>Platform-wide statutory reference data, seeded once (then maintained in the Control Center).</summary>
    private async Task SeedStatutoryAsync(CancellationToken ct)
    {
        if (await db.StatutoryRateSets.AnyAsync(ct) || await db.IncomeTaxRules.AnyAsync(ct)) return;
        Application.Payroll.StatutoryDefaults.AddTo(db);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Seeded default statutory rules (PF/ESI/PT/LWF/income tax). They are marked UNVERIFIED — review them in the Control Center.");
    }

    private async Task SeedFeaturesAsync(CancellationToken ct)
    {
        var existing = await db.Features.Select(f => f.Code).ToListAsync(ct);
        var order = 0;
        foreach (var f in FeatureCatalog.All)
        {
            order += 10;
            if (existing.Contains(f.Code)) continue;
            db.Features.Add(new Feature { Code = f.Code, Name = f.Name, ModuleGroup = f.ModuleGroup, Tier = f.Tier, Description = f.Description, SortOrder = order });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var added = new List<Permission>();
        foreach (var p in TenantPermissions.All.Where(p => !existing.Contains(p.Code)))
        {
            var permission = new Permission { Code = p.Code, Name = p.Name, Module = p.Module, FeatureCode = p.FeatureCode };
            db.Permissions.Add(permission);
            added.Add(permission);
        }
        await db.SaveChangesAsync(ct);
        if (added.Count == 0 || existing.Count == 0) return;

        // Permissions introduced by a new release are granted once to existing organizations' system roles
        // that include them by default. Only brand-new codes are granted, so earlier admin removals stay removed.
        var systemRoles = await db.Roles.Where(r => r.IsSystem).Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        foreach (var role in systemRoles)
        {
            if (!TenantPermissions.SystemRoles.TryGetValue(role.Name, out var defaults)) continue;
            foreach (var p in added.Where(p => defaults.Contains(p.Code)))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPlansAsync(CancellationToken ct)
    {
        if (await db.SubscriptionPlans.AnyAsync(ct)) return;
        var features = await db.Features.ToListAsync(ct);
        List<PlanFeature> Tier(FeatureTier tier) => features.Where(f => f.Tier == tier).Select(f => new PlanFeature { FeatureId = f.Id }).ToList();

        // Starting values only; pricing and limits are managed from the Super Admin panel.
        var foundation = new SubscriptionPlan
        {
            Code = "FOUNDATION", Name = "Foundation", Description = "Essential HR, attendance, leave, payroll and self-service.",
            MonthlyPrice = 99, YearlyPrice = 1069, TrialDays = 14, MaxEmployees = 100, MaxBranches = 1, StorageLimitGb = 10, SortOrder = 10,
            PlanFeatures = Tier(FeatureTier.Foundation)
        };
        db.SubscriptionPlans.Add(foundation);
        await db.SaveChangesAsync(ct);

        var strength = new SubscriptionPlan
        {
            Code = "STRENGTH", Name = "Strength", Description = "Advanced automation: geo attendance, assets, SSO, analytics, surveys.",
            ParentPlanId = foundation.Id, MonthlyPrice = 149, YearlyPrice = 1609, TrialDays = 14, MaxEmployees = 500, MaxBranches = 5, StorageLimitGb = 50, SortOrder = 20,
            PlanFeatures = Tier(FeatureTier.Strength)
        };
        db.SubscriptionPlans.Add(strength);
        await db.SaveChangesAsync(ct);

        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Code = "GROWTH", Name = "Growth", Description = "Performance and business management: OKRs, reviews, workflows, skills.",
            ParentPlanId = strength.Id, MonthlyPrice = 199, YearlyPrice = 2149, TrialDays = 14, MaxEmployees = 2000, MaxBranches = 20, StorageLimitGb = 200, SortOrder = 30,
            PlanFeatures = Tier(FeatureTier.Growth)
        });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Code = "ENTERPRISE", Name = "Custom Enterprise", Description = "Tailored modules, limits and pricing for large organizations.",
            MonthlyPrice = 0, YearlyPrice = 0, TrialDays = 30, MaxEmployees = 100000, MaxBranches = 1000, StorageLimitGb = 1000, SortOrder = 40,
            IsCustom = true, IsPublic = false,
            PlanFeatures = features.Select(f => new PlanFeature { FeatureId = f.Id }).ToList()
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSuperAdminAsync(CancellationToken ct)
    {
        if (await db.PlatformUsers.AnyAsync(ct)) return;
        var email = config["Seed:SuperAdminEmail"];
        var password = config["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No platform users exist and Seed:SuperAdminEmail / Seed:SuperAdminPassword are not configured. " +
                              "Set them (e.g. as environment variables Seed__SuperAdminEmail / Seed__SuperAdminPassword) and restart.");
            return;
        }
        db.PlatformUsers.Add(new PlatformUser
        {
            Email = email.Trim().ToLowerInvariant(),
            FullName = config["Seed:SuperAdminName"] ?? "Platform Owner",
            PasswordHash = passwords.Hash(password),
            Role = PlatformRole.SuperAdmin,
            MustChangePassword = true
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded initial Super Admin {Email}. The password must be changed at first sign-in.", email);
    }
}
