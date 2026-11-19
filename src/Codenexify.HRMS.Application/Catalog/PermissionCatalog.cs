using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Application.Catalog;

/// <summary>Permissions for Codenexify's own staff in the Super Admin panel.</summary>
public static class PlatformPermissions
{
    public const string DashboardView = "platform.dashboard.view";
    public const string OrganizationsView = "platform.organizations.view";
    public const string OrganizationsManage = "platform.organizations.manage";
    public const string OrganizationsDelete = "platform.organizations.delete";
    public const string PlansView = "platform.plans.view";
    public const string PlansManage = "platform.plans.manage";
    public const string SubscriptionsManage = "platform.subscriptions.manage";
    public const string FeaturesManage = "platform.features.manage";
    public const string UsersManage = "platform.users.manage";
    public const string AuditView = "platform.audit.view";
    public const string StatutoryManage = "platform.statutory.manage";
    public const string BillingManage = "platform.billing.manage";
    public const string SettingsManage = "platform.settings.manage";
    public const string SupportManage = "platform.support.manage";
    public const string SupportAccess = "platform.support.access";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [SupportManage] = "Work on support tickets and publish announcements",
        [SupportAccess] = "Open read-only sessions in organizations that granted consent",
        [BillingManage] = "Invoices, payments, credit notes & revenue",
        [SettingsManage] = "Platform billing, payment-gateway and email settings",
        [StatutoryManage] = "Maintain statutory rules (PF, ESI, PT, LWF, income tax)",
        [DashboardView] = "View platform dashboard",
        [OrganizationsView] = "View organizations",
        [OrganizationsManage] = "Create / edit organizations and change status",
        [OrganizationsDelete] = "Delete organizations",
        [PlansView] = "View plans and features",
        [PlansManage] = "Create / edit plans",
        [SubscriptionsManage] = "Manage subscriptions and feature overrides",
        [FeaturesManage] = "Enable / disable features globally",
        [UsersManage] = "Manage platform users and permissions",
        [AuditView] = "View audit and login history",
    };

    public static IReadOnlyCollection<string> All => Descriptions.Keys.ToArray();

    /// <summary>Default permission set for each platform role. Extra grants are stored per user.</summary>
    public static IReadOnlySet<string> ForRole(PlatformRole role) => role switch
    {
        PlatformRole.SuperAdmin => new HashSet<string>(Descriptions.Keys),
        PlatformRole.SupportAdmin => new HashSet<string> { DashboardView, OrganizationsView, PlansView, AuditView, SupportManage, SupportAccess },
        PlatformRole.FinanceAdmin => new HashSet<string> { DashboardView, OrganizationsView, PlansView, SubscriptionsManage, StatutoryManage, BillingManage },
        PlatformRole.SalesAdmin => new HashSet<string> { DashboardView, OrganizationsView, OrganizationsManage, PlansView },
        _ => new HashSet<string>()
    };
}

public sealed record TenantPermissionDefinition(string Code, string Name, string Module, string? FeatureCode = null);

/// <summary>Permissions inside a client organization. Extended module by module.</summary>
public static class TenantPermissions
{
    public const string DashboardView = "org.dashboard.view";
    public const string ProfileView = "org.profile.view";
    public const string ProfileManage = "org.profile.manage";
    public const string UsersView = "org.users.view";
    public const string UsersManage = "org.users.manage";
    public const string RolesManage = "org.roles.manage";
    public const string SubscriptionView = "org.subscription.view";
    public const string AuditView = "org.audit.view";
    public const string StructureManage = "org.structure.manage";
    public const string EmployeesView = "org.employees.view";
    public const string EmployeesManage = "org.employees.manage";
    public const string EmployeesSensitive = "org.employees.sensitive";
    public const string DocumentsManage = "org.documents.manage";
    public const string ExitManage = "org.exit.manage";
    public const string ReportsView = "org.reports.view";
    public const string LeaveManage = "org.leave.manage";
    public const string AttendanceView = "org.attendance.view";
    public const string AttendanceManage = "org.attendance.manage";
    public const string PayrollView = "org.payroll.view";
    public const string PayrollManage = "org.payroll.manage";
    public const string BillingManage = "org.billing.manage";
    public const string SupportTickets = "org.support.tickets";
    public const string SupportAccessGrant = "org.support.access";
    public const string WallModerate = "org.wall.moderate";
    public const string LettersManage = "org.letters.manage";
    public const string ExpensesManage = "org.expenses.manage";
    public const string AssetsManage = "org.assets.manage";
    public const string SurveysManage = "org.surveys.manage";
    public const string FeedbackManage = "org.feedback.manage";
    public const string AnalyticsView = "org.analytics.view";
    public const string OnboardingManage = "org.onboarding.manage";
    public const string TravelManage = "org.travel.manage";
    public const string NotificationRulesManage = "org.notifications.manage";
    public const string SecurityManage = "org.security.manage";
    public const string BenefitsManage = "org.benefits.manage";
    public const string GoalsManage = "org.goals.manage";
    public const string ReviewsManage = "org.reviews.manage";
    public const string ValuesManage = "org.values.manage";
    public const string SkillsManage = "org.skills.manage";
    public const string PromotionsManage = "org.promotions.manage";
    public const string PipManage = "org.pip.manage";
    public const string CompensationManage = "org.compensation.manage";
    public const string WorkflowsManage = "org.workflows.manage";
    public const string ReportBuilder = "org.reports.build";
    public const string HeadcountManage = "org.headcount.manage";
    public const string PreboardingManage = "org.preboarding.manage";

