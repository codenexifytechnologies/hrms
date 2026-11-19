using System.Linq.Expressions;
using System.Text.Json;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Codenexify.HRMS.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentContext currentContext, IClock clock)
    : DbContext(options), IAppDbContext
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
        { nameof(User.PasswordHash), nameof(PlatformUser.TwoFactorSecret), nameof(User.SecurityStamp), "RowVersion",
          "PanEncrypted", "AccountNumberEncrypted", "StorageKey",
          "RazorpayKeySecretProtected", "RazorpayWebhookSecretProtected", "SmtpPasswordProtected",
          // One-time codes (hashes of 6-digit codes can be brute-forced, so they never reach the audit trail) and other secrets.
          "OtpHash", "CodeHash", "AccessCodeHash", "GoogleClientSecretProtected", "MicrosoftClientSecretProtected", "Msg91AuthKeyProtected",
          "SelfieStorageKey", "TicketStorageKey" };

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<OrganizationFeatureOverride> OrganizationFeatureOverrides => Set<OrganizationFeatureOverride>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<PlatformUserPermission> PlatformUserPermissions => Set<PlatformUserPermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    public DbSet<Domain.Identity.PasswordResetToken> PasswordResetTokens => Set<Domain.Identity.PasswordResetToken>();
    public DbSet<LegalPage> LegalPages => Set<LegalPage>();
    public DbSet<Domain.Benefits.BenefitPlan> BenefitPlans => Set<Domain.Benefits.BenefitPlan>();
    public DbSet<Domain.Benefits.BenefitEnrollment> BenefitEnrollments => Set<Domain.Benefits.BenefitEnrollment>();
    public DbSet<Domain.Benefits.BenefitDependent> BenefitDependents => Set<Domain.Benefits.BenefitDependent>();
    public DbSet<Domain.Performance.Goal> Goals => Set<Domain.Performance.Goal>();
    public DbSet<Domain.Performance.KeyResult> KeyResults => Set<Domain.Performance.KeyResult>();
    public DbSet<Domain.Performance.GoalCheckIn> GoalCheckIns => Set<Domain.Performance.GoalCheckIn>();
    public DbSet<Domain.Performance.ReviewCycle> ReviewCycles => Set<Domain.Performance.ReviewCycle>();
    public DbSet<Domain.Performance.ReviewQuestion> ReviewQuestions => Set<Domain.Performance.ReviewQuestion>();
    public DbSet<Domain.Performance.PerformanceReview> PerformanceReviews => Set<Domain.Performance.PerformanceReview>();
    public DbSet<Domain.Performance.ReviewAnswer> ReviewAnswers => Set<Domain.Performance.ReviewAnswer>();
    public DbSet<Domain.Performance.FeedbackNote> FeedbackNotes => Set<Domain.Performance.FeedbackNote>();
    public DbSet<Domain.Performance.FeedbackRequest> FeedbackRequests => Set<Domain.Performance.FeedbackRequest>();
    public DbSet<Domain.Performance.FeedbackRequestRecipient> FeedbackRequestRecipients => Set<Domain.Performance.FeedbackRequestRecipient>();
    public DbSet<Domain.Performance.OneOnOne> OneOnOnes => Set<Domain.Performance.OneOnOne>();
    public DbSet<Domain.Performance.MeetingItem> MeetingItems => Set<Domain.Performance.MeetingItem>();
    public DbSet<Domain.Performance.CoreValue> CoreValues => Set<Domain.Performance.CoreValue>();
    public DbSet<Domain.Performance.ValueShoutout> ValueShoutouts => Set<Domain.Performance.ValueShoutout>();
    public DbSet<Domain.Performance.Skill> Skills => Set<Domain.Performance.Skill>();
    public DbSet<Domain.Performance.EmployeeSkill> EmployeeSkills => Set<Domain.Performance.EmployeeSkill>();
    public DbSet<Domain.Performance.SkillRequirement> SkillRequirements => Set<Domain.Performance.SkillRequirement>();
    public DbSet<Domain.Performance.PromotionCase> PromotionCases => Set<Domain.Performance.PromotionCase>();
    public DbSet<Domain.Performance.ImprovementPlan> ImprovementPlans => Set<Domain.Performance.ImprovementPlan>();
    public DbSet<Domain.Performance.PipObjective> PipObjectives => Set<Domain.Performance.PipObjective>();
    public DbSet<Domain.Performance.PipCheckIn> PipCheckIns => Set<Domain.Performance.PipCheckIn>();
    public DbSet<Domain.Performance.CompensationCycle> CompensationCycles => Set<Domain.Performance.CompensationCycle>();
    public DbSet<Domain.Performance.CompensationGuideline> CompensationGuidelines => Set<Domain.Performance.CompensationGuideline>();
    public DbSet<Domain.Performance.CompensationLine> CompensationLines => Set<Domain.Performance.CompensationLine>();
    public DbSet<Domain.Performance.CalibrationTarget> CalibrationTargets => Set<Domain.Performance.CalibrationTarget>();
    public DbSet<Domain.Performance.WorkflowRule> WorkflowRules => Set<Domain.Performance.WorkflowRule>();
    public DbSet<Domain.Performance.WorkflowAction> WorkflowActions => Set<Domain.Performance.WorkflowAction>();
    public DbSet<Domain.Performance.WorkflowRun> WorkflowRuns => Set<Domain.Performance.WorkflowRun>();
    public DbSet<Domain.Performance.WorkTask> WorkTasks => Set<Domain.Performance.WorkTask>();
    public DbSet<Domain.Performance.SavedReport> SavedReports => Set<Domain.Performance.SavedReport>();
    public DbSet<Domain.Planning.HeadcountPlan> HeadcountPlans => Set<Domain.Planning.HeadcountPlan>();
    public DbSet<Domain.Planning.HeadcountPosition> HeadcountPositions => Set<Domain.Planning.HeadcountPosition>();
    public DbSet<Domain.Planning.HeadcountHire> HeadcountHires => Set<Domain.Planning.HeadcountHire>();
    public DbSet<Domain.Planning.PreboardingSettings> PreboardingSettings => Set<Domain.Planning.PreboardingSettings>();
    public DbSet<Domain.Planning.PreboardingItem> PreboardingItems => Set<Domain.Planning.PreboardingItem>();
    public DbSet<Domain.Planning.PreboardingInvite> PreboardingInvites => Set<Domain.Planning.PreboardingInvite>();
    public DbSet<Domain.Planning.PreboardingResponse> PreboardingResponses => Set<Domain.Planning.PreboardingResponse>();

    public DbSet<Domain.CoreHr.OrganizationSetting> OrganizationSettings => Set<Domain.CoreHr.OrganizationSetting>();
    public DbSet<Domain.CoreHr.Branch> Branches => Set<Domain.CoreHr.Branch>();
    public DbSet<Domain.CoreHr.Department> Departments => Set<Domain.CoreHr.Department>();
    public DbSet<Domain.CoreHr.Designation> Designations => Set<Domain.CoreHr.Designation>();
    public DbSet<Domain.CoreHr.Employee> Employees => Set<Domain.CoreHr.Employee>();
    public DbSet<Domain.CoreHr.EmployeeBankDetail> EmployeeBankDetails => Set<Domain.CoreHr.EmployeeBankDetail>();
    public DbSet<Domain.CoreHr.EmployeeEducation> EmployeeEducation => Set<Domain.CoreHr.EmployeeEducation>();
    public DbSet<Domain.CoreHr.EmployeeExperience> EmployeeExperience => Set<Domain.CoreHr.EmployeeExperience>();
    public DbSet<Domain.CoreHr.EmployeeDocument> EmployeeDocuments => Set<Domain.CoreHr.EmployeeDocument>();
    public DbSet<Domain.CoreHr.ExitRequest> ExitRequests => Set<Domain.CoreHr.ExitRequest>();
    public DbSet<Domain.CoreHr.ExitChecklistItem> ExitChecklistItems => Set<Domain.CoreHr.ExitChecklistItem>();
    public DbSet<Domain.CoreHr.ExitChecklistTemplateItem> ExitChecklistTemplateItems => Set<Domain.CoreHr.ExitChecklistTemplateItem>();

    public DbSet<Domain.Time.Holiday> Holidays => Set<Domain.Time.Holiday>();
    public DbSet<Domain.Time.LeaveType> LeaveTypes => Set<Domain.Time.LeaveType>();
    public DbSet<Domain.Time.LeaveBalance> LeaveBalances => Set<Domain.Time.LeaveBalance>();
    public DbSet<Domain.Time.LeaveApplication> LeaveApplications => Set<Domain.Time.LeaveApplication>();
    public DbSet<Domain.Time.Shift> Shifts => Set<Domain.Time.Shift>();
    public DbSet<Domain.Time.ShiftAssignment> ShiftAssignments => Set<Domain.Time.ShiftAssignment>();
    public DbSet<Domain.Time.AttendancePunch> AttendancePunches => Set<Domain.Time.AttendancePunch>();
    public DbSet<Domain.Time.AttendanceDay> Attendance => Set<Domain.Time.AttendanceDay>();
    public DbSet<Domain.Time.OvertimeRequest> OvertimeRequests => Set<Domain.Time.OvertimeRequest>();
    public DbSet<Domain.Time.Notification> Notifications => Set<Domain.Time.Notification>();

    public DbSet<Domain.Payroll.StatutoryRateSet> StatutoryRateSets => Set<Domain.Payroll.StatutoryRateSet>();
    public DbSet<Domain.Payroll.ProfessionalTaxSlab> ProfessionalTaxSlabs => Set<Domain.Payroll.ProfessionalTaxSlab>();
    public DbSet<Domain.Payroll.LabourWelfareFundRule> LabourWelfareFundRules => Set<Domain.Payroll.LabourWelfareFundRule>();
    public DbSet<Domain.Payroll.IncomeTaxRule> IncomeTaxRules => Set<Domain.Payroll.IncomeTaxRule>();
    public DbSet<Domain.Payroll.IncomeTaxSlab> IncomeTaxSlabs => Set<Domain.Payroll.IncomeTaxSlab>();
    public DbSet<Domain.Payroll.IncomeTaxSurcharge> IncomeTaxSurcharges => Set<Domain.Payroll.IncomeTaxSurcharge>();
    public DbSet<Domain.Payroll.PayrollSetting> PayrollSettings => Set<Domain.Payroll.PayrollSetting>();
    public DbSet<Domain.Payroll.SalaryComponent> SalaryComponents => Set<Domain.Payroll.SalaryComponent>();
    public DbSet<Domain.Payroll.SalaryStructure> SalaryStructures => Set<Domain.Payroll.SalaryStructure>();
    public DbSet<Domain.Payroll.SalaryStructureLine> SalaryStructureLines => Set<Domain.Payroll.SalaryStructureLine>();
    public DbSet<Domain.Payroll.EmployeeSalary> EmployeeSalaries => Set<Domain.Payroll.EmployeeSalary>();
    public DbSet<Domain.Payroll.TaxDeclaration> TaxDeclarations => Set<Domain.Payroll.TaxDeclaration>();
    public DbSet<Domain.Payroll.EmployeeLoan> EmployeeLoans => Set<Domain.Payroll.EmployeeLoan>();
    public DbSet<Domain.Payroll.LoanRepayment> LoanRepayments => Set<Domain.Payroll.LoanRepayment>();
    public DbSet<Domain.Payroll.PayrollRun> PayrollRuns => Set<Domain.Payroll.PayrollRun>();
    public DbSet<Domain.Payroll.PayrollDetail> PayrollDetails => Set<Domain.Payroll.PayrollDetail>();
    public DbSet<Domain.Payroll.PayrollLine> PayrollLines => Set<Domain.Payroll.PayrollLine>();
    public DbSet<Domain.Payroll.FullAndFinalSettlement> FullAndFinalSettlements => Set<Domain.Payroll.FullAndFinalSettlement>();
    public DbSet<Domain.Payroll.SettlementLine> SettlementLines => Set<Domain.Payroll.SettlementLine>();

    public DbSet<Domain.Billing.PlatformSetting> PlatformSettings => Set<Domain.Billing.PlatformSetting>();
    public DbSet<Domain.Billing.DocumentSequence> DocumentSequences => Set<Domain.Billing.DocumentSequence>();
    public DbSet<Domain.Billing.Invoice> Invoices => Set<Domain.Billing.Invoice>();
    public DbSet<Domain.Billing.InvoiceLine> InvoiceLines => Set<Domain.Billing.InvoiceLine>();
    public DbSet<Domain.Billing.Payment> Payments => Set<Domain.Billing.Payment>();
    public DbSet<Domain.Billing.CreditNote> CreditNotes => Set<Domain.Billing.CreditNote>();
    public DbSet<Domain.Billing.EmailLog> EmailLogs => Set<Domain.Billing.EmailLog>();
    public DbSet<Domain.Support.SupportTicket> SupportTickets => Set<Domain.Support.SupportTicket>();
    public DbSet<Domain.Support.TicketMessage> TicketMessages => Set<Domain.Support.TicketMessage>();
    public DbSet<Domain.Support.SupportAccessGrant> SupportAccessGrants => Set<Domain.Support.SupportAccessGrant>();
    public DbSet<Domain.Support.SupportSession> SupportSessions => Set<Domain.Support.SupportSession>();
    public DbSet<Domain.Support.PlatformAnnouncement> PlatformAnnouncements => Set<Domain.Support.PlatformAnnouncement>();
    public DbSet<Domain.Engagement.WallPost> WallPosts => Set<Domain.Engagement.WallPost>();
    public DbSet<Domain.Engagement.WallPostRecipient> WallPostRecipients => Set<Domain.Engagement.WallPostRecipient>();
    public DbSet<Domain.Engagement.PollOption> PollOptions => Set<Domain.Engagement.PollOption>();
    public DbSet<Domain.Engagement.PollVote> PollVotes => Set<Domain.Engagement.PollVote>();
    public DbSet<Domain.Engagement.WallComment> WallComments => Set<Domain.Engagement.WallComment>();
    public DbSet<Domain.Engagement.WallReaction> WallReactions => Set<Domain.Engagement.WallReaction>();
    public DbSet<Domain.Engagement.ProfileChangeRequest> ProfileChangeRequests => Set<Domain.Engagement.ProfileChangeRequest>();
    public DbSet<Domain.CoreHr.LetterTemplate> LetterTemplates => Set<Domain.CoreHr.LetterTemplate>();
    public DbSet<Domain.CoreHr.GeneratedLetter> GeneratedLetters => Set<Domain.CoreHr.GeneratedLetter>();
    public DbSet<Domain.Payroll.PayrollAdjustment> PayrollAdjustments => Set<Domain.Payroll.PayrollAdjustment>();
    public DbSet<Domain.Payroll.ExpenseCategory> ExpenseCategories => Set<Domain.Payroll.ExpenseCategory>();
    public DbSet<Domain.Payroll.ExpenseClaim> ExpenseClaims => Set<Domain.Payroll.ExpenseClaim>();
    public DbSet<Domain.Payroll.ExpenseItem> ExpenseItems => Set<Domain.Payroll.ExpenseItem>();
    public DbSet<Domain.Strength.Asset> Assets => Set<Domain.Strength.Asset>();
    public DbSet<Domain.Strength.AssetAssignment> AssetAssignments => Set<Domain.Strength.AssetAssignment>();
    public DbSet<Domain.Strength.OfficeLocation> OfficeLocations => Set<Domain.Strength.OfficeLocation>();
    public DbSet<Domain.Strength.Survey> Surveys => Set<Domain.Strength.Survey>();
    public DbSet<Domain.Strength.SurveyQuestion> SurveyQuestions => Set<Domain.Strength.SurveyQuestion>();
    public DbSet<Domain.Strength.SurveyParticipation> SurveyParticipations => Set<Domain.Strength.SurveyParticipation>();
    public DbSet<Domain.Strength.SurveyResponse> SurveyResponses => Set<Domain.Strength.SurveyResponse>();
    public DbSet<Domain.Strength.SurveyAnswer> SurveyAnswers => Set<Domain.Strength.SurveyAnswer>();
    public DbSet<Domain.Strength.FeedbackThread> FeedbackThreads => Set<Domain.Strength.FeedbackThread>();
    public DbSet<Domain.Strength.FeedbackMessage> FeedbackMessages => Set<Domain.Strength.FeedbackMessage>();
    public DbSet<Domain.Strength.OnboardingTemplate> OnboardingTemplates => Set<Domain.Strength.OnboardingTemplate>();
    public DbSet<Domain.Strength.OnboardingTemplateTask> OnboardingTemplateTasks => Set<Domain.Strength.OnboardingTemplateTask>();
    public DbSet<Domain.Strength.OnboardingPlan> OnboardingPlans => Set<Domain.Strength.OnboardingPlan>();
    public DbSet<Domain.Strength.OnboardingTask> OnboardingTasks => Set<Domain.Strength.OnboardingTask>();
    public DbSet<Domain.Strength.TravelRequest> TravelRequests => Set<Domain.Strength.TravelRequest>();
    public DbSet<Domain.Strength.SignatureRequest> SignatureRequests => Set<Domain.Strength.SignatureRequest>();
    public DbSet<Domain.Strength.NotificationRule> NotificationRules => Set<Domain.Strength.NotificationRule>();
    public DbSet<Domain.Strength.NotificationRuleLog> NotificationRuleLogs => Set<Domain.Strength.NotificationRuleLog>();
    public DbSet<Domain.Strength.ReportSchedule> ReportSchedules => Set<Domain.Strength.ReportSchedule>();
    public DbSet<Domain.Strength.LoginOtp> LoginOtps => Set<Domain.Strength.LoginOtp>();

    // Evaluated per query by EF Core (DbContext members are parameterised, not captured as constants).
    private bool IsPlatformScope => currentContext.IsPlatform;
    private int? CurrentOrganizationId => currentContext.OrganizationId;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            if (typeof(ITenantEntity).IsAssignableFrom(clr))
                typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), flags)!.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
            else if (typeof(ISoftDelete).IsAssignableFrom(clr))
                typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), flags)!.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>Central tenant isolation: tenant rows are visible only to their own organization (or platform scope).</summary>
    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity, ISoftDelete
    {
        Expression<Func<T, bool>> filter = e => (IsPlatformScope || e.OrganizationId == CurrentOrganizationId) && !e.IsDeleted;
        modelBuilder.Entity<T>().HasQueryFilter(filter);
    }

    private void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
        => modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyStampsAndGuards();
        var pendingAudits = CaptureAudits();
        if (pendingAudits.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Data change and its audit rows commit atomically.
        var ownTransaction = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            foreach (var p in pendingAudits)
            {
                p.Log.EntityId ??= KeyOf(p.Entry);
                if (p.Log.OrganizationId is null && p.Entry.Entity is Organization o) p.Log.OrganizationId = o.Id;
                AuditLogs.Add(p.Log);
            }
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            if (ownTransaction is not null) await ownTransaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            if (ownTransaction is not null) await ownTransaction.DisposeAsync();
        }
    }

    private void ApplyStampsAndGuards()
    {
        var now = clock.UtcNow;
        var actor = currentContext.UserName ?? "system";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantEntity tenantEntity) GuardTenant(entry, tenantEntity);

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
            }
            if (entry.Entity is ISoftDelete sd && sd.IsDeleted && entry.State == EntityState.Modified
                && entry.Property(nameof(ISoftDelete.IsDeleted)).OriginalValue is false)
            {
                sd.DeletedAtUtc ??= now;
                sd.DeletedBy ??= actor;
            }

            if (entry.Entity is IAnonymousRecord && entry.Entity is BaseEntity anon)
            {
                // Who and exactly when would identify the writer: keep the date only.
                if (entry.State == EntityState.Added) { anon.CreatedAtUtc = now.Date; anon.CreatedBy = null; }
                else if (entry.State == EntityState.Modified)
                {
                    anon.UpdatedAtUtc = now.Date; anon.UpdatedBy = null;
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                }
                continue;
            }

            if (entry.Entity is BaseEntity b)
            {
                if (entry.State == EntityState.Added)
                {
                    b.CreatedAtUtc = now;
                    b.CreatedBy ??= actor;
                }
                else if (entry.State == EntityState.Modified)
                {
                    b.UpdatedAtUtc = now;
                    b.UpdatedBy = actor;
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                }
            }
        }
    }

    /// <summary>Central write guard: a tenant can only create/change/delete rows of its own organization.</summary>
    private void GuardTenant(EntityEntry entry, ITenantEntity entity)
    {
        if (entry.State is EntityState.Unchanged or EntityState.Detached) return;
        var orgId = currentContext.OrganizationId;

        if (currentContext.IsPlatform)
        {
            if (entry.State == EntityState.Added && entity.OrganizationId <= 0)
                throw new TenantIsolationException($"{entry.Metadata.ClrType.Name} created in platform scope must specify an OrganizationId.");
            return;
        }

        if (orgId is null)
            throw new TenantIsolationException($"Cannot write {entry.Metadata.ClrType.Name} without an organization context.");

        if (entry.State == EntityState.Added)
        {
            if (entity.OrganizationId == 0) entity.OrganizationId = orgId.Value;
            else if (entity.OrganizationId != orgId)
                throw new TenantIsolationException($"Attempt to create {entry.Metadata.ClrType.Name} for another organization.");
            return;
        }

        var original = (int)entry.Property(nameof(ITenantEntity.OrganizationId)).OriginalValue!;
        if (original != orgId || entity.OrganizationId != orgId)
            throw new TenantIsolationException($"Attempt to modify {entry.Metadata.ClrType.Name} of another organization.");
    }

    private sealed record PendingAudit(EntityEntry Entry, AuditLog Log);

    private List<PendingAudit> CaptureAudits()
    {
        var list = new List<PendingAudit>();
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.Entity is IAuditable && e.Entity is not IAnonymousRecord &&
                     e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Deleted => "Deleted",
                _ when entry.Entity is ISoftDelete { IsDeleted: true } && entry.Property(nameof(ISoftDelete.IsDeleted)).OriginalValue is false => "Deleted",
                _ => "Updated"
            };

            Dictionary<string, object?>? oldValues = null;
            var newValues = new Dictionary<string, object?>();
            foreach (var prop in entry.Properties)
            {
                var name = prop.Metadata.Name;
                if (SensitiveProperties.Contains(name) || prop.Metadata.IsPrimaryKey()) continue;
                if (entry.State == EntityState.Added) { newValues[name] = prop.CurrentValue; continue; }
                if (entry.State == EntityState.Deleted) { (oldValues ??= [])[name] = prop.OriginalValue; continue; }
                if (!prop.IsModified || Equals(prop.OriginalValue, prop.CurrentValue)) continue;
                if (name is nameof(BaseEntity.UpdatedAtUtc) or nameof(BaseEntity.UpdatedBy)) continue;
                (oldValues ??= [])[name] = prop.OriginalValue;
                newValues[name] = prop.CurrentValue;
            }
            if (action == "Updated" && newValues.Count == 0) continue;

            var orgProp = entry.Metadata.FindProperty("OrganizationId");
            list.Add(new PendingAudit(entry, new AuditLog
            {
                TimestampUtc = now,
                OrganizationId = orgProp is null ? null : entry.Property(orgProp.Name).CurrentValue as int?,
                ActorType = currentContext.PrincipalType,
                ActorId = currentContext.UserId,
                ActorName = currentContext.UserName,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.State == EntityState.Added ? null : KeyOf(entry),
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                IpAddress = currentContext.IpAddress
            }));
        }
        return list;
    }

    private static string? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        return key is null ? null : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
    }
}
