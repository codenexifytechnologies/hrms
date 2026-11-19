using Codenexify.HRMS.Domain.Enums;

namespace Codenexify.HRMS.Application.Catalog;

/// <summary>Stable feature codes. Code references these constants; names/tiers live in the database.</summary>
public static class FeatureCodes
{
    // Foundation — Core HR
    public const string CoreHr = "CORE_HR";
    public const string DocumentsLetters = "DOCUMENTS_LETTERS";
    public const string OnboardingBasic = "ONBOARDING_BASIC";
    public const string EmployeeExit = "EMPLOYEE_EXIT";
    public const string NotificationsBasic = "NOTIFICATIONS_BASIC";
    public const string ReportsBasic = "REPORTS_BASIC";
    // Foundation — Time
    public const string Leave = "LEAVE";
    public const string Attendance = "ATTENDANCE";
    public const string AttendanceGamification = "ATTENDANCE_GAMIFICATION";
    public const string Overtime = "OVERTIME";
    public const string Shifts = "SHIFTS";
    // Foundation — Payroll & expense
    public const string Payroll = "PAYROLL";
    public const string Statutory = "STATUTORY";
    public const string AccountingIntegration = "ACCOUNTING_INTEGRATION";
    public const string Loans = "LOANS";
    public const string Benefits = "BENEFITS";
    public const string Expenses = "EXPENSES";
    public const string EmployeeTax = "EMPLOYEE_TAX";
    public const string Gratuity = "GRATUITY";
    // Foundation — ESS & experience
    public const string SelfService = "ESS";
    public const string SocialWall = "SOCIAL_WALL";
    // Strength
    public const string OnboardingAdvanced = "ONBOARDING_ADVANCED";
    public const string ESignature = "ESIGN";
    public const string TravelDesk = "TRAVEL_DESK";
    public const string Assets = "ASSETS";
    public const string PeopleAnalytics = "PEOPLE_ANALYTICS";
    public const string EmployeeTimeline = "EMPLOYEE_TIMELINE";
    public const string CustomRoles = "CUSTOM_ROLES";
    public const string ExitSurvey = "EXIT_SURVEY";
    public const string NotificationsAdvanced = "NOTIFICATIONS_ADVANCED";
    public const string ReportsAdvanced = "REPORTS_ADVANCED";
    public const string SelfieClockIn = "SELFIE_CLOCKIN";
    public const string GpsAttendance = "GPS_ATTENDANCE";
    public const string GeoFencing = "GEO_FENCING";
    public const string MultiLocation = "MULTI_LOCATION";
    public const string SingleSignOn = "SSO";
    public const string MobileOtp = "MOBILE_OTP";
    public const string PulseSurveys = "PULSE_SURVEYS";
    public const string AnonymousFeedback = "ANONYMOUS_FEEDBACK";
    // Growth
    public const string WorkflowAutomation = "WORKFLOW_AUTOMATION";
    public const string CustomReportBuilder = "CUSTOM_REPORT_BUILDER";
    public const string HeadcountPlanning = "HEADCOUNT_PLANNING";
    public const string Preboarding = "PREBOARDING";
    public const string Okrs = "OKR_GOALS";
    public const string CoreValues = "CORE_VALUES";
    public const string ContinuousFeedback = "CONTINUOUS_FEEDBACK";
    public const string OneOnOne = "ONE_ON_ONE";
    public const string PerformanceReviews = "PERFORMANCE_REVIEWS";
    public const string Calibration = "CALIBRATION";
    public const string Promotions = "PROMOTIONS";
    public const string Pip = "PIP";
    public const string CompensationPlans = "COMPENSATION_PLANS";
    public const string Skills = "SKILLS";
    // Add-ons
    public const string Recruitment = "RECRUITMENT";
    public const string Lms = "LMS";
    public const string Biometric = "BIOMETRIC";
    public const string MultiCompany = "MULTI_COMPANY";
    public const string WhatsApp = "WHATSAPP";
    public const string AiAssistant = "AI_ASSISTANT";
    public const string DocumentVerification = "DOC_VERIFICATION";
}

public sealed record FeatureDefinition(string Code, string Name, string ModuleGroup, FeatureTier Tier, string Description);

