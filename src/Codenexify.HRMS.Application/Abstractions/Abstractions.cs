using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Identity;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Codenexify.HRMS.Application.Abstractions;

/// <summary>
/// Who is executing the current operation. Populated only from the authenticated principal —
/// never from request input — and consumed by the data layer to enforce tenant isolation.
/// </summary>
public interface ICurrentContext
{
    PrincipalType? PrincipalType { get; }
    bool IsPlatform { get; }
    int? OrganizationId { get; }
    int? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }

    /// <summary>
    /// Server-side elevation into one organization for trusted flows that run before a tenant principal
    /// exists (sign-in, self-registration, provisioning, background jobs). Dispose to restore.
    /// </summary>
    IDisposable BeginOrganizationScope(int organizationId);

    /// <summary>Server-side elevation to platform scope for system jobs. Dispose to restore.</summary>
    IDisposable BeginPlatformScope();
}

/// <summary>Unit of work over the application's tables. EF Core DbSets act as the repositories.</summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Feature> Features { get; }
    DbSet<PlanFeature> PlanFeatures { get; }
    DbSet<OrganizationSubscription> OrganizationSubscriptions { get; }
    DbSet<OrganizationFeatureOverride> OrganizationFeatureOverrides { get; }
    DbSet<PlatformUser> PlatformUsers { get; }
    DbSet<PlatformUserPermission> PlatformUserPermissions { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<LoginAudit> LoginAudits { get; }

    DbSet<Domain.CoreHr.OrganizationSetting> OrganizationSettings { get; }
    DbSet<Domain.CoreHr.Branch> Branches { get; }
    DbSet<Domain.CoreHr.Department> Departments { get; }
    DbSet<Domain.CoreHr.Designation> Designations { get; }
    DbSet<Domain.CoreHr.Employee> Employees { get; }
    DbSet<Domain.CoreHr.EmployeeBankDetail> EmployeeBankDetails { get; }
    DbSet<Domain.CoreHr.EmployeeEducation> EmployeeEducation { get; }
    DbSet<Domain.CoreHr.EmployeeExperience> EmployeeExperience { get; }
    DbSet<Domain.CoreHr.EmployeeDocument> EmployeeDocuments { get; }
    DbSet<Domain.CoreHr.ExitRequest> ExitRequests { get; }
    DbSet<Domain.CoreHr.ExitChecklistItem> ExitChecklistItems { get; }
    DbSet<Domain.CoreHr.ExitChecklistTemplateItem> ExitChecklistTemplateItems { get; }

    DbSet<Domain.Time.Holiday> Holidays { get; }
    DbSet<Domain.Time.LeaveType> LeaveTypes { get; }
    DbSet<Domain.Time.LeaveBalance> LeaveBalances { get; }
    DbSet<Domain.Time.LeaveApplication> LeaveApplications { get; }
    DbSet<Domain.Time.Shift> Shifts { get; }
    DbSet<Domain.Time.ShiftAssignment> ShiftAssignments { get; }
    DbSet<Domain.Time.AttendancePunch> AttendancePunches { get; }
    DbSet<Domain.Time.AttendanceDay> Attendance { get; }
    DbSet<Domain.Time.OvertimeRequest> OvertimeRequests { get; }
    DbSet<Domain.Time.Notification> Notifications { get; }

    DbSet<Domain.Payroll.StatutoryRateSet> StatutoryRateSets { get; }
    DbSet<Domain.Payroll.ProfessionalTaxSlab> ProfessionalTaxSlabs { get; }
    DbSet<Domain.Payroll.LabourWelfareFundRule> LabourWelfareFundRules { get; }
    DbSet<Domain.Payroll.IncomeTaxRule> IncomeTaxRules { get; }
    DbSet<Domain.Payroll.IncomeTaxSlab> IncomeTaxSlabs { get; }
    DbSet<Domain.Payroll.IncomeTaxSurcharge> IncomeTaxSurcharges { get; }
    DbSet<Domain.Payroll.PayrollSetting> PayrollSettings { get; }
    DbSet<Domain.Payroll.SalaryComponent> SalaryComponents { get; }
    DbSet<Domain.Payroll.SalaryStructure> SalaryStructures { get; }
    DbSet<Domain.Payroll.SalaryStructureLine> SalaryStructureLines { get; }
    DbSet<Domain.Payroll.EmployeeSalary> EmployeeSalaries { get; }
    DbSet<Domain.Payroll.TaxDeclaration> TaxDeclarations { get; }
    DbSet<Domain.Payroll.EmployeeLoan> EmployeeLoans { get; }
    DbSet<Domain.Payroll.LoanRepayment> LoanRepayments { get; }
    DbSet<Domain.Payroll.PayrollRun> PayrollRuns { get; }
    DbSet<Domain.Payroll.PayrollDetail> PayrollDetails { get; }
    DbSet<Domain.Payroll.PayrollLine> PayrollLines { get; }
    DbSet<Domain.Payroll.FullAndFinalSettlement> FullAndFinalSettlements { get; }
    DbSet<Domain.Payroll.SettlementLine> SettlementLines { get; }

    DbSet<Domain.Billing.PlatformSetting> PlatformSettings { get; }
    DbSet<Domain.Billing.DocumentSequence> DocumentSequences { get; }
    DbSet<Domain.Billing.Invoice> Invoices { get; }
    DbSet<Domain.Billing.InvoiceLine> InvoiceLines { get; }
    DbSet<Domain.Billing.Payment> Payments { get; }
    DbSet<Domain.Billing.CreditNote> CreditNotes { get; }
    DbSet<Domain.Billing.EmailLog> EmailLogs { get; }
    DbSet<Domain.Support.SupportTicket> SupportTickets { get; }
    DbSet<Domain.Support.TicketMessage> TicketMessages { get; }
    DbSet<Domain.Support.SupportAccessGrant> SupportAccessGrants { get; }
    DbSet<Domain.Support.SupportSession> SupportSessions { get; }
    DbSet<Domain.Support.PlatformAnnouncement> PlatformAnnouncements { get; }
    DbSet<Domain.Engagement.WallPost> WallPosts { get; }
    DbSet<Domain.Engagement.WallPostRecipient> WallPostRecipients { get; }
    DbSet<Domain.Engagement.PollOption> PollOptions { get; }
    DbSet<Domain.Engagement.PollVote> PollVotes { get; }
    DbSet<Domain.Engagement.WallComment> WallComments { get; }
    DbSet<Domain.Engagement.WallReaction> WallReactions { get; }
    DbSet<Domain.Engagement.ProfileChangeRequest> ProfileChangeRequests { get; }
    DbSet<Domain.CoreHr.LetterTemplate> LetterTemplates { get; }
    DbSet<Domain.CoreHr.GeneratedLetter> GeneratedLetters { get; }
    DbSet<Domain.Payroll.PayrollAdjustment> PayrollAdjustments { get; }
    DbSet<Domain.Payroll.ExpenseCategory> ExpenseCategories { get; }
    DbSet<Domain.Payroll.ExpenseClaim> ExpenseClaims { get; }
    DbSet<Domain.Payroll.ExpenseItem> ExpenseItems { get; }
    DbSet<Domain.Strength.Asset> Assets { get; }
    DbSet<Domain.Strength.AssetAssignment> AssetAssignments { get; }
    DbSet<Domain.Strength.OfficeLocation> OfficeLocations { get; }
    DbSet<Domain.Strength.Survey> Surveys { get; }
    DbSet<Domain.Strength.SurveyQuestion> SurveyQuestions { get; }
    DbSet<Domain.Strength.SurveyParticipation> SurveyParticipations { get; }
    DbSet<Domain.Strength.SurveyResponse> SurveyResponses { get; }
    DbSet<Domain.Strength.SurveyAnswer> SurveyAnswers { get; }
    DbSet<Domain.Strength.FeedbackThread> FeedbackThreads { get; }
    DbSet<Domain.Strength.FeedbackMessage> FeedbackMessages { get; }
    DbSet<Domain.Strength.OnboardingTemplate> OnboardingTemplates { get; }
    DbSet<Domain.Strength.OnboardingTemplateTask> OnboardingTemplateTasks { get; }
    DbSet<Domain.Strength.OnboardingPlan> OnboardingPlans { get; }
    DbSet<Domain.Strength.OnboardingTask> OnboardingTasks { get; }
    DbSet<Domain.Strength.TravelRequest> TravelRequests { get; }
    DbSet<Domain.Strength.SignatureRequest> SignatureRequests { get; }
    DbSet<Domain.Strength.NotificationRule> NotificationRules { get; }
    DbSet<Domain.Strength.NotificationRuleLog> NotificationRuleLogs { get; }
    DbSet<Domain.Strength.ReportSchedule> ReportSchedules { get; }
    DbSet<Domain.Strength.LoginOtp> LoginOtps { get; }
    DbSet<Domain.Identity.PasswordResetToken> PasswordResetTokens { get; }
    DbSet<Domain.Platform.LegalPage> LegalPages { get; }
    DbSet<Domain.Benefits.BenefitPlan> BenefitPlans { get; }
    DbSet<Domain.Benefits.BenefitEnrollment> BenefitEnrollments { get; }
    DbSet<Domain.Benefits.BenefitDependent> BenefitDependents { get; }
    DbSet<Domain.Performance.Goal> Goals { get; }
    DbSet<Domain.Performance.KeyResult> KeyResults { get; }
    DbSet<Domain.Performance.GoalCheckIn> GoalCheckIns { get; }
    DbSet<Domain.Performance.ReviewCycle> ReviewCycles { get; }
    DbSet<Domain.Performance.ReviewQuestion> ReviewQuestions { get; }
    DbSet<Domain.Performance.PerformanceReview> PerformanceReviews { get; }
    DbSet<Domain.Performance.ReviewAnswer> ReviewAnswers { get; }
    DbSet<Domain.Performance.FeedbackNote> FeedbackNotes { get; }
    DbSet<Domain.Performance.FeedbackRequest> FeedbackRequests { get; }
    DbSet<Domain.Performance.FeedbackRequestRecipient> FeedbackRequestRecipients { get; }
    DbSet<Domain.Performance.OneOnOne> OneOnOnes { get; }
    DbSet<Domain.Performance.MeetingItem> MeetingItems { get; }
    DbSet<Domain.Performance.CoreValue> CoreValues { get; }
    DbSet<Domain.Performance.ValueShoutout> ValueShoutouts { get; }
    DbSet<Domain.Performance.Skill> Skills { get; }
    DbSet<Domain.Performance.EmployeeSkill> EmployeeSkills { get; }
    DbSet<Domain.Performance.SkillRequirement> SkillRequirements { get; }
    DbSet<Domain.Performance.PromotionCase> PromotionCases { get; }
    DbSet<Domain.Performance.ImprovementPlan> ImprovementPlans { get; }
    DbSet<Domain.Performance.PipObjective> PipObjectives { get; }
    DbSet<Domain.Performance.PipCheckIn> PipCheckIns { get; }
    DbSet<Domain.Performance.CompensationCycle> CompensationCycles { get; }
    DbSet<Domain.Performance.CompensationGuideline> CompensationGuidelines { get; }
    DbSet<Domain.Performance.CompensationLine> CompensationLines { get; }
    DbSet<Domain.Performance.CalibrationTarget> CalibrationTargets { get; }
    DbSet<Domain.Performance.WorkflowRule> WorkflowRules { get; }
    DbSet<Domain.Performance.WorkflowAction> WorkflowActions { get; }
    DbSet<Domain.Performance.WorkflowRun> WorkflowRuns { get; }
    DbSet<Domain.Performance.WorkTask> WorkTasks { get; }
    DbSet<Domain.Performance.SavedReport> SavedReports { get; }
    DbSet<Domain.Planning.HeadcountPlan> HeadcountPlans { get; }
    DbSet<Domain.Planning.HeadcountPosition> HeadcountPositions { get; }
    DbSet<Domain.Planning.HeadcountHire> HeadcountHires { get; }
    DbSet<Domain.Planning.PreboardingSettings> PreboardingSettings { get; }
    DbSet<Domain.Planning.PreboardingItem> PreboardingItems { get; }
    DbSet<Domain.Planning.PreboardingInvite> PreboardingInvites { get; }
    DbSet<Domain.Planning.PreboardingResponse> PreboardingResponses { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public interface IPasswordService
{
    string Hash(string password);
    /// <returns>Verified flag and whether the stored hash should be upgraded.</returns>
    (bool Verified, bool NeedsRehash) Verify(string hash, string password);
}

public interface ITotpService
{
    string GenerateSecret();
    bool Verify(string base32Secret, string code, DateTime utcNow);
    string BuildProvisioningUri(string issuer, string account, string base32Secret);
}

/// <summary>Symmetric protection for secrets stored in the database (TOTP keys, API credentials).</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    string Unprotect(string protectedValue);
}

/// <summary>Private file storage for tenant documents (local disk now; cloud blob storage later).</summary>
public interface IFileStorage
{
    /// <returns>An opaque storage key, always prefixed with the organization id.</returns>
    Task<string> SaveAsync(int organizationId, Stream content, string extension, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>Writes tabular data to an .xlsx workbook.</summary>
public interface IExcelExporter
{
    byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);
}

/// <summary>Writes named business actions to the audit trail (data changes are captured automatically).</summary>
public interface IAuditWriter
{
    void Add(string action, string entityType, string? entityId, string? details, int? organizationId = null);
}