    public static readonly IReadOnlyList<TenantPermissionDefinition> All =
    [
        new(StructureManage, "Manage branches, departments, designations & HR settings", "Core HR", FeatureCodes.CoreHr),
        new(EmployeesView, "View employee directory & profiles", "Core HR", FeatureCodes.CoreHr),
        new(EmployeesManage, "Add / edit employees", "Core HR", FeatureCodes.CoreHr),
        new(EmployeesSensitive, "View / edit bank details & PAN", "Core HR", FeatureCodes.CoreHr),
        new(DocumentsManage, "Upload, verify & delete employee documents", "Core HR", FeatureCodes.DocumentsLetters),
        new(ExitManage, "Manage resignations & exits", "Core HR", FeatureCodes.EmployeeExit),
        new(AssetsManage, "Manage the asset register, assign and take back assets", "Core HR", FeatureCodes.Assets),
        new(AnalyticsView, "View people analytics dashboards", "Reports", FeatureCodes.PeopleAnalytics),
        new(OnboardingManage, "Manage onboarding templates and new-joiner onboarding plans", "Core HR", FeatureCodes.OnboardingAdvanced),
        new(TravelManage, "Travel desk: book approved trips and record advances", "Core HR", FeatureCodes.TravelDesk),
        new(NotificationRulesManage, "Configure automatic reminder rules", "Administration", FeatureCodes.NotificationsAdvanced),
        new(SecurityManage, "Configure sign-in options (single sign-on, mobile OTP)", "Users & Roles"),
        new(SurveysManage, "Create surveys and view results", "Employee Experience", FeatureCodes.PulseSurveys),
        new(FeedbackManage, "Read and answer anonymous feedback", "Employee Experience", FeatureCodes.AnonymousFeedback),
        new(LettersManage,"Manage letter templates and issue letters (offer, appointment, experience…)", "Core HR", FeatureCodes.DocumentsLetters),
        new(ReportsView, "View & export HR reports", "Reports", FeatureCodes.ReportsBasic),
        new(LeaveManage, "Manage leave types, holidays, balances & approve any leave", "Time & Attendance", FeatureCodes.Leave),
        new(AttendanceView, "View attendance of all employees", "Time & Attendance", FeatureCodes.Attendance),
        new(AttendanceManage, "Manage shifts, regularize attendance & approve overtime", "Time & Attendance", FeatureCodes.Attendance),
        new(PayrollView, "View payroll runs, salary registers & reports", "Payroll", FeatureCodes.Payroll),
        new(PayrollManage, "Manage salary structures & salaries, run and finalize payroll, loans, tax & F&F", "Payroll", FeatureCodes.Payroll),
        new(ExpensesManage, "Approve and pay expense claims, manage expense categories", "Payroll", FeatureCodes.Expenses),
        new(BenefitsManage, "Manage perks & benefit plans, eligibility and enrolments", "Payroll", FeatureCodes.Benefits),
        new(GoalsManage, "Set company and department goals; see and edit everyone's goals", "Performance", FeatureCodes.Okrs),
        new(ReviewsManage, "Run review cycles, review employees without a manager, adjust ratings and release results", "Performance", FeatureCodes.PerformanceReviews),
        new(ValuesManage, "Define the company's core values and remove inappropriate shout-outs", "Performance", FeatureCodes.CoreValues),
        new(SkillsManage, "Manage the skill catalog and job-title requirements; see everyone's skills", "Performance", FeatureCodes.Skills),
        new(PromotionsManage, "Approve or reject promotions and apply the new title and salary", "Performance", FeatureCodes.Promotions),
        new(PipManage, "Start, review and close performance improvement plans for anyone", "Performance", FeatureCodes.Pip),
        new(CompensationManage, "Run salary-revision cycles: budgets, guidelines, every raise, approval", "Performance", FeatureCodes.CompensationPlans),
        new(WorkflowsManage, "Create automation rules (notify, tasks, onboarding, benefits) and see their runs", "Administration", FeatureCodes.WorkflowAutomation),
        new(ReportBuilder, "Build, save and export custom reports on employees, leave and attendance (payroll needs payroll access)", "Reports", FeatureCodes.CustomReportBuilder),
        new(HeadcountManage, "Plan headcount: positions, openings and budgets by department, and link new hires to them", "Administration", FeatureCodes.HeadcountPlanning),
        new(PreboardingManage, "Set up pre-boarding and send new hires their secure welcome link", "Core HR", FeatureCodes.Preboarding),
        new(DashboardView, "View organization dashboard", "Administration"),
        new(ProfileView, "View company profile", "Administration"),
        new(ProfileManage, "Edit company profile & branding", "Administration"),
        new(UsersView, "View users", "Users & Roles"),
        new(UsersManage, "Create / edit / disable users", "Users & Roles"),
        new(RolesManage, "Manage roles & permissions", "Users & Roles"),
        new(SubscriptionView, "View subscription & plan", "Administration"),
        new(BillingManage, "Buy plans, view invoices & pay online", "Administration"),
        new(AuditView, "View organization audit trail", "Administration"),
        new(WallModerate, "Post announcements, pin and remove posts on the social wall", "Employee Experience", FeatureCodes.SocialWall),
        new(SupportTickets, "Raise and follow support tickets with Codenexify", "Administration"),
        new(SupportAccessGrant, "Grant / revoke Codenexify read-only support access", "Administration"),
    ];

