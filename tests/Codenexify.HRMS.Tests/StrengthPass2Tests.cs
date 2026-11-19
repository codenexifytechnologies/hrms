using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Strength;
using Codenexify.HRMS.Application.Tenancy;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Codenexify.HRMS.Domain.Time;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class StrengthPass2Tests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other, _admin, _otherAdmin;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "STRENGTH");
        _other = await _host.ProvisionAsync("globex", "STRENGTH");
        _admin = await OwnerAsync(_org);
        _otherAdmin = await OwnerAsync(_other);
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private Task<int> OwnerAsync(int org)
    {
        _host.Context.AsPlatform();
        return _host.RunAsync(sp => S<AppDbContext>(sp).Users.IgnoreQueryFilters().Where(u => u.OrganizationId == org && u.IsOrganizationOwner).Select(u => u.Id).SingleAsync());
    }

    private async Task<(int EmployeeId, int UserId)> EmployeeAsync(int org, string name, int? managerId = null, DateOnly? joined = null, string? phone = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "T", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = joined ?? new DateOnly(2024, 1, 1), ReportingManagerId = managerId },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var user = await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var e = await db.Employees.SingleAsync(x => x.Id == r.Value);
            if (phone is not null) (await db.Users.SingleAsync(u => u.Id == e.UserId)).Phone = phone;
            await db.SaveChangesAsync();
            return e.UserId!.Value;
        });
        return (r.Value, user);
    }

    private Task<T> As<TService, T>(int org, int user, Func<TService, Task<T>> a) where TService : notnull
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => a(S<TService>(sp)));
    }

    private Task<int> UnreadAsync(int org, int user) => As<INotificationService, int>(org, user, n => n.UnreadCountAsync());

    // ------------------------------------------------------------------ custom roles

    [Fact]
    public async Task Role_managers_cannot_grant_more_than_they_hold()
    {
        var roles = await As<ITenantAdminService, IReadOnlyList<RoleRow>>(_org, _admin, a => a.ListRolesAsync());
        var hrAdminRole = roles.Single(r => r.Name == "HR Admin").Id;
        var orgAdminRole = roles.Single(r => r.Name == TenantPermissions.OrgAdminRole).Id;
        var hr = await As<ITenantAdminService, Result<int>>(_org, _admin, a => a.CreateUserAsync(new TenantUserInput { FullName = "Hema HR", Email = "hema@acme.test", Password = "Welcome@123", RoleIds = [hrAdminRole] }));
        Assert.True(hr.Succeeded, hr.Error);

        var perms = await As<ITenantAdminService, IReadOnlyList<Domain.Identity.Permission>>(_org, _admin, a => a.GetPermissionCatalogAsync());
        int P(string code) => perms.Single(p => p.Code == code).Id;

        // HR Admin can build a role from permissions they hold…
        var ok = await As<ITenantAdminService, Result<int>>(_org, hr.Value, a => a.CreateRoleAsync(new RoleInput { Name = "Recruiter", PermissionIds = [P(TenantPermissions.EmployeesView)] }));
        Assert.True(ok.Succeeded, ok.Error);
        // …but not one with payroll powers, nor add them later, nor hand out the Organization Admin role.
        var payroll = await As<ITenantAdminService, Result<int>>(_org, hr.Value, a => a.CreateRoleAsync(new RoleInput { Name = "Sneaky", PermissionIds = [P(TenantPermissions.PayrollManage)] }));
        Assert.False(payroll.Succeeded);
        Assert.Contains("only grant permissions you hold", payroll.Error);
        Assert.False((await As<ITenantAdminService, Result>(_org, hr.Value, a => a.UpdateRoleAsync(ok.Value, new RoleInput { Name = "Recruiter", PermissionIds = [P(TenantPermissions.EmployeesView), P(TenantPermissions.PayrollManage)] }))).Succeeded);
        Assert.False((await As<ITenantAdminService, Result<int>>(_org, hr.Value, a => a.CreateUserAsync(new TenantUserInput { FullName = "Mallory", Email = "m@acme.test", Password = "Welcome@123", RoleIds = [orgAdminRole] }))).Succeeded);
        Assert.False((await As<ITenantAdminService, Result>(_org, hr.Value, a => a.UpdateUserAsync(hr.Value, new TenantUserInput { FullName = "Hema HR", Email = "hema@acme.test", RoleIds = [hrAdminRole, orgAdminRole] }))).Succeeded);
        // HR Admin can't edit the owner's account either.
        Assert.False((await As<ITenantAdminService, Result>(_org, hr.Value, a => a.UpdateUserAsync(_admin, new TenantUserInput { FullName = "Owner", Email = "admin@acme.test", RoleIds = [orgAdminRole] }))).Succeeded);
        // The Organization Admin is unrestricted.
        Assert.True((await As<ITenantAdminService, Result<int>>(_org, _admin, a => a.CreateRoleAsync(new RoleInput { Name = "Payroll lead", PermissionIds = [P(TenantPermissions.PayrollManage)] }))).Succeeded);
    }

    // ------------------------------------------------------------------ onboarding

    [Fact]
    public async Task Onboarding_assigns_tasks_to_hr_manager_buddy_and_joiner()
    {
        var (mgr, mgrUser) = await EmployeeAsync(_org, "Manu");
        var (buddy, buddyUser) = await EmployeeAsync(_org, "Bina");
        var joined = _host.Clock.Today.AddDays(5);
        var (joiner, joinerUser) = await EmployeeAsync(_org, "Jay", mgr, joined);
        Task<T> Hr<T>(Func<IOnboardingService, Task<T>> a) => As(_org, _admin, a);

        var template = (await Hr(o => o.TemplatesAsync())).Single(); // seeded "Standard onboarding"
        Assert.Contains(joiner, (await Hr(o => o.CandidatesAsync())).Select(c => c.EmployeeId));
        Assert.False((await Hr(o => o.StartAsync(joiner, template.Id, null))).Succeeded); // the template has buddy tasks
        Assert.False((await Hr(o => o.StartAsync(joiner, template.Id, joiner))).Succeeded);
        var plan = await Hr(o => o.StartAsync(joiner, template.Id, buddy));
        Assert.True(plan.Succeeded, plan.Error);
        Assert.False((await Hr(o => o.StartAsync(joiner, template.Id, buddy))).Succeeded); // one live plan
        Assert.Equal(1, await UnreadAsync(_org, mgrUser));
        Assert.Equal(1, await UnreadAsync(_org, buddyUser));

        var view = await Hr(o => o.PlanAsync(plan.Value, asHr: true));
        Assert.Equal(9, view!.Tasks.Count);
        Assert.Equal(joined.AddDays(-7), view.Tasks.First().DueOn);
        Assert.Equal(("Manu T", "Bina T"), (view.Manager, view.Buddy));

        // Each person sees and completes only their own tasks.
        var managerTasks = await As<IOnboardingService, IReadOnlyList<MyOnboardingTask>>(_org, mgrUser, o => o.MyTasksAsync());
        Assert.All(managerTasks, t => Assert.Equal(OnboardingAssignee.Manager, t.Assignee));
        Assert.Equal(3, managerTasks.Count);
        var hrTask = view.Tasks.First(t => t.Assignee == OnboardingAssignee.Hr).Id;
        Assert.False((await As<IOnboardingService, Result>(_org, joinerUser, o => o.CompleteTaskAsync(hrTask, null, asHr: false))).Succeeded);
        Assert.Null(await As<IOnboardingService, OnboardingPlanView?>(_org, (await EmployeeAsync(_org, "Stranger")).UserId, o => o.PlanAsync(plan.Value, false)));
        foreach (var t in managerTasks) Assert.True((await As<IOnboardingService, Result>(_org, mgrUser, o => o.CompleteTaskAsync(t.TaskId, "done", false))).Succeeded);
        foreach (var t in await As<IOnboardingService, IReadOnlyList<MyOnboardingTask>>(_org, buddyUser, o => o.MyTasksAsync()))
            Assert.True((await As<IOnboardingService, Result>(_org, buddyUser, o => o.CompleteTaskAsync(t.TaskId, null, false))).Succeeded);
        foreach (var t in await As<IOnboardingService, IReadOnlyList<MyOnboardingTask>>(_org, joinerUser, o => o.MyTasksAsync()))
        {
            Assert.True(t.ForMe);
            Assert.True((await As<IOnboardingService, Result>(_org, joinerUser, o => o.CompleteTaskAsync(t.TaskId, null, false))).Succeeded);
        }
        foreach (var t in (await Hr(o => o.PlanAsync(plan.Value, true)))!.Tasks.Where(t => t.CompletedAtUtc is null))
            Assert.True((await Hr(o => o.CompleteTaskAsync(t.Id, null, true))).Succeeded);
        Assert.Equal(OnboardingStatus.Completed, (await Hr(o => o.PlanAsync(plan.Value, true)))!.Status);

        // Tenant isolation.
        Assert.Null(await As<IOnboardingService, OnboardingPlanView?>(_other, _otherAdmin, o => o.PlanAsync(plan.Value, true)));
        Assert.False((await As<IOnboardingService, Result>(_other, _otherAdmin, o => o.CompleteTaskAsync(hrTask, null, true))).Succeeded);
    }

    // ------------------------------------------------------------------ travel desk

    [Fact]
    public async Task Travel_goes_from_manager_approval_to_desk_booking()
    {
        var (mgr, mgrUser) = await EmployeeAsync(_org, "Manu");
        var (emp, empUser) = await EmployeeAsync(_org, "Tara", mgr);
        var depart = _host.Clock.Today.AddDays(10);
        var input = new TravelInput { Purpose = "Client workshop", FromCity = "Pune", ToCity = "Mumbai", DepartOn = depart, ReturnOn = depart.AddDays(2), Mode = TravelMode.Train, EstimatedCost = 9000, AdvanceRequested = 3000 };
        Task<T> Emp<T>(Func<ITravelService, Task<T>> a) => As(_org, empUser, a);

        Assert.False((await Emp(t => t.SubmitAsync(emp, new TravelInput { Purpose = "x", FromCity = "Pune", ToCity = "Delhi", DepartOn = _host.Clock.Today.AddDays(-1) }))).Succeeded);
        Assert.False((await Emp(t => t.SubmitAsync(emp, new TravelInput { Purpose = "x", FromCity = "Pune", ToCity = "Delhi", DepartOn = depart, EstimatedCost = 100, AdvanceRequested = 500 }))).Succeeded);
        var id = (await Emp(t => t.SubmitAsync(emp, input))).Value;
        Assert.Equal(1, await UnreadAsync(_org, mgrUser));

        // The traveller can't approve, the desk can't jump the manager, the manager can.
        Assert.False((await Emp(t => t.DecideAsync(id, true, null, false))).Succeeded);
        Assert.False((await As<ITravelService, Result>(_org, _admin, t => t.DecideAsync(id, true, null, isDesk: true))).Succeeded);
        Assert.Single(await As<ITravelService, IReadOnlyList<TravelRow>>(_org, mgrUser, t => t.ToApproveAsync()));
        Assert.True((await As<ITravelService, Result>(_org, mgrUser, t => t.DecideAsync(id, true, "Go", false))).Succeeded);

        Task<Result> Book(UploadedFile? f) => As<ITravelService, Result>(_org, _admin, t => t.BookAsync(id, "PNR 1234567890, Deccan Queen", 1800, f));
        Assert.False((await Book(new UploadedFile(new MemoryStream("not a pdf"u8.ToArray()), "ticket.pdf", 9))).Succeeded);
        var pdf = "%PDF-1.4\n%test"u8.ToArray();
        Assert.True((await Book(new UploadedFile(new MemoryStream(pdf), "ticket.pdf", pdf.Length))).Succeeded);
        var ticket = await Emp(t => t.TicketAsync(id, false, emp));
        Assert.Equal("application/pdf", ticket!.ContentType);
        Assert.Null(await As<ITravelService, TravelTicket?>(_org, mgrUser, t => t.TicketAsync(id, false, mgr)));

        Assert.True((await As<ITravelService, Result>(_org, _admin, t => t.RecordAdvanceAsync(id, 3000, _host.Clock.Today))).Succeeded);
        Assert.False((await As<ITravelService, Result>(_org, _admin, t => t.CompleteAsync(id))).Succeeded); // not back yet
        _host.Clock.AdvanceDays(13);
        Assert.True((await As<ITravelService, Result>(_org, _admin, t => t.CompleteAsync(id))).Succeeded);
        Assert.Equal(TravelStatus.Completed, (await Emp(t => t.MineAsync(emp))).Single().Status);
        Assert.Null(await As<ITravelService, TravelView?>(_other, _otherAdmin, t => t.GetAsync(id, true, null)));
    }

    [Fact]
    public async Task Travel_without_a_manager_is_approved_by_the_desk()
    {
        var (emp, empUser) = await EmployeeAsync(_org, "Solo");
        var id = (await As<ITravelService, Result<int>>(_org, empUser, t => t.SubmitAsync(emp, new TravelInput { Purpose = "Expo", FromCity = "Pune", ToCity = "Goa", DepartOn = _host.Clock.Today.AddDays(3) }))).Value;
        Assert.False((await As<ITravelService, Result>(_org, _admin, t => t.DecideAsync(id, false, null, true))).Succeeded); // reason needed
        Assert.True((await As<ITravelService, Result>(_org, _admin, t => t.DecideAsync(id, false, "Budget freeze", true))).Succeeded);
        Assert.False((await As<ITravelService, Result>(_org, empUser, t => t.CancelAsync(id, emp))).Succeeded);
    }

    // ------------------------------------------------------------------ selfie clock-in

    [Fact]
    public async Task Selfie_clock_in_requires_a_photo_and_photos_expire()
    {
        var (emp, user) = await EmployeeAsync(_org, "Sana");
        Assert.True((await As<ISelfieService, Result>(_org, _admin, s => s.SaveSettingsAsync(true, 30))).Succeeded);
        Assert.False((await As<ISelfieService, Result>(_org, _admin, s => s.SaveSettingsAsync(true, 3))).Succeeded);
        Task<Result<PunchResult>> Punch(byte[]? photo) => As<IAttendanceService, Result<PunchResult>>(_org, user, a => a.PunchAsync(emp, PunchSource.Web, null, null, photo));

        Assert.Contains("photo", (await Punch(null)).Error);
        Assert.False((await Punch("not a jpeg"u8.ToArray())).Succeeded);
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9];
        Assert.True((await Punch(jpeg)).Succeeded);

        var rows = await As<ISelfieService, IReadOnlyList<SelfiePunchRow>>(_org, _admin, s => s.ForDayAsync(_host.Clock.Today));
        var file = await As<ISelfieService, SelfieFile?>(_org, _admin, s => s.OpenAsync(rows.Single().PunchId));
        using (var ms = new MemoryStream()) { await file!.Content.CopyToAsync(ms); file.Content.Dispose(); Assert.Equal(jpeg, ms.ToArray()); }
        Assert.Null(await As<ISelfieService, SelfieFile?>(_other, _otherAdmin, s => s.OpenAsync(rows.Single().PunchId)));

        _host.Clock.AdvanceDays(31);
        Assert.Equal(1, await As<ISelfieService, int>(_org, _admin, s => s.PurgeExpiredAsync()));
        Assert.Null(await As<ISelfieService, SelfieFile?>(_org, _admin, s => s.OpenAsync(rows.Single().PunchId)));
    }

    // ------------------------------------------------------------------ e-signature

    [Fact]
    public async Task Employee_signs_a_letter_with_an_emailed_code()
    {
        var (emp, user) = await EmployeeAsync(_org, "Esha");
        var template = await As<ILetterService, Result<int>>(_org, _admin, l => l.SaveTemplateAsync(null, new LetterTemplateInput
            { Name = "Policy acknowledgement", Kind = LetterKind.Other, Subject = "Code of conduct", Body = "Dear {{Employee.FullName}}, please confirm you have read the code of conduct." }));
        var letter = await As<ILetterService, Result<int>>(_org, _admin, l => l.IssueAsync(emp, new LetterIssueInput { TemplateId = template.Value, SignatoryName = "Priya HR" }));
        Assert.True(letter.Succeeded, letter.Error);

        var req = await As<ISignatureService, Result<int>>(_org, _admin, s => s.RequestAsync(letter.Value, "Please sign"));
        Assert.True(req.Succeeded, req.Error);
        Assert.False((await As<ISignatureService, Result<int>>(_org, _admin, s => s.RequestAsync(letter.Value, null))).Succeeded); // already pending
        Task<T> Me<T>(Func<ISignatureService, Task<T>> a) => As(_org, user, a);

        Assert.False((await Me(s => s.SignAsync(req.Value, emp, "000000", "Esha T", true, "1.2.3.4", "ua"))).Succeeded); // no code sent yet
        Assert.True((await Me(s => s.SendCodeAsync(req.Value, emp))).Succeeded);
        Assert.False((await Me(s => s.SendCodeAsync(req.Value, emp))).Succeeded); // wait a minute
        var code = _host.Email.Sent.Last().Subject[^6..];
        Assert.False((await Me(s => s.SignAsync(req.Value, emp, code, "Someone Else", true, "1.2.3.4", "ua"))).Succeeded);
        Assert.False((await Me(s => s.SignAsync(req.Value, emp, code, "Esha T", false, "1.2.3.4", "ua"))).Succeeded);
        Assert.False((await Me(s => s.SignAsync(req.Value, emp, code == "123456" ? "654321" : "123456", "Esha T", true, "1.2.3.4", "ua"))).Succeeded);
        Assert.Null(await As<ISignatureService, SignatureView?>(_org, _admin, s => s.ViewAsync(req.Value, emp + 1000)));
        var signed = await Me(s => s.SignAsync(req.Value, emp, code, "  esha   t ", true, "1.2.3.4", "ua"));
        Assert.True(signed.Succeeded, signed.Error);

        var row = (await As<ISignatureService, IReadOnlyList<SignatureRow>>(_org, _admin, s => s.ListAsync(SignatureStatus.Signed))).Single();
        Assert.NotNull(row.SignedDocumentId);
        var doc = await _host.RunAsync(sp => S<AppDbContext>(sp).EmployeeDocuments.AsNoTracking().SingleAsync(d => d.Id == row.SignedDocumentId));
        Assert.True(doc.VisibleToEmployee && doc.Title.StartsWith("Signed:"));
        // The one-time code never reaches the audit trail.
        _host.Context.AsPlatform();
        Assert.False(await _host.RunAsync(sp => S<AppDbContext>(sp).AuditLogs.AnyAsync(a => (a.NewValues ?? "").Contains("OtpHash") || (a.OldValues ?? "").Contains("OtpHash"))));
        Assert.Empty(await As<ISignatureService, IReadOnlyList<SignatureRow>>(_other, _otherAdmin, s => s.ListAsync(null)));
    }

    // ------------------------------------------------------------------ reminders & scheduled reports

    [Fact]
    public async Task Reminder_rules_fire_once_per_subject()
    {
        var (mgr, mgrUser) = await EmployeeAsync(_org, "Manu");
        var (emp, empUser) = await EmployeeAsync(_org, "Pia", mgr);
        await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var e = await db.Employees.SingleAsync(x => x.Id == emp);
            e.Status = EmployeeStatus.Probation;
            e.ProbationEndDate = _host.Clock.Today.AddDays(14);
            await db.SaveChangesAsync();
        });
        Assert.False((await As<IReminderService, Result<int>>(_org, _admin, r => r.SaveAsync(null, new ReminderRuleInput { Name = "Nobody", NotifyEmployee = false, NotifyManager = false, NotifyHr = false }))).Succeeded);
        Assert.True((await As<IReminderService, Result<int>>(_org, _admin, r => r.SaveAsync(null, new ReminderRuleInput { Name = "Probation review due", Event = ReminderEvent.ProbationEnding, Days = 14, NotifyManager = true, NotifyHr = true, NotifyEmployee = true }))).Succeeded);

        var before = await UnreadAsync(_org, mgrUser);
        Assert.Equal(1, await As<IReminderService, int>(_org, _admin, r => r.RunAsync()));
        Assert.Equal(0, await As<IReminderService, int>(_org, _admin, r => r.RunAsync())); // already sent
        Assert.Equal(before + 1, await UnreadAsync(_org, mgrUser));
        Assert.Equal(1, await UnreadAsync(_org, empUser));
        Assert.Equal(0, await As<IReminderService, int>(_other, _otherAdmin, r => r.RunAsync()));
    }

    [Fact]
    public async Task Scheduled_reports_compute_periods_and_email_allowed_recipients()
    {
        Assert.Equal(new DateOnly(2026, 6, 22), ReportScheduleService.NextRun(ReportFrequency.Weekly, new DateOnly(2026, 6, 15))); // Monday → next Monday
        Assert.Equal(new DateOnly(2026, 6, 22), ReportScheduleService.NextRun(ReportFrequency.Weekly, new DateOnly(2026, 6, 17)));
        Assert.Equal(new DateOnly(2026, 7, 1), ReportScheduleService.NextRun(ReportFrequency.Monthly, new DateOnly(2026, 6, 15)));
        Assert.Equal((new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)), ReportScheduleService.Period(ReportFrequency.Monthly, new DateOnly(2026, 7, 1)));

        await EmployeeAsync(_org, "Nia", joined: _host.Clock.Today.AddDays(-3));
        var (_, plainUser) = await EmployeeAsync(_org, "Ola");
        Assert.False((await As<IReportScheduleService, Result<int>>(_org, _admin, s => s.SaveAsync(null, new ReportScheduleInput { Name = "Weekly", Frequency = ReportFrequency.Weekly, RecipientUserIds = [plainUser] }))).Succeeded);
        var id = await As<IReportScheduleService, Result<int>>(_org, _admin, s => s.SaveAsync(null, new ReportScheduleInput { Name = "Weekly joiners", Frequency = ReportFrequency.Weekly, RecipientUserIds = [_admin] }));
        Assert.True(id.Succeeded, id.Error);
        var sent = await As<IReportScheduleService, Result<int>>(_org, _admin, s => s.SendNowAsync(id.Value, "https://hr.example.com"));
        Assert.True(sent.Succeeded, sent.Error);
        var mail = _host.Email.Sent.Last(m => m.Category == "report");
        Assert.Equal("admin@acme.test", mail.To);
        Assert.Contains("https://hr.example.com/org/reports", mail.HtmlBody);
        Assert.Empty(await As<IReportScheduleService, IReadOnlyList<ReportScheduleRow>>(_other, _otherAdmin, s => s.ListAsync()));
    }

    // ------------------------------------------------------------------ SSO & mobile OTP

    private async Task EnablePlatformProvidersAsync()
    {
        _host.Context.AsPlatform();
        await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var s = await db.PlatformSettings.SingleAsync();
            var protector = S<Application.Abstractions.ISecretProtector>(sp);
            s.GoogleClientId = "google-client"; s.GoogleClientSecretProtected = protector.Protect("g-secret");
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Single_sign_on_only_for_existing_users_on_allowed_domains()
    {
        await EnablePlatformProvidersAsync();
        await EmployeeAsync(_org, "Gita"); // gita@{org}.test
        var domain = $"{_org}.test";
        Task<AuthResult> Sso(string email, bool verified = true) =>
            _host.RunAsync(sp => { _host.Context.AsAnonymous(); return S<IAlternateSignInService>(sp).SignInExternalAsync("acme", SsoProviders.Google, email, verified, "1.1.1.1", "ua"); });

        Assert.False((await Sso($"gita@{domain}")).Succeeded); // not switched on yet
        Assert.False((await As<ISignInSettingsService, Result>(_org, _admin, s => s.SaveAsync(new SignInSettingsInput { SsoGoogle = true }))).Succeeded); // domain required
        Assert.False((await As<ISignInSettingsService, Result>(_org, _admin, s => s.SaveAsync(new SignInSettingsInput { SsoGoogle = true, SsoDomains = "gmail.com" }))).Succeeded);
        Assert.False((await As<ISignInSettingsService, Result>(_org, _admin, s => s.SaveAsync(new SignInSettingsInput { SsoMicrosoft = true, SsoDomains = domain }))).Succeeded); // not on the platform
        Assert.True((await As<ISignInSettingsService, Result>(_org, _admin, s => s.SaveAsync(new SignInSettingsInput { SsoGoogle = true, SsoDomains = "@" + domain.ToUpperInvariant() }))).Succeeded);

        var ok = await Sso($"Gita@{domain}");
        Assert.True(ok.Succeeded, ok.Message);
        Assert.Equal(_org, ok.Principal!.OrganizationId);
        Assert.False((await Sso($"gita@{domain}", verified: false)).Succeeded);
        Assert.False((await Sso($"nobody@{domain}")).Succeeded); // never auto-created
        Assert.False((await Sso("gita@evil.test")).Succeeded);
        var other = await _host.RunAsync(sp => { _host.Context.AsAnonymous(); return S<IAlternateSignInService>(sp).SignInExternalAsync("globex", SsoProviders.Google, $"gita@{domain}", true, null, null); });
        Assert.False(other.Succeeded);
    }

    [Fact]
    public async Task Mobile_otp_signs_in_once_and_never_reveals_numbers()
    {
        var (_, user) = await EmployeeAsync(_org, "Omi", phone: "+91 98765 43210");
        Assert.True((await As<ISignInSettingsService, Result>(_org, _admin, s => s.SaveAsync(new SignInSettingsInput { MobileOtpLogin = true }))).Succeeded);
        Task Request(string mobile, string org = "acme") => _host.RunAsync(sp => { _host.Context.AsAnonymous(); return S<IAlternateSignInService>(sp).RequestOtpAsync(org, mobile, "1.1.1.1"); });
        Task<AuthResult> Verify(string mobile, string code) => _host.RunAsync(sp => { _host.Context.AsAnonymous(); return S<IAlternateSignInService>(sp).SignInWithOtpAsync("acme", mobile, code, "1.1.1.1", "ua"); });

        await Request("9000000000"); // unknown number: nothing sent, no error
        await Request("9876543210", "globex"); // other company: nothing sent
        Assert.Empty(_host.Sms.Sent);
        await Request("09876543210");
        var (mobile, code) = _host.Sms.Sent.Single();
        Assert.Equal("9876543210", mobile);

        Assert.False((await Verify("9876543210", code == "111111" ? "222222" : "111111")).Succeeded);
        var ok = await Verify("+919876543210", code);
        Assert.True(ok.Succeeded, ok.Message);
        Assert.Equal(user, ok.Principal!.UserId);
        Assert.False((await Verify("9876543210", code)).Succeeded); // single use

        // Only three codes per quarter hour.
        for (var i = 0; i < 5; i++) await Request("9876543210");
        Assert.Equal(3, _host.Sms.Sent.Count);
        _host.Context.AsPlatform();
        Assert.False(await _host.RunAsync(sp => S<AppDbContext>(sp).AuditLogs.AnyAsync(a => (a.NewValues ?? "").Contains("CodeHash"))));
    }
}
