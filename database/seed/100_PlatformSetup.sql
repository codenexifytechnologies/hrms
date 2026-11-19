/* =====================================================================================================================
   Codenexify HRMS - platform setup (starting data)
   Exported 2026-11-16 21:52 from [CodenexifyHRMS] by database/Generate-SeedScripts.ps1. Do not edit by hand; regenerate instead.

   Contains: modules (features), permissions, subscription plans with prices/limits and their modules, statutory
   rules (PF/ESI/gratuity, income tax, professional tax, LWF), public policy pages, Control Center settings and
   announcements. No company data.

   RUN ORDER on a new database:
     1. database/000_FullDatabase.sql
     2. database/seed/100_PlatformSetup.sql   (this file)
     3. database/seed/110_SuperAdmins.sql
     4. Start the application.
   e.g.  sqlcmd -S <server> -d CodenexifyHRMS -E -b -i database\seed\100_PlatformSetup.sql

   Safe to run again: each row is inserted only if its key is missing; existing rows are never changed.
   Saved secrets are NOT included (they are encrypted with the source server's keys). SMTP, payment gateway and SMS
   are switched off here: re-enter their passwords/keys in Control Center -> Billing & email settings, then switch
   them on.
   ===================================================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL OR NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261114093445_HeadcountAndPreboarding')
    THROW 50000, 'Create the schema first: run database/000_FullDatabase.sql (it must include migration 20261114093445_HeadcountAndPreboarding).', 1;
BEGIN TRANSACTION;

-- ---------------------------------------------------------------- Modules (features) (60)
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CORE_HR')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CORE_HR', N'Core HR & Organization Structure', N'Companies, branches, departments, designations, employee profiles, org chart.', N'Core HR', N'Foundation', 1, 10, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'DOCUMENTS_LETTERS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'DOCUMENTS_LETTERS', N'Documents & Letters', N'Letter templates, PDF generation, document uploads and expiry alerts.', N'Core HR', N'Foundation', 1, 20, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ONBOARDING_BASIC')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ONBOARDING_BASIC', N'Employee Onboarding (Basic)', N'Registration, employee ID generation, document collection.', N'Core HR', N'Foundation', 1, 30, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'EMPLOYEE_EXIT')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'EMPLOYEE_EXIT', N'Employee Exit', N'Resignation, approvals, notice period, checklist, F&F.', N'Core HR', N'Foundation', 1, 40, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'NOTIFICATIONS_BASIC')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'NOTIFICATIONS_BASIC', N'Notification Engine (Basic)', N'Email and in-app alerts, birthdays and anniversaries.', N'Core HR', N'Foundation', 1, 50, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'REPORTS_BASIC')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'REPORTS_BASIC', N'Reports (Basic)', N'Employee, joining/exit, attendance and leave reports.', N'Core HR', N'Foundation', 1, 60, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'LEAVE')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'LEAVE', N'Leave Management', N'Leave types, accrual, carry-forward, encashment, approvals.', N'Time & Attendance', N'Foundation', 1, 70, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ATTENDANCE')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ATTENDANCE', N'Attendance Tracking', N'Daily attendance and punches.', N'Time & Attendance', N'Foundation', 1, 80, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ATTENDANCE_GAMIFICATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ATTENDANCE_GAMIFICATION', N'Gamified Attendance', N'Streaks, punctuality points, badges, leaderboards.', N'Time & Attendance', N'Foundation', 1, 90, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'OVERTIME')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'OVERTIME', N'Overtime Automation', N'Overtime rules, calculation and approval.', N'Time & Attendance', N'Foundation', 1, 100, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'SHIFTS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'SHIFTS', N'Shift Management (Basic)', N'Shifts, weekly offs, shift allocation.', N'Time & Attendance', N'Foundation', 1, 110, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PAYROLL')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PAYROLL', N'Payroll Automation', N'Salary structures, payroll runs, payslips, registers.', N'Payroll & Expense', N'Foundation', 1, 120, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'STATUTORY')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'STATUTORY', N'Statutory Compliance', N'PF, ESI, PT, TDS, LWF with configurable rules.', N'Payroll & Expense', N'Foundation', 1, 130, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ACCOUNTING_INTEGRATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ACCOUNTING_INTEGRATION', N'Accounting Integration', N'Journal entries and accounting exports.', N'Payroll & Expense', N'Foundation', 1, 140, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'LOANS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'LOANS', N'Loans & Salary Advances', N'Requests, approvals, instalments.', N'Payroll & Expense', N'Foundation', 1, 150, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'BENEFITS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'BENEFITS', N'Perks & Benefits', N'Insurance, allowances, benefit eligibility.', N'Payroll & Expense', N'Foundation', 1, 160, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'EXPENSES')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'EXPENSES', N'Expense Management', N'Claims, receipts, manager and finance approvals.', N'Payroll & Expense', N'Foundation', 1, 170, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'EMPLOYEE_TAX')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'EMPLOYEE_TAX', N'Employee Tax Management', N'Declarations, regimes, proofs, projections.', N'Payroll & Expense', N'Foundation', 1, 180, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'GRATUITY')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'GRATUITY', N'Gratuity Management', N'Eligibility, calculation, liability reports.', N'Payroll & Expense', N'Foundation', 1, 190, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ESS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ESS', N'Employee Self-Service', N'Employee portal, inbox, payslips, leave, attendance.', N'Self-Service', N'Foundation', 1, 200, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'SOCIAL_WALL')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'SOCIAL_WALL', N'Social Wall & Recognition', N'Posts, polls, announcements, praise.', N'Employee Experience', N'Foundation', 1, 210, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ONBOARDING_ADVANCED')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ONBOARDING_ADVANCED', N'Advanced Onboarding', N'Onboarding workflows and task checklists.', N'Core HR', N'Strength', 1, 220, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ESIGN')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ESIGN', N'Digital E-Signature', N'E-sign integration for letters and documents.', N'Core HR', N'Strength', 1, 230, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'TRAVEL_DESK')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'TRAVEL_DESK', N'Travel Desk', N'Travel requests and bookings.', N'Core HR', N'Strength', 1, 240, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ASSETS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ASSETS', N'Asset Tracking', N'Asset register, allocation and return.', N'Core HR', N'Strength', 1, 250, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PEOPLE_ANALYTICS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PEOPLE_ANALYTICS', N'People Analytics', N'Workforce dashboards and insights.', N'Core HR', N'Strength', 1, 260, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'EMPLOYEE_TIMELINE')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'EMPLOYEE_TIMELINE', N'Employee Timeline', N'Chronological employee history.', N'Core HR', N'Strength', 1, 270, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CUSTOM_ROLES')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CUSTOM_ROLES', N'Custom Roles & Privileges', N'Create organization-specific roles.', N'Core HR', N'Strength', 1, 280, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'EXIT_SURVEY')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'EXIT_SURVEY', N'Exit Survey', N'Structured exit interviews.', N'Core HR', N'Strength', 1, 290, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'NOTIFICATIONS_ADVANCED')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'NOTIFICATIONS_ADVANCED', N'Advanced Notification Engine', N'Rule-based, multi-channel notifications.', N'Core HR', N'Strength', 1, 300, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'REPORTS_ADVANCED')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'REPORTS_ADVANCED', N'Advanced Reports', N'Advanced filters and scheduled reports.', N'Core HR', N'Strength', 1, 310, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'SELFIE_CLOCKIN')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'SELFIE_CLOCKIN', N'Selfie Clock-in', N'Photo verified punches.', N'Time & Attendance', N'Strength', 1, 320, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'GPS_ATTENDANCE')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'GPS_ATTENDANCE', N'GPS Attendance', N'Location-stamped punches with consent.', N'Time & Attendance', N'Strength', 1, 330, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'GEO_FENCING')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'GEO_FENCING', N'Geo Fencing', N'Location-based attendance validation.', N'Time & Attendance', N'Strength', 1, 340, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'MULTI_LOCATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'MULTI_LOCATION', N'Multiple Office Locations', N'Several office geo-locations per organization.', N'Time & Attendance', N'Strength', 1, 350, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'SSO')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'SSO', N'Single Sign-On', N'Microsoft and Google login.', N'Self-Service', N'Strength', 1, 360, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'MOBILE_OTP')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'MOBILE_OTP', N'Mobile OTP Login', N'One-time password sign in.', N'Self-Service', N'Strength', 1, 370, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PULSE_SURVEYS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PULSE_SURVEYS', N'Pulse & Surveys', N'Pulse checks, surveys and engagement analytics.', N'Employee Experience', N'Strength', 1, 380, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ANONYMOUS_FEEDBACK')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ANONYMOUS_FEEDBACK', N'Anonymous Feedback', N'Confidential feedback channel.', N'Employee Experience', N'Strength', 1, 390, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'WORKFLOW_AUTOMATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'WORKFLOW_AUTOMATION', N'Workflow Automation', N'Configurable multi-step approval workflows.', N'Core HR', N'Growth', 1, 400, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CUSTOM_REPORT_BUILDER')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CUSTOM_REPORT_BUILDER', N'Custom Reports Builder', N'Build and save custom reports.', N'Core HR', N'Growth', 1, 410, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'HEADCOUNT_PLANNING')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'HEADCOUNT_PLANNING', N'Headcount Planning', N'Plan positions and budgets.', N'Core HR', N'Growth', 1, 420, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PREBOARDING')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PREBOARDING', N'Pre-boarding', N'Engage new hires before day one.', N'Core HR', N'Growth', 1, 430, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'OKR_GOALS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'OKR_GOALS', N'Goals & OKRs', N'Company, department and individual goals with alignment.', N'Business Performance', N'Growth', 1, 440, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CORE_VALUES')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CORE_VALUES', N'Core Values', N'Define and recognise core values.', N'Business Performance', N'Growth', 1, 450, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CONTINUOUS_FEEDBACK')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CONTINUOUS_FEEDBACK', N'Continuous Feedback', N'Ongoing feedback and praise.', N'Employee Performance', N'Growth', 1, 460, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'ONE_ON_ONE')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ONE_ON_ONE', N'One-on-One Meetings', N'Agenda and notes for 1:1s.', N'Employee Performance', N'Growth', 1, 470, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PERFORMANCE_REVIEWS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PERFORMANCE_REVIEWS', N'Performance Reviews', N'Review cycles and forms.', N'Employee Performance', N'Growth', 1, 480, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'CALIBRATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'CALIBRATION', N'Performance Calibration', N'Rating normalisation.', N'Employee Performance', N'Growth', 1, 490, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PROMOTIONS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PROMOTIONS', N'Promotions', N'Promotion workflows.', N'Employee Performance', N'Growth', 1, 500, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'PIP')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'PIP', N'Performance Improvement Plans', N'PIP tracking.', N'Employee Performance', N'Growth', 1, 510, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'COMPENSATION_PLANS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'COMPENSATION_PLANS', N'Compensation Plans', N'Basic compensation planning.', N'Employee Performance', N'Growth', 1, 520, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'SKILLS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'SKILLS', N'Skills & Competencies', N'Skills, gap analysis, development recommendations.', N'Employee Performance', N'Growth', 1, 530, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'RECRUITMENT')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'RECRUITMENT', N'Recruitment & ATS', N'Jobs, careers page, pipeline, interviews, offers.', N'Add-ons', N'AddOn', 1, 540, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'LMS')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'LMS', N'Learning Management', N'Courses, assessments, certificates.', N'Add-ons', N'AddOn', 1, 550, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'BIOMETRIC')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'BIOMETRIC', N'Biometric Integration', N'Device sync and reconciliation.', N'Add-ons', N'AddOn', 1, 560, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'MULTI_COMPANY')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'MULTI_COMPANY', N'Multi-Company Management', N'Multiple legal and payroll entities.', N'Add-ons', N'AddOn', 1, 570, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'WHATSAPP')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'WHATSAPP', N'WhatsApp Notifications', N'WhatsApp Business API notifications.', N'Add-ons', N'AddOn', 1, 580, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'AI_ASSISTANT')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'AI_ASSISTANT', N'AI HR Assistant', N'Role-aware HR chatbot.', N'Add-ons', N'AddOn', 1, 590, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [Features] WHERE [Code] = N'DOC_VERIFICATION')
    INSERT INTO [Features] ([Code], [Name], [Description], [ModuleGroup], [Tier], [IsGloballyEnabled], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'DOC_VERIFICATION', N'Document Verification', N'PAN, bank, education and employment verification.', N'Add-ons', N'AddOn', 1, 600, '2026-11-11T18:28:28.3125663', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Permissions (46)
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.dashboard.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.dashboard.view', N'View organization dashboard', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.profile.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.profile.view', N'View company profile', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.profile.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.profile.manage', N'Edit company profile & branding', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.users.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.users.view', N'View users', N'Users & Roles', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.users.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.users.manage', N'Create / edit / disable users', N'Users & Roles', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.roles.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.roles.manage', N'Manage roles & permissions', N'Users & Roles', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.subscription.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.subscription.view', N'View subscription & plan', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.audit.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.audit.view', N'View organization audit trail', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.structure.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.structure.manage', N'Manage branches, departments, designations & HR settings', N'Core HR', N'CORE_HR');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.employees.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.employees.view', N'View employee directory & profiles', N'Core HR', N'CORE_HR');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.employees.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.employees.manage', N'Add / edit employees', N'Core HR', N'CORE_HR');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.employees.sensitive')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.employees.sensitive', N'View / edit bank details & PAN', N'Core HR', N'CORE_HR');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.documents.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.documents.manage', N'Upload, verify & delete employee documents', N'Core HR', N'DOCUMENTS_LETTERS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.exit.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.exit.manage', N'Manage resignations & exits', N'Core HR', N'EMPLOYEE_EXIT');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.reports.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.reports.view', N'View & export HR reports', N'Reports', N'REPORTS_BASIC');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.leave.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.leave.manage', N'Manage leave types, holidays, balances & approve any leave', N'Time & Attendance', N'LEAVE');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.attendance.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.attendance.view', N'View attendance of all employees', N'Time & Attendance', N'ATTENDANCE');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.attendance.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.attendance.manage', N'Manage shifts, regularize attendance & approve overtime', N'Time & Attendance', N'ATTENDANCE');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.payroll.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.payroll.view', N'View payroll runs, salary registers & reports', N'Payroll', N'PAYROLL');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.payroll.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.payroll.manage', N'Manage salary structures & salaries, run and finalize payroll, loans, tax & F&F', N'Payroll', N'PAYROLL');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.billing.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.billing.manage', N'Buy plans, view invoices & pay online', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.support.tickets')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.support.tickets', N'Raise and follow support tickets with Codenexify', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.support.access')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.support.access', N'Grant / revoke Codenexify read-only support access', N'Administration', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.wall.moderate')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.wall.moderate', N'Post announcements, pin and remove posts on the social wall', N'Employee Experience', N'SOCIAL_WALL');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.letters.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.letters.manage', N'Manage letter templates and issue letters (offer, appointment, experience…)', N'Core HR', N'DOCUMENTS_LETTERS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.expenses.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.expenses.manage', N'Approve and pay expense claims, manage expense categories', N'Payroll', N'EXPENSES');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.assets.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.assets.manage', N'Manage the asset register, assign and take back assets', N'Core HR', N'ASSETS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.analytics.view')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.analytics.view', N'View people analytics dashboards', N'Reports', N'PEOPLE_ANALYTICS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.surveys.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.surveys.manage', N'Create surveys and view results', N'Employee Experience', N'PULSE_SURVEYS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.feedback.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.feedback.manage', N'Read and answer anonymous feedback', N'Employee Experience', N'ANONYMOUS_FEEDBACK');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.onboarding.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.onboarding.manage', N'Manage onboarding templates and new-joiner onboarding plans', N'Core HR', N'ONBOARDING_ADVANCED');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.travel.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.travel.manage', N'Travel desk: book approved trips and record advances', N'Core HR', N'TRAVEL_DESK');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.notifications.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.notifications.manage', N'Configure automatic reminder rules', N'Administration', N'NOTIFICATIONS_ADVANCED');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.security.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.security.manage', N'Configure sign-in options (single sign-on, mobile OTP)', N'Users & Roles', NULL);
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.benefits.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.benefits.manage', N'Manage perks & benefit plans, eligibility and enrolments', N'Payroll', N'BENEFITS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.goals.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.goals.manage', N'Set company and department goals; see and edit everyone''s goals', N'Performance', N'OKR_GOALS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.reviews.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.reviews.manage', N'Run review cycles, review employees without a manager, adjust ratings and release results', N'Performance', N'PERFORMANCE_REVIEWS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.values.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.values.manage', N'Define the company''s core values and remove inappropriate shout-outs', N'Performance', N'CORE_VALUES');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.skills.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.skills.manage', N'Manage the skill catalog and job-title requirements; see everyone''s skills', N'Performance', N'SKILLS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.promotions.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.promotions.manage', N'Approve or reject promotions and apply the new title and salary', N'Performance', N'PROMOTIONS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.pip.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.pip.manage', N'Start, review and close performance improvement plans for anyone', N'Performance', N'PIP');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.compensation.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.compensation.manage', N'Run salary-revision cycles: budgets, guidelines, every raise, approval', N'Performance', N'COMPENSATION_PLANS');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.workflows.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.workflows.manage', N'Create automation rules (notify, tasks, onboarding, benefits) and see their runs', N'Administration', N'WORKFLOW_AUTOMATION');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.reports.build')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.reports.build', N'Build, save and export custom reports on employees, leave and attendance (payroll needs payroll access)', N'Reports', N'CUSTOM_REPORT_BUILDER');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.headcount.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.headcount.manage', N'Plan headcount: positions, openings and budgets by department, and link new hires to them', N'Administration', N'HEADCOUNT_PLANNING');
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Code] = N'org.preboarding.manage')
    INSERT INTO [Permissions] ([Code], [Name], [Module], [FeatureCode]) VALUES (N'org.preboarding.manage', N'Set up pre-boarding and send new hires their secure welcome link', N'Core HR', N'PREBOARDING');

-- ---------------------------------------------------------------- Subscription plans (4)
IF NOT EXISTS (SELECT 1 FROM [SubscriptionPlans] WHERE [Code] = N'FOUNDATION')
    INSERT INTO [SubscriptionPlans] ([Code], [Name], [Description], [ParentPlanId], [MonthlyPrice], [YearlyPrice], [IsPerEmployeePricing], [TrialDays], [MaxEmployees], [MaxBranches], [StorageLimitGb], [IsCustom], [IsActive], [IsPublic], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'FOUNDATION', N'Foundation', N'Essential HR, attendance, leave, payroll and self-service.', NULL, 99.00, 1069.00, 1, 14, 100, 1, 10, 0, 1, 1, 10, '2026-11-11T18:28:28.8810509', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [SubscriptionPlans] WHERE [Code] = N'STRENGTH')
    INSERT INTO [SubscriptionPlans] ([Code], [Name], [Description], [ParentPlanId], [MonthlyPrice], [YearlyPrice], [IsPerEmployeePricing], [TrialDays], [MaxEmployees], [MaxBranches], [StorageLimitGb], [IsCustom], [IsActive], [IsPublic], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'STRENGTH', N'Strength', N'Advanced automation: geo attendance, assets, SSO, analytics, surveys.', (SELECT [Id] FROM [SubscriptionPlans] WHERE [Code] = N'FOUNDATION'), 149.00, 1609.00, 1, 14, 500, 5, 50, 0, 1, 1, 20, '2026-11-11T18:28:28.9535612', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [SubscriptionPlans] WHERE [Code] = N'GROWTH')
    INSERT INTO [SubscriptionPlans] ([Code], [Name], [Description], [ParentPlanId], [MonthlyPrice], [YearlyPrice], [IsPerEmployeePricing], [TrialDays], [MaxEmployees], [MaxBranches], [StorageLimitGb], [IsCustom], [IsActive], [IsPublic], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'GROWTH', N'Growth', N'Performance and business management: OKRs, reviews, workflows, skills.', (SELECT [Id] FROM [SubscriptionPlans] WHERE [Code] = N'STRENGTH'), 199.00, 2149.00, 1, 14, 2000, 20, 200, 0, 1, 1, 30, '2026-11-11T18:28:28.9725038', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [SubscriptionPlans] WHERE [Code] = N'ENTERPRISE')
    INSERT INTO [SubscriptionPlans] ([Code], [Name], [Description], [ParentPlanId], [MonthlyPrice], [YearlyPrice], [IsPerEmployeePricing], [TrialDays], [MaxEmployees], [MaxBranches], [StorageLimitGb], [IsCustom], [IsActive], [IsPublic], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'ENTERPRISE', N'Custom Enterprise', N'Tailored modules, limits and pricing for large organizations.', NULL, 0.00, 0.00, 1, 30, 100000, 1000, 1000, 1, 1, 0, 40, '2026-11-11T18:28:28.9725038', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Modules in each plan (113)
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'CORE_HR' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'DOCUMENTS_LETTERS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'ONBOARDING_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'EMPLOYEE_EXIT' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'NOTIFICATIONS_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'REPORTS_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'LEAVE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'ATTENDANCE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'ATTENDANCE_GAMIFICATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'OVERTIME' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'SHIFTS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'PAYROLL' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'STATUTORY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'ACCOUNTING_INTEGRATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'LOANS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'BENEFITS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'EXPENSES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'EMPLOYEE_TAX' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'GRATUITY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'ESS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'FOUNDATION' AND f.[Code] = N'SOCIAL_WALL' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'ONBOARDING_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'ESIGN' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'TRAVEL_DESK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'ASSETS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'PEOPLE_ANALYTICS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'EMPLOYEE_TIMELINE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'CUSTOM_ROLES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'EXIT_SURVEY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'NOTIFICATIONS_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'REPORTS_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'SELFIE_CLOCKIN' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'GPS_ATTENDANCE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'GEO_FENCING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'MULTI_LOCATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'SSO' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'MOBILE_OTP' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'PULSE_SURVEYS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'STRENGTH' AND f.[Code] = N'ANONYMOUS_FEEDBACK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'WORKFLOW_AUTOMATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'CUSTOM_REPORT_BUILDER' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'HEADCOUNT_PLANNING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'PREBOARDING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'OKR_GOALS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'CORE_VALUES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'CONTINUOUS_FEEDBACK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'ONE_ON_ONE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'PERFORMANCE_REVIEWS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'CALIBRATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'PROMOTIONS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'PIP' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'COMPENSATION_PLANS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'GROWTH' AND f.[Code] = N'SKILLS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CORE_HR' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'DOCUMENTS_LETTERS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ONBOARDING_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'EMPLOYEE_EXIT' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'NOTIFICATIONS_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'REPORTS_BASIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'LEAVE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ATTENDANCE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ATTENDANCE_GAMIFICATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'OVERTIME' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'SHIFTS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PAYROLL' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'STATUTORY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ACCOUNTING_INTEGRATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'LOANS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'BENEFITS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'EXPENSES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'EMPLOYEE_TAX' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'GRATUITY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ESS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'SOCIAL_WALL' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ONBOARDING_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ESIGN' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'TRAVEL_DESK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ASSETS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PEOPLE_ANALYTICS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'EMPLOYEE_TIMELINE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CUSTOM_ROLES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'EXIT_SURVEY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'NOTIFICATIONS_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'REPORTS_ADVANCED' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'SELFIE_CLOCKIN' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'GPS_ATTENDANCE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'GEO_FENCING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'MULTI_LOCATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'SSO' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'MOBILE_OTP' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PULSE_SURVEYS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ANONYMOUS_FEEDBACK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'WORKFLOW_AUTOMATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CUSTOM_REPORT_BUILDER' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'HEADCOUNT_PLANNING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PREBOARDING' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'OKR_GOALS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CORE_VALUES' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CONTINUOUS_FEEDBACK' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'ONE_ON_ONE' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PERFORMANCE_REVIEWS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'CALIBRATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PROMOTIONS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'PIP' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'COMPENSATION_PLANS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'SKILLS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'RECRUITMENT' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'LMS' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'BIOMETRIC' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'MULTI_COMPANY' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'WHATSAPP' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'AI_ASSISTANT' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);
INSERT INTO [PlanFeatures] ([PlanId], [FeatureId]) SELECT p.[Id], f.[Id] FROM [SubscriptionPlans] p CROSS JOIN [Features] f WHERE p.[Code] = N'ENTERPRISE' AND f.[Code] = N'DOC_VERIFICATION' AND NOT EXISTS (SELECT 1 FROM [PlanFeatures] x WHERE x.[PlanId] = p.[Id] AND x.[FeatureId] = f.[Id]);

-- ---------------------------------------------------------------- PF / ESI / gratuity rates (1)
IF NOT EXISTS (SELECT 1 FROM [StatutoryRateSets] WHERE [EffectiveFrom] = '2017-01-01')
    INSERT INTO [StatutoryRateSets] ([EffectiveFrom], [PfEmployeeRate], [PfEmployerRate], [EpsRate], [PfWageCeiling], [EdliRate], [PfAdminChargesRate], [EsiEmployeeRate], [EsiEmployerRate], [EsiWageLimit], [GratuityMinServiceYears], [GratuityDaysPerYear], [GratuityWageDivisor], [GratuityCeiling], [IsVerified], [Notes], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES ('2017-01-01', 12.0000, 12.0000, 8.3300, 15000.00, 0.5000, 0.5000, 0.7500, 3.2500, 21000.00, 5.0000, 15.0000, 26.0000, 2000000.00, 0, N'Seeded starting values. Verify against current EPFO/ESIC notifications and the Payment of Gratuity Act before use.', '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Income tax rules (4)
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New')
    INSERT INTO [IncomeTaxRules] ([FinancialYear], [Regime], [StandardDeduction], [RebateIncomeLimit], [RebateMaxAmount], [CessRate], [Section80CLimit], [Section80DLimit], [Section80CCD1BLimit], [HomeLoanInterestLimit], [IsVerified], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (2025, N'New', 75000.00, 1200000.00, 60000.00, 4.0000, 0.00, 0.00, 0.00, 0.00, 0, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old')
    INSERT INTO [IncomeTaxRules] ([FinancialYear], [Regime], [StandardDeduction], [RebateIncomeLimit], [RebateMaxAmount], [CessRate], [Section80CLimit], [Section80DLimit], [Section80CCD1BLimit], [HomeLoanInterestLimit], [IsVerified], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (2025, N'Old', 50000.00, 500000.00, 12500.00, 4.0000, 150000.00, 25000.00, 50000.00, 200000.00, 0, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New')
    INSERT INTO [IncomeTaxRules] ([FinancialYear], [Regime], [StandardDeduction], [RebateIncomeLimit], [RebateMaxAmount], [CessRate], [Section80CLimit], [Section80DLimit], [Section80CCD1BLimit], [HomeLoanInterestLimit], [IsVerified], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (2026, N'New', 75000.00, 1200000.00, 60000.00, 4.0000, 0.00, 0.00, 0.00, 0.00, 0, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old')
    INSERT INTO [IncomeTaxRules] ([FinancialYear], [Regime], [StandardDeduction], [RebateIncomeLimit], [RebateMaxAmount], [CessRate], [Section80CLimit], [Section80DLimit], [Section80CCD1BLimit], [HomeLoanInterestLimit], [IsVerified], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (2026, N'Old', 50000.00, 500000.00, 12500.00, 4.0000, 150000.00, 25000.00, 50000.00, 200000.00, 0, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Income tax slabs (22)
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 0.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 0.00, 400000.00, 0.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 400000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 400000.00, 800000.00, 5.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 800000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 800000.00, 1200000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 1200000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 1200000.00, 1600000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 1600000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 1600000.00, 2000000.00, 20.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 2000000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 2000000.00, 2400000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [FromIncome] = 2400000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 2400000.00, NULL, 30.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [FromIncome] = 0.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 0.00, 250000.00, 0.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [FromIncome] = 250000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 250000.00, 500000.00, 5.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [FromIncome] = 500000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 500000.00, 1000000.00, 20.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [FromIncome] = 1000000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 1000000.00, NULL, 30.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 0.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 0.00, 400000.00, 0.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 400000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 400000.00, 800000.00, 5.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 800000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 800000.00, 1200000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 1200000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 1200000.00, 1600000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 1600000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 1600000.00, 2000000.00, 20.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 2000000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 2000000.00, 2400000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [FromIncome] = 2400000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 2400000.00, NULL, 30.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [FromIncome] = 0.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 0.00, 250000.00, 0.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [FromIncome] = 250000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 250000.00, 500000.00, 5.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [FromIncome] = 500000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 500000.00, 1000000.00, 20.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSlabs] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [FromIncome] = 1000000.00)
    INSERT INTO [IncomeTaxSlabs] ([IncomeTaxRuleId], [FromIncome], [ToIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 1000000.00, NULL, 30.0000);

-- ---------------------------------------------------------------- Income tax surcharges (14)
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [AboveIncome] = 5000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 5000000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [AboveIncome] = 10000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 10000000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New') AND [AboveIncome] = 20000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'New'), 20000000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [AboveIncome] = 5000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 5000000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [AboveIncome] = 10000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 10000000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [AboveIncome] = 20000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 20000000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old') AND [AboveIncome] = 50000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2025 AND [Regime] = N'Old'), 50000000.00, 37.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [AboveIncome] = 5000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 5000000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [AboveIncome] = 10000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 10000000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New') AND [AboveIncome] = 20000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'New'), 20000000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [AboveIncome] = 5000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 5000000.00, 10.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [AboveIncome] = 10000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 10000000.00, 15.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [AboveIncome] = 20000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 20000000.00, 25.0000);
IF NOT EXISTS (SELECT 1 FROM [IncomeTaxSurcharges] WHERE [IncomeTaxRuleId] = (SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old') AND [AboveIncome] = 50000000.00)
    INSERT INTO [IncomeTaxSurcharges] ([IncomeTaxRuleId], [AboveIncome], [Rate]) VALUES ((SELECT [Id] FROM [IncomeTaxRules] WHERE [FinancialYear] = 2026 AND [Regime] = N'Old'), 50000000.00, 37.0000);

-- ---------------------------------------------------------------- Professional tax slabs (15)
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Gujarat' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Gujarat', '2017-01-01', NULL, 0.00, 11999.99, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Gujarat' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 12000.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Gujarat', '2017-01-01', NULL, 12000.00, NULL, 200.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Karnataka' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Karnataka', '2017-01-01', NULL, 0.00, 24999.99, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Karnataka' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 25000.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Karnataka', '2017-01-01', NULL, 25000.00, NULL, 200.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Other' AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Other', 0.00, 7500.00, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Male' AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Male', 0.00, 7500.00, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Female' AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Female', 0.00, 25000.00, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Male' AND [MinMonthlyGross] = 7500.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Male', 7500.01, 10000.00, 175.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Other' AND [MinMonthlyGross] = 7500.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Other', 7500.01, 10000.00, 175.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Other' AND [MinMonthlyGross] = 10000.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Other', 10000.01, NULL, 200.00, 2, 300.00, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Male' AND [MinMonthlyGross] = 10000.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Male', 10000.01, NULL, 200.00, 2, 300.00, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01' AND [Gender] = N'Female' AND [MinMonthlyGross] = 25000.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', N'Female', 25000.01, NULL, 200.00, 2, 300.00, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Telangana' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 0.00)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Telangana', '2017-01-01', NULL, 0.00, 15000.00, 0.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Telangana' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 15000.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Telangana', '2017-01-01', NULL, 15000.01, 20000.00, 150.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [ProfessionalTaxSlabs] WHERE [State] = N'Telangana' AND [EffectiveFrom] = '2017-01-01' AND [Gender] IS NULL AND [MinMonthlyGross] = 20000.01)
    INSERT INTO [ProfessionalTaxSlabs] ([State], [EffectiveFrom], [Gender], [MinMonthlyGross], [MaxMonthlyGross], [MonthlyAmount], [SpecialMonth], [SpecialMonthAmount], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Telangana', '2017-01-01', NULL, 20000.01, NULL, 200.00, NULL, NULL, '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Labour welfare fund (2)
IF NOT EXISTS (SELECT 1 FROM [LabourWelfareFundRules] WHERE [State] = N'Karnataka' AND [EffectiveFrom] = '2017-01-01')
    INSERT INTO [LabourWelfareFundRules] ([State], [EffectiveFrom], [EmployeeAmount], [EmployerAmount], [Months], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Karnataka', '2017-01-01', 50.00, 100.00, N'12', '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [LabourWelfareFundRules] WHERE [State] = N'Maharashtra' AND [EffectiveFrom] = '2017-01-01')
    INSERT INTO [LabourWelfareFundRules] ([State], [EffectiveFrom], [EmployeeAmount], [EmployerAmount], [Months], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'Maharashtra', '2017-01-01', 25.00, 75.00, N'6,12', '2026-11-12T03:24:30.0513040', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Policy pages (4)
IF NOT EXISTS (SELECT 1 FROM [LegalPages] WHERE [Slug] = N'privacy')
    INSERT INTO [LegalPages] ([Slug], [Title], [Summary], [Body], [IsPublished], [EffectiveOn], [Version], [IsReviewed], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'privacy', N'Privacy Policy', N'How {{CompanyName}} collects, uses and protects personal data in {{ProductName}}.', N'{{CompanyName}} ("we", "us") provides {{ProductName}}, an online HR, attendance and payroll service for businesses. This policy explains what personal data we handle, why, and the choices you have. It is written for the Digital Personal Data Protection Act, 2023 and the Information Technology Act, 2000 and its rules.

## Who decides what happens to your data

- **Employee data inside a company''s workspace.** The company that subscribes to {{ProductName}} (your employer) decides what employee data to record and why. Under the DPDP Act it is the **Data Fiduciary**; we process that data on its behalf as a **Data Processor**, only to provide the service. Questions or requests about this data should go first to your employer''s HR team.
- **Data about our customers and visitors.** For the details of company administrators who sign up, billing contacts, support conversations and visitors to our website, we are the Data Fiduciary.

## What we collect

- **Account details:** name, work email, mobile number, company name and company code, and a hashed password (we never store passwords in readable form).
- **Employee records entered by the employer:** job, department, reporting manager, joining and exit dates, documents, leave, attendance, salary and statutory details (such as PAN, UAN, bank account), tax declarations, assets, onboarding tasks and similar HR information. PAN and bank account numbers are stored encrypted.
- **Attendance location and photos, only if the employer turns them on:** the GPS position at the moment of clocking in or out (no tracking in between), and a selfie with a clock-in, which is deleted automatically after the period the employer chooses.
- **Usage and security logs:** sign-in history, IP address, browser type and an audit trail of changes, kept to protect accounts and investigate problems.
- **Billing details:** invoices, GSTIN and payment status. Card, UPI and bank payments are handled by our payment partner; we do not see or store card numbers.
- **Support conversations** you send us.

## Why we use it

- To provide, secure and support the service the company has subscribed to.
- To calculate attendance, leave and payroll as configured by the employer.
- To send service emails and, if enabled, sign-in codes by SMS.
- To bill subscriptions and meet tax and accounting obligations.
- To prevent fraud and misuse, and to comply with law and lawful requests.

We do not sell personal data and we do not use it for advertising.

## Who we share it with

We use a small number of service providers, only for the purposes above and under contract:

- Hosting and database infrastructure that runs {{ProductName}}.
- Email delivery, for notifications, payslip alerts and password resets.
- Razorpay, to collect subscription payments.
- MSG91, to send sign-in codes by SMS, only if a company enables mobile sign-in.
- Google and Microsoft, only if a company enables sign-in with those accounts; we receive your verified email address from them.

We may disclose data when required by law, court order or a government authority. **Codenexify support staff can see a company''s workspace only when that company grants time-limited, read-only access**, and every page they view is recorded in the company''s audit log.

## How long we keep it

We keep workspace data while the company''s subscription is active. After a subscription ends, the company can ask for an export; we then delete or anonymise the workspace, except records we must keep for longer under tax, company or other applicable law (for example, invoices). Security logs are kept only as long as needed to protect the service.

## How we protect it

Data is sent over encrypted HTTPS connections. Passwords are hashed, sensitive identifiers are encrypted, each company''s data is kept separate from every other company''s, staff accounts can use two-step sign-in, and access is limited by role and recorded in audit logs. No system is perfectly secure; if a personal data breach affects you, we will inform the affected company and the authorities as the law requires.

## Your rights

You can ask to access, correct, complete or erase your personal data, withdraw consent where processing is based on consent, and nominate someone to exercise your rights. For employee data, send the request to your employer, who controls it; we will help them respond. For other data, write to us at the address below. We will not discriminate against you for using these rights.

## Cookies

We use only cookies needed for the service to work: to keep you signed in, to protect forms against forgery and to show one-time messages. Your light or dark theme choice is saved in your browser. We do not use advertising or tracking cookies.

## Children

{{ProductName}} is a workplace service and is not meant for children. We do not knowingly collect data about anyone under 18 except as part of an employer''s lawful records.

## Grievance Officer

If you have a concern about how your data is handled, contact our Grievance Officer: **{{GrievanceOfficer}}**, [{{GrievanceEmail}}](mailto:{{GrievanceEmail}}), {{CompanyName}}, {{Address}}. We will acknowledge your complaint and try to resolve it within the time required by law. If you are not satisfied, you may approach the Data Protection Board of India.

## Changes to this policy

We will post any changes here with a new effective date, and tell customers by email or in the app before material changes take effect.', 1, '2026-11-13', 1, 0, 10, '2026-11-13T17:49:34.9600676', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [LegalPages] WHERE [Slug] = N'terms')
    INSERT INTO [LegalPages] ([Slug], [Title], [Summary], [Body], [IsPublished], [EffectiveOn], [Version], [IsReviewed], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'terms', N'Terms of Service', N'The agreement between {{CompanyName}} and companies that use {{ProductName}}.', N'These terms are an agreement between {{CompanyName}} ("we", "us") and the business that creates a {{ProductName}} workspace ("you", "the customer"). The person who registers confirms they are authorised to accept these terms for that business.

## 1. The service

{{ProductName}} is an online service for HR records, attendance, leave, payroll and related tasks. The modules available to you depend on your plan, as shown on our website and in your workspace. We may improve or change features; we will not remove a core feature of your paid plan during a paid period without notice.

## 2. Accounts and security

You are responsible for the people you give access to, the roles you assign them, and keeping sign-in details confidential. Tell us immediately at [{{SupportEmail}}](mailto:{{SupportEmail}}) if you suspect unauthorised access.

## 3. Free trial

New workspaces may start with a free trial for the period shown at sign-up. No payment is taken during the trial. At the end of the trial the workspace stops working until a plan is purchased.

## 4. Fees, invoices and taxes

- Subscription fees are as shown for your plan and billing cycle, or as agreed in writing. Per-employee plans are charged for the number of employees in the workspace.
- Fees are billed in advance and GST is added as applicable. We issue GST tax invoices from our GSTIN {{Gstin}}.
- Invoices are payable by the due date shown on them, online through our payment partner or by bank transfer.

## 5. Renewal, late payment and suspension

Subscriptions renew for the same cycle unless cancelled. We send the renewal invoice before the current period ends. If payment is late, access continues for a short grace period, after which the workspace may be suspended until payment is received. Your data is not deleted because of a suspension.

## 6. Your data

- You own the data you put into {{ProductName}}. You are responsible for having a lawful basis to collect and use your employees'' data, giving them any required notices, and the accuracy of what you enter.
- We process that data only to provide and support the service, as described in our [Privacy Policy](/privacy), and act on your instructions as your data processor.
- Our support staff can open your workspace only when you grant time-limited, read-only access from your settings; you can revoke it at any time.

## 7. Payroll, tax and statutory figures

{{ProductName}} calculates salaries, PF, ESI, professional tax, labour welfare fund and income tax from the rules and rates configured in the system and the data you enter. Rates and thresholds change by law; you must check the results and the statutory settings before paying salaries or filing returns. Statutory filings and payments remain your responsibility.

## 8. Acceptable use

You will not use the service to break the law, upload malicious code, try to access other customers'' data, overload or probe the system, or resell it without our written permission.

## 9. Availability and maintenance

We aim to keep the service available at all times but do not guarantee uninterrupted operation. We may carry out planned maintenance, announced in advance where possible.

## 10. Intellectual property

The software, design and content of {{ProductName}} belong to {{CompanyName}}. You receive a non-exclusive, non-transferable right to use it for your business during your subscription.

## 11. Confidentiality

Each party will keep the other''s non-public information confidential and use it only for this agreement.

## 12. Limitation of liability

To the extent the law allows, neither party is liable for indirect or consequential losses, or loss of profit or data arising from the other party''s actions. Our total liability under these terms is limited to the fees you paid us in the twelve months before the claim.

## 13. Ending the agreement

You can cancel at any time; see our [Refund & Cancellation Policy](/refund-policy). We may end or suspend the service for serious breach of these terms or non-payment. After the subscription ends you may request an export of your data before it is deleted as described in the Privacy Policy.

## 14. Changes to these terms

We may update these terms. We will post the new version here with its effective date and tell customers before material changes take effect. Continuing to use the service after that date means you accept the new terms.

## 15. Governing law

These terms are governed by the laws of India. The courts at **{{Jurisdiction}}** have exclusive jurisdiction.

## 16. Contact

{{CompanyName}}, {{Address}}. Email: [{{SupportEmail}}](mailto:{{SupportEmail}}).', 1, '2026-11-13', 1, 0, 20, '2026-11-13T17:49:34.9600676', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [LegalPages] WHERE [Slug] = N'refund-policy')
    INSERT INTO [LegalPages] ([Slug], [Title], [Summary], [Body], [IsPublished], [EffectiveOn], [Version], [IsReviewed], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'refund-policy', N'Refund & Cancellation Policy', N'How cancellations, refunds and service delivery work for {{ProductName}} subscriptions.', N'## Free trial

You can try {{ProductName}} free for the trial period shown at sign-up. No payment details are needed and nothing is charged.

## Cancelling a subscription

You can cancel at any time by writing to [{{BillingEmail}}](mailto:{{BillingEmail}}) from your registered email, or through your workspace where available. Cancellation takes effect at the end of the period you have already paid for; you keep full access until then and are not charged again.

## Refunds

Subscription fees are paid in advance for a monthly or annual period and are **not refundable for a period that has already started**, except in these cases:

- you were charged twice for the same invoice;
- you were charged after cancelling, because of an error on our side;
- the service was unavailable for a long period because of a fault on our side.

To request a refund, email [{{BillingEmail}}](mailto:{{BillingEmail}}) within 30 days of the payment with your company code and invoice number. Approved refunds are issued with a GST credit note and paid back to the original payment method, normally within 5–7 working days of approval; your bank may take longer to show it.

## Delivery of the service

{{ProductName}} is an online service. Your workspace is available immediately after registration, and paid plans are activated as soon as payment is confirmed. Nothing is shipped physically.

## Contact

{{CompanyName}}, {{Address}}. Phone: {{Phone}}. Email: [{{BillingEmail}}](mailto:{{BillingEmail}}).', 1, '2026-11-13', 1, 0, 30, '2026-11-13T17:49:34.9600676', N'system', NULL, NULL);
IF NOT EXISTS (SELECT 1 FROM [LegalPages] WHERE [Slug] = N'contact')
    INSERT INTO [LegalPages] ([Slug], [Title], [Summary], [Body], [IsPublished], [EffectiveOn], [Version], [IsReviewed], [SortOrder], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy]) VALUES (N'contact', N'Contact Us', N'How to reach {{CompanyName}}.', N'## {{CompanyName}}

{{Address}}

- **Support:** [{{SupportEmail}}](mailto:{{SupportEmail}})
- **Billing:** [{{BillingEmail}}](mailto:{{BillingEmail}})
- **Phone:** {{Phone}}
- **GSTIN:** {{Gstin}}
- **Website:** [{{Website}}]({{Website}})

## Already a customer?

Sign in to your workspace and open **Support** to raise a ticket; you can follow the conversation there and we reply by email too.

## Privacy questions

Write to our Grievance Officer, {{GrievanceOfficer}}, at [{{GrievanceEmail}}](mailto:{{GrievanceEmail}}). See our [Privacy Policy](/privacy).', 1, '2026-11-13', 1, 0, 40, '2026-11-13T17:49:34.9600676', N'system', NULL, NULL);

-- ---------------------------------------------------------------- Control Center settings (secrets removed) (1)
IF NOT EXISTS (SELECT 1 FROM [PlatformSettings])
    INSERT INTO [PlatformSettings] ([LegalName], [Gstin], [Pan], [AddressLine], [City], [State], [PostalCode], [BillingEmail], [Phone], [InvoicePrefix], [CreditNotePrefix], [SacCode], [GstRate], [InvoiceDueDays], [RenewalInvoiceDaysBefore], [ReminderSchedule], [BankDetails], [RazorpayEnabled], [RazorpayKeyId], [RazorpayKeySecretProtected], [RazorpayWebhookSecretProtected], [SmtpEnabled], [SmtpHost], [SmtpPort], [SmtpUseSsl], [SmtpUsername], [SmtpPasswordProtected], [SmtpFromEmail], [SmtpFromName], [CreatedAtUtc], [CreatedBy], [UpdatedAtUtc], [UpdatedBy], [MaintenanceEnabled], [MaintenanceMessage], [MaintenanceUntilUtc], [SupportEmail], [GoogleClientId], [GoogleClientSecretProtected], [MicrosoftClientId], [MicrosoftClientSecretProtected], [Msg91AuthKeyProtected], [Msg91OtpVariable], [Msg91TemplateId], [SmsEnabled], [GrievanceOfficerEmail], [GrievanceOfficerName], [LegalJurisdictionCity]) VALUES (N'Codenexify Technologies Private Limited', N'27AAACC1234D1Z5', NULL, NULL, NULL, N'Maharashtra', NULL, NULL, NULL, N'CNX', N'CNXCN', N'998314', 18.00, 7, 7, N'3,1,0,-3,-7', N'Sample Bank · A/c 000111222333 · IFSC SBIN0000001', 0, NULL, NULL, NULL, 0, NULL, 587, 1, NULL, NULL, NULL, N'Codenexify HRMS', '2026-11-12T04:00:59.9338326', N'system', '2026-11-12T04:33:56.5856884', N'owner@codenexify.local', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'', NULL, 0, NULL, NULL, NULL);

-- ---------------------------------------------------------------- Announcements (0)

COMMIT TRANSACTION;
PRINT 'Platform setup applied.';
