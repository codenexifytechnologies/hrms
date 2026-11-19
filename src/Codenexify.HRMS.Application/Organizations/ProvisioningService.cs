using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Organizations;

/// <summary>Everything needed to create a client organization and its first administrator.</summary>
public class OrganizationRegistrationInput
{
    [Required, StringLength(150), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 3), RegularExpression("^[a-z0-9][a-z0-9-]*[a-z0-9]$", ErrorMessage = "Use lower-case letters, digits and hyphens.")]
    [Display(Name = "Company code (used at login)")]
    public string Code { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200), Display(Name = "Company email")]
    public string CompanyEmail { get; set; } = string.Empty;
    [Phone, StringLength(20)]
    public string? Phone { get; set; }
    [StringLength(100)]
    public string? Industry { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? State { get; set; }

    [Required, Display(Name = "Plan")]
    public int PlanId { get; set; }
    [Display(Name = "Billing cycle")]
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    [Required, StringLength(150), Display(Name = "Administrator name")]
    public string AdminName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200), Display(Name = "Administrator email")]
    public string AdminEmail { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Administrator password")]
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Required for self-registration (checked in the service); not used when Codenexify staff create the workspace.</summary>
    [Display(Name = "I agree to the Terms of Service and Privacy Policy")]
    public bool AcceptTerms { get; set; }
}

public interface IProvisioningService
{
    /// <param name="startAsTrial">Self-registration always starts on trial; the Super Admin may start paid.</param>
    Task<Result<int>> ProvisionAsync(OrganizationRegistrationInput input, bool startAsTrial, CancellationToken ct = default);
    Task<bool> IsCodeAvailableAsync(string code, CancellationToken ct = default);
}

public sealed partial class ProvisioningService(
    IAppDbContext db, ICurrentContext context, IClock clock, IPasswordService passwords,
    ISubscriptionService subscriptions, IAuditWriter audit, Support.ILegalPageService legal) : IProvisioningService
{
    private static readonly HashSet<string> ReservedCodes = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "superadmin", "api", "www", "app", "login", "register", "support", "codenexify", "org", "me" };

    public async Task<bool> IsCodeAvailableAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim().ToLowerInvariant();
        if (ReservedCodes.Contains(code) || !CodeRegex().IsMatch(code)) return false;
        // Deleted organizations keep their code reserved so old links/logins can never hit a new tenant.
        return !await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Code == code, ct);
    }

    public async Task<Result<int>> ProvisionAsync(OrganizationRegistrationInput input, bool startAsTrial, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToLowerInvariant();
        if (!await IsCodeAvailableAsync(code, ct)) return Result<int>.Fail("This company code is not available. Please choose another.");
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == input.PlanId && p.IsActive, ct);
        if (plan is null) return Result<int>.Fail("Please select a valid plan.");
        if (!context.IsPlatform && (!plan.IsPublic || plan.IsCustom)) return Result<int>.Fail("This plan is not available for self registration.");
        if (!context.IsPlatform && !input.AcceptTerms) return Result<int>.Fail("Please accept the Terms of Service and Privacy Policy to continue.");
        var acceptedVersions = context.IsPlatform ? null : await legal.AcceptanceVersionsAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var org = new Organization
        {
            Code = code,
            Name = input.CompanyName.Trim(),
            LegalName = input.CompanyName.Trim(),
            Email = input.CompanyEmail.Trim().ToLowerInvariant(),
            Phone = input.Phone?.Trim(),
            Industry = input.Industry?.Trim(),
            City = input.City?.Trim(),
            State = input.State?.Trim(),
            TermsAcceptedAtUtc = context.IsPlatform ? null : clock.UtcNow,
            TermsAcceptedVersions = acceptedVersions,
            TermsAcceptedIp = context.IsPlatform ? null : context.IpAddress
        };
        db.Organizations.Add(org);
        subscriptions.CreateInitial(org, plan, startAsTrial, input.BillingCycle, clock.Today);
        await db.SaveChangesAsync(ct);

        using (context.BeginOrganizationScope(org.Id))
        {
            var permissionIds = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.Id, ct);
            Role? adminRole = null;
            foreach (var (name, codes) in TenantPermissions.SystemRoles)
            {
                var role = new Role
                {
                    OrganizationId = org.Id, Name = name, IsSystem = true,
                    Description = name == TenantPermissions.OrgAdminRole ? "Full administration of this organization" : null,
                    RolePermissions = codes.Where(permissionIds.ContainsKey)
                        .Select(c => new RolePermission { PermissionId = permissionIds[c] }).ToList()
                };
                db.Roles.Add(role);
                if (name == TenantPermissions.OrgAdminRole) adminRole = role;
            }

            var admin = new User
            {
                OrganizationId = org.Id,
                Email = input.AdminEmail.Trim().ToLowerInvariant(),
                FullName = input.AdminName.Trim(),
                PasswordHash = passwords.Hash(input.AdminPassword),
                IsOrganizationOwner = true,
                IsActive = true
            };
            admin.UserRoles.Add(new UserRole { Role = adminRole! });
            db.Users.Add(admin);
            CoreHr.HrDefaults.AddTo(db, org.Id);
            Time.TimeDefaults.AddTo(db, org.Id);
            Payroll.PayrollDefaults.AddTo(db, org.Id);
            Documents.LetterDefaults.AddTo(db, org.Id);
            Payroll.ExpenseDefaults.AddTo(db, org.Id);
            Strength.OnboardingDefaults.AddTo(db, org.Id);

            audit.Add(context.IsPlatform ? "Organization.CreatedByPlatform" : "Organization.SelfRegistered",
                nameof(Organization), org.Id.ToString(), $"{org.Name} on {plan.Name} ({(startAsTrial ? "trial" : "paid")})", org.Id);
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Result<int>.Ok(org.Id);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$")]
    private static partial Regex CodeRegex();
}
