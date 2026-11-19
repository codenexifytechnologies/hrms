using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IFeatureAccessService, FeatureAccessService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IProvisioningService, ProvisioningService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IPlatformAuthService, PlatformAuthService>();
        services.AddScoped<ITenantAuthService, TenantAuthService>();
        services.AddScoped<IPlatformUserService, PlatformUserService>();
        services.AddScoped<ITenantAdminService, TenantAdminService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<CoreHr.IStructureService, CoreHr.StructureService>();
        services.AddScoped<CoreHr.IEmployeeService, CoreHr.EmployeeService>();
        services.AddScoped<CoreHr.IDocumentService, CoreHr.DocumentService>();
        services.AddScoped<CoreHr.IExitService, CoreHr.ExitService>();
        services.AddScoped<CoreHr.IHrReportService, CoreHr.HrReportService>();
        services.AddScoped<Time.INotificationService, Time.NotificationService>();
        services.AddScoped<Time.IAttendanceService, Time.AttendanceService>();
        services.AddScoped<Time.ILeaveService, Time.LeaveService>();
        services.AddScoped<Time.IShiftService, Time.ShiftService>();
        services.AddScoped<Time.IOvertimeService, Time.OvertimeService>();
        services.AddScoped<Time.IGamificationService, Time.GamificationService>();
        services.AddScoped<Time.ISelfServiceNav, Time.SelfServiceNav>();
        services.AddScoped<Payroll.IStatutoryRuleService, Payroll.StatutoryRuleService>();
        services.AddScoped<Payroll.IPayrollSetupService, Payroll.PayrollSetupService>();
        services.AddScoped<Payroll.IPayrollRunService, Payroll.PayrollRunService>();
        services.AddScoped<Payroll.ILoanService, Payroll.LoanService>();
        services.AddScoped<Payroll.ITaxDeclarationService, Payroll.TaxDeclarationService>();
        services.AddScoped<Payroll.ISettlementService, Payroll.SettlementService>();
        services.AddScoped<Billing.IBillingService, Billing.BillingService>();
        services.AddScoped<Billing.IBillingAutomation, Billing.BillingAutomation>();
        services.AddScoped<Support.ISupportTicketService, Support.SupportTicketService>();
        services.AddScoped<Support.ISupportAccessService, Support.SupportAccessService>();
        services.AddScoped<Support.IAnnouncementService, Support.AnnouncementService>();
        services.AddScoped<Support.ILegalPageService, Support.LegalPageService>();
        services.AddScoped<Engagement.ISocialWallService, Engagement.SocialWallService>();
        services.AddScoped<Engagement.IProfileChangeService, Engagement.ProfileChangeService>();
        services.AddScoped<Engagement.IPeopleService, Engagement.PeopleService>();
        services.AddScoped<Engagement.INotificationDispatcher, Engagement.NotificationDispatcher>();
        services.AddScoped<Documents.ILetterService, Documents.LetterService>();
        services.AddScoped<Payroll.IPayrollAdjustmentService, Payroll.PayrollAdjustmentService>();
        services.AddScoped<Payroll.IExpenseService, Payroll.ExpenseService>();
        services.AddScoped<Payroll.ITaxReturnService, Payroll.TaxReturnService>();
        services.AddScoped<Strength.IAssetService, Strength.AssetService>();
        services.AddScoped<Strength.IOfficeLocationService, Strength.OfficeLocationService>();
        services.AddScoped<Strength.ISurveyService, Strength.SurveyService>();
        services.AddScoped<Strength.IFeedbackService, Strength.FeedbackService>();
        services.AddScoped<Strength.IInsightsService, Strength.InsightsService>();
        services.AddScoped<Strength.IOnboardingService, Strength.OnboardingService>();
        services.AddScoped<Strength.ITravelService, Strength.TravelService>();
        services.AddScoped<Benefits.IBenefitService, Benefits.BenefitService>();
        services.AddScoped<Performance.IGoalService, Performance.GoalService>();
        services.AddScoped<Performance.IReviewService, Performance.ReviewService>();
        services.AddScoped<Performance.IFeedbackService, Performance.FeedbackService>();
        services.AddScoped<Performance.IOneOnOneService, Performance.OneOnOneService>();
        services.AddScoped<Performance.ICoreValueService, Performance.CoreValueService>();
        services.AddScoped<Performance.ISkillService, Performance.SkillService>();
        services.AddScoped<Performance.IPromotionService, Performance.PromotionService>();
        services.AddScoped<Performance.IPipService, Performance.PipService>();
        services.AddScoped<Performance.ICompensationService, Performance.CompensationService>();
        services.AddScoped<Performance.ICalibrationService, Performance.CalibrationService>();
        services.AddScoped<Performance.IWorkflowService, Performance.WorkflowService>();
        services.AddScoped<Performance.IReportBuilderService, Performance.ReportBuilderService>();
        services.AddScoped<Planning.IHeadcountService, Planning.HeadcountService>();
        services.AddScoped<Planning.IPreboardingService, Planning.PreboardingService>();
        services.AddScoped<Strength.ISelfieService, Strength.SelfieService>();
        services.AddScoped<Strength.ISignatureService, Strength.SignatureService>();
        services.AddScoped<Strength.IReminderService, Strength.ReminderService>();
        services.AddScoped<Identity.ISignInSettingsService, Identity.SignInSettingsService>();
        services.AddScoped<Identity.IAlternateSignInService, Identity.AlternateSignInService>();
        services.AddScoped<Identity.IPasswordResetService, Identity.PasswordResetService>();
        services.AddScoped<Strength.IReportScheduleService, Strength.ReportScheduleService>();
        return services;
    }
}