/// <summary>Initial catalog used by the seeder. After seeding, the database is the source of truth.</summary>
public static class FeatureCatalog
{
    public static readonly IReadOnlyList<FeatureDefinition> All =
    [
        new(FeatureCodes.CoreHr, "Core HR & Organization Structure", "Core HR", FeatureTier.Foundation, "Companies, branches, departments, designations, employee profiles, org chart."),
        new(FeatureCodes.DocumentsLetters, "Documents & Letters", "Core HR", FeatureTier.Foundation, "Letter templates, PDF generation, document uploads and expiry alerts."),
        new(FeatureCodes.OnboardingBasic, "Employee Onboarding (Basic)", "Core HR", FeatureTier.Foundation, "Registration, employee ID generation, document collection."),
        new(FeatureCodes.EmployeeExit, "Employee Exit", "Core HR", FeatureTier.Foundation, "Resignation, approvals, notice period, checklist, F&F."),
        new(FeatureCodes.NotificationsBasic, "Notification Engine (Basic)", "Core HR", FeatureTier.Foundation, "Email and in-app alerts, birthdays and anniversaries."),
        new(FeatureCodes.ReportsBasic, "Reports (Basic)", "Core HR", FeatureTier.Foundation, "Employee, joining/exit, attendance and leave reports."),
        new(FeatureCodes.Leave, "Leave Management", "Time & Attendance", FeatureTier.Foundation, "Leave types, accrual, carry-forward, encashment, approvals."),
        new(FeatureCodes.Attendance, "Attendance Tracking", "Time & Attendance", FeatureTier.Foundation, "Daily attendance and punches."),
        new(FeatureCodes.AttendanceGamification, "Gamified Attendance", "Time & Attendance", FeatureTier.Foundation, "Streaks, punctuality points, badges, leaderboards."),
        new(FeatureCodes.Overtime, "Overtime Automation", "Time & Attendance", FeatureTier.Foundation, "Overtime rules, calculation and approval."),
        new(FeatureCodes.Shifts, "Shift Management (Basic)", "Time & Attendance", FeatureTier.Foundation, "Shifts, weekly offs, shift allocation."),
        new(FeatureCodes.Payroll, "Payroll Automation", "Payroll & Expense", FeatureTier.Foundation, "Salary structures, payroll runs, payslips, registers."),
        new(FeatureCodes.Statutory, "Statutory Compliance", "Payroll & Expense", FeatureTier.Foundation, "PF, ESI, PT, TDS, LWF with configurable rules."),
        new(FeatureCodes.AccountingIntegration, "Accounting Integration", "Payroll & Expense", FeatureTier.Foundation, "Journal entries and accounting exports."),
        new(FeatureCodes.Loans, "Loans & Salary Advances", "Payroll & Expense", FeatureTier.Foundation, "Requests, approvals, instalments."),
        new(FeatureCodes.Benefits, "Perks & Benefits", "Payroll & Expense", FeatureTier.Foundation, "Insurance, allowances, benefit eligibility."),
        new(FeatureCodes.Expenses, "Expense Management", "Payroll & Expense", FeatureTier.Foundation, "Claims, receipts, manager and finance approvals."),
        new(FeatureCodes.EmployeeTax, "Employee Tax Management", "Payroll & Expense", FeatureTier.Foundation, "Declarations, regimes, proofs, projections."),
        new(FeatureCodes.Gratuity, "Gratuity Management", "Payroll & Expense", FeatureTier.Foundation, "Eligibility, calculation, liability reports."),
        new(FeatureCodes.SelfService, "Employee Self-Service", "Self-Service", FeatureTier.Foundation, "Employee portal, inbox, payslips, leave, attendance."),
        new(FeatureCodes.SocialWall, "Social Wall & Recognition", "Employee Experience", FeatureTier.Foundation, "Posts, polls, announcements, praise."),

        new(FeatureCodes.OnboardingAdvanced, "Advanced Onboarding", "Core HR", FeatureTier.Strength, "Onboarding workflows and task checklists."),
        new(FeatureCodes.ESignature, "Digital E-Signature", "Core HR", FeatureTier.Strength, "E-sign integration for letters and documents."),
        new(FeatureCodes.TravelDesk, "Travel Desk", "Core HR", FeatureTier.Strength, "Travel requests and bookings."),
        new(FeatureCodes.Assets, "Asset Tracking", "Core HR", FeatureTier.Strength, "Asset register, allocation and return."),
        new(FeatureCodes.PeopleAnalytics, "People Analytics", "Core HR", FeatureTier.Strength, "Workforce dashboards and insights."),
        new(FeatureCodes.EmployeeTimeline, "Employee Timeline", "Core HR", FeatureTier.Strength, "Chronological employee history."),
        new(FeatureCodes.CustomRoles, "Custom Roles & Privileges", "Core HR", FeatureTier.Strength, "Create organization-specific roles."),
        new(FeatureCodes.ExitSurvey, "Exit Survey", "Core HR", FeatureTier.Strength, "Structured exit interviews."),
        new(FeatureCodes.NotificationsAdvanced, "Advanced Notification Engine", "Core HR", FeatureTier.Strength, "Rule-based, multi-channel notifications."),
        new(FeatureCodes.ReportsAdvanced, "Advanced Reports", "Core HR", FeatureTier.Strength, "Advanced filters and scheduled reports."),
        new(FeatureCodes.SelfieClockIn, "Selfie Clock-in", "Time & Attendance", FeatureTier.Strength, "Photo verified punches."),
        new(FeatureCodes.GpsAttendance, "GPS Attendance", "Time & Attendance", FeatureTier.Strength, "Location-stamped punches with consent."),
        new(FeatureCodes.GeoFencing, "Geo Fencing", "Time & Attendance", FeatureTier.Strength, "Location-based attendance validation."),
        new(FeatureCodes.MultiLocation, "Multiple Office Locations", "Time & Attendance", FeatureTier.Strength, "Several office geo-locations per organization."),
        new(FeatureCodes.SingleSignOn, "Single Sign-On", "Self-Service", FeatureTier.Strength, "Microsoft and Google login."),
        new(FeatureCodes.MobileOtp, "Mobile OTP Login", "Self-Service", FeatureTier.Strength, "One-time password sign in."),
        new(FeatureCodes.PulseSurveys, "Pulse & Surveys", "Employee Experience", FeatureTier.Strength, "Pulse checks, surveys and engagement analytics."),
        new(FeatureCodes.AnonymousFeedback, "Anonymous Feedback", "Employee Experience", FeatureTier.Strength, "Confidential feedback channel."),

        new(FeatureCodes.WorkflowAutomation, "Workflow Automation", "Core HR", FeatureTier.Growth, "Configurable multi-step approval workflows."),
        new(FeatureCodes.CustomReportBuilder, "Custom Reports Builder", "Core HR", FeatureTier.Growth, "Build and save custom reports."),
        new(FeatureCodes.HeadcountPlanning, "Headcount Planning", "Core HR", FeatureTier.Growth, "Plan positions and budgets."),
        new(FeatureCodes.Preboarding, "Pre-boarding", "Core HR", FeatureTier.Growth, "Engage new hires before day one."),
        new(FeatureCodes.Okrs, "Goals & OKRs", "Business Performance", FeatureTier.Growth, "Company, department and individual goals with alignment."),
        new(FeatureCodes.CoreValues, "Core Values", "Business Performance", FeatureTier.Growth, "Define and recognise core values."),
        new(FeatureCodes.ContinuousFeedback, "Continuous Feedback", "Employee Performance", FeatureTier.Growth, "Ongoing feedback and praise."),
        new(FeatureCodes.OneOnOne, "One-on-One Meetings", "Employee Performance", FeatureTier.Growth, "Agenda and notes for 1:1s."),
        new(FeatureCodes.PerformanceReviews, "Performance Reviews", "Employee Performance", FeatureTier.Growth, "Review cycles and forms."),
        new(FeatureCodes.Calibration, "Performance Calibration", "Employee Performance", FeatureTier.Growth, "Rating normalisation."),
        new(FeatureCodes.Promotions, "Promotions", "Employee Performance", FeatureTier.Growth, "Promotion workflows."),
        new(FeatureCodes.Pip, "Performance Improvement Plans", "Employee Performance", FeatureTier.Growth, "PIP tracking."),
        new(FeatureCodes.CompensationPlans, "Compensation Plans", "Employee Performance", FeatureTier.Growth, "Basic compensation planning."),
        new(FeatureCodes.Skills, "Skills & Competencies", "Employee Performance", FeatureTier.Growth, "Skills, gap analysis, development recommendations."),

        new(FeatureCodes.Recruitment, "Recruitment & ATS", "Add-ons", FeatureTier.AddOn, "Jobs, careers page, pipeline, interviews, offers."),
        new(FeatureCodes.Lms, "Learning Management", "Add-ons", FeatureTier.AddOn, "Courses, assessments, certificates."),
        new(FeatureCodes.Biometric, "Biometric Integration", "Add-ons", FeatureTier.AddOn, "Device sync and reconciliation."),
        new(FeatureCodes.MultiCompany, "Multi-Company Management", "Add-ons", FeatureTier.AddOn, "Multiple legal and payroll entities."),
        new(FeatureCodes.WhatsApp, "WhatsApp Notifications", "Add-ons", FeatureTier.AddOn, "WhatsApp Business API notifications."),
        new(FeatureCodes.AiAssistant, "AI HR Assistant", "Add-ons", FeatureTier.AddOn, "Role-aware HR chatbot."),
        new(FeatureCodes.DocumentVerification, "Document Verification", "Add-ons", FeatureTier.AddOn, "PAN, bank, education and employment verification."),
    ];
}