    /// <summary>
    /// What Codenexify staff can see during a consented read-only support session. Writes are blocked
    /// separately for the whole session; bank details/PAN, documents, billing and user/role admin stay hidden.
    /// </summary>
    public static IReadOnlyList<string> SupportSessionPermissions(bool includePayroll)
    {
        var p = new List<string> { DashboardView, ProfileView, UsersView, SubscriptionView, AuditView, StructureManage,
            EmployeesView, ReportsView, LeaveManage, AttendanceView };
        if (includePayroll) p.Add(PayrollView);
        return p;
    }

    public const string OrgAdminRole = "Organization Admin";

    /// <summary>System roles created for every new organization with their default permissions.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> SystemRoles = new Dictionary<string, string[]>
    {
        [OrgAdminRole] = All.Select(p => p.Code).ToArray(),
        ["HR Admin"] = [DashboardView, ProfileView, UsersView, UsersManage, AuditView, StructureManage, EmployeesView, EmployeesManage, EmployeesSensitive, DocumentsManage, ExitManage, ReportsView, LeaveManage, AttendanceView, AttendanceManage, SupportTickets, WallModerate, LettersManage, AssetsManage, SurveysManage, FeedbackManage, AnalyticsView, OnboardingManage, TravelManage, NotificationRulesManage, BenefitsManage, GoalsManage, ReviewsManage, ValuesManage, SkillsManage, PromotionsManage, PipManage, WorkflowsManage, ReportBuilder, HeadcountManage, PreboardingManage],
        ["HR Executive"] = [DashboardView, ProfileView, UsersView, EmployeesView, EmployeesManage, DocumentsManage, ReportsView, LeaveManage, AttendanceView, AttendanceManage, OnboardingManage],
        ["Payroll Admin"] = [DashboardView, ProfileView, EmployeesView, EmployeesSensitive, ReportsView, AttendanceView, PayrollView, PayrollManage, SupportTickets, ExpensesManage, BenefitsManage, CompensationManage],
        ["Finance User"] = [DashboardView, ProfileView, SubscriptionView, ReportsView, PayrollView, BillingManage, SupportTickets, ExpensesManage, TravelManage],
        ["Manager"] = [DashboardView],
        ["Team Leader"] = [DashboardView],
        ["Employee"] = [],
    };
}
