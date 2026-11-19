using System.Text;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class CoreHrTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _orgA, _orgB;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _orgA = await _host.ProvisionAsync("alpha", "STRENGTH");
        _orgB = await _host.ProvisionAsync("bravo", "STRENGTH");
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private T Svc<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private static EmployeeCreateInput NewEmployee(string first, string email, DateOnly joined, bool login = false, int? managerId = null, int? deptId = null) => new()
    {
        Employment = new EmploymentInput { FirstName = first, LastName = "Kumar", WorkEmail = email, DateOfJoining = joined, ReportingManagerId = managerId, DepartmentId = deptId },
        CreateLogin = login,
        InitialPassword = login ? "Welcome@123" : null
    };

    private async Task<int> AddEmployeeAsync(int org, string first, bool login = false, int? managerId = null, int? deptId = null)
    {
        _host.Context.AsTenant(org);
        var r = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).CreateAsync(NewEmployee(first, $"{first.ToLowerInvariant()}@{org}.test", _host.Clock.Today, login, managerId, deptId)));
        Assert.True(r.Succeeded, r.Error);
        return r.Value;
    }

    [Fact]
    public async Task Employee_ids_are_generated_sequentially_per_organization()
    {
        var a1 = await AddEmployeeAsync(_orgA, "Asha");
        var a2 = await AddEmployeeAsync(_orgA, "Bala");
        var b1 = await AddEmployeeAsync(_orgB, "Chen");

        _host.Context.AsPlatform();
        var codes = await _host.RunAsync(sp => Svc<AppDbContext>(sp).Employees.OrderBy(e => e.Id).Select(e => new { e.Id, e.EmployeeCode }).ToListAsync());
        Assert.Equal("EMP0001", codes.Single(c => c.Id == a1).EmployeeCode);
        Assert.Equal("EMP0002", codes.Single(c => c.Id == a2).EmployeeCode);
        Assert.Equal("EMP0001", codes.Single(c => c.Id == b1).EmployeeCode); // independent sequence for org B

        _host.Context.AsTenant(_orgA);
        var save = await _host.RunAsync(sp => Svc<IStructureService>(sp).SaveSettingsAsync(new HrSettingsInput { EmployeeIdPrefix = "acm-", EmployeeIdPadding = 3, NextEmployeeNumber = 50 }));
        Assert.True(save.Succeeded, save.Error);
        var a3 = await AddEmployeeAsync(_orgA, "Devi");
        _host.Context.AsTenant(_orgA);
        Assert.Equal("ACM-050", await _host.RunAsync(sp => Svc<AppDbContext>(sp).Employees.Where(e => e.Id == a3).Select(e => e.EmployeeCode).SingleAsync()));

        // A sequence that would reuse an existing ID is refused.
        var clash = await _host.RunAsync(sp => Svc<IStructureService>(sp).SaveSettingsAsync(new HrSettingsInput { EmployeeIdPrefix = "EMP", EmployeeIdPadding = 4, NextEmployeeNumber = 1 }));
        Assert.False(clash.Succeeded);
    }

    [Fact]
    public async Task Employee_with_login_gets_employee_role_and_must_change_password()
    {
        var id = await AddEmployeeAsync(_orgA, "Esha", login: true);
        _host.Context.AsAnonymous();
        var auth = await _host.RunAsync(sp => Svc<ITenantAuthService>(sp).SignInAsync("alpha", $"esha@{_orgA}.test", "Welcome@123", null, null));
        Assert.True(auth.Succeeded, auth.Message);
        Assert.True(auth.Principal!.MustChangePassword);
        Assert.Equal("Employee", auth.Principal.Role);
        Assert.DoesNotContain(TenantPermissions.EmployeesView, auth.Principal.Permissions);

        _host.Context.AsTenant(_orgA, auth.Principal.UserId);
        Assert.Equal(id, await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetMyEmployeeIdAsync()));
    }

    [Fact]
    public async Task Plan_employee_and_branch_limits_are_enforced()
    {
        _host.Context.AsPlatform();
        Assert.True((await _host.RunAsync(sp => Svc<IOrganizationService>(sp).UpdateLimitsAsync(_orgA, new OrganizationLimitsInput { MaxEmployees = 1, MaxBranches = 1 }))).Succeeded);

        await AddEmployeeAsync(_orgA, "First");
        _host.Context.AsTenant(_orgA);
        var second = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).CreateAsync(NewEmployee("Second", "second@a.test", _host.Clock.Today)));
        Assert.False(second.Succeeded);
        Assert.Contains("allows 1 employees", second.Error);

        var b1 = await _host.RunAsync(sp => Svc<IStructureService>(sp).SaveBranchAsync(null, new BranchInput { Name = "Pune HQ", Code = "PUN" }));
        Assert.True(b1.Succeeded, b1.Error);
        var b2 = await _host.RunAsync(sp => Svc<IStructureService>(sp).SaveBranchAsync(null, new BranchInput { Name = "Mumbai", Code = "MUM" }));
        Assert.False(b2.Succeeded);
    }

    [Fact]
    public async Task Another_organizations_department_or_manager_cannot_be_referenced()
    {
        _host.Context.AsTenant(_orgB);
        Assert.True((await _host.RunAsync(sp => Svc<IStructureService>(sp).SaveDepartmentAsync(null, new DepartmentInput { Name = "B Sales" }))).Succeeded);
        var bDept = await _host.RunAsync(sp => Svc<AppDbContext>(sp).Departments.Select(d => d.Id).SingleAsync());
        var bManager = await AddEmployeeAsync(_orgB, "Boss");

        _host.Context.AsTenant(_orgA);
        var withDept = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).CreateAsync(NewEmployee("Mal", "mal@a.test", _host.Clock.Today, deptId: bDept)));
        Assert.False(withDept.Succeeded);
        Assert.Equal("Department not found.", withDept.Error);

        var withManager = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).CreateAsync(NewEmployee("Mal", "mal@a.test", _host.Clock.Today, managerId: bManager)));
        Assert.False(withManager.Succeeded);

        Assert.Null(await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(bManager, includeSensitive: true)));
    }

    [Fact]
    public async Task Reporting_line_loops_are_rejected_and_org_chart_is_built()
    {
        var ceo = await AddEmployeeAsync(_orgA, "Ceo");
        var cto = await AddEmployeeAsync(_orgA, "Cto", managerId: ceo);
        var dev = await AddEmployeeAsync(_orgA, "Dev", managerId: cto);

        _host.Context.AsTenant(_orgA);
        var profile = (await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(ceo, false)))!;
        var input = new EmploymentInput
        {
            FirstName = "Ceo", LastName = "Kumar", WorkEmail = profile.Employee.WorkEmail, DateOfJoining = profile.Employee.DateOfJoining, ReportingManagerId = dev
        };
        var loop = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).UpdateEmploymentAsync(ceo, input));
        Assert.False(loop.Succeeded);
        Assert.Contains("loop", loop.Error);

        var chart = await _host.RunAsync(sp => Svc<IEmployeeService>(sp).OrgChartAsync());
        var root = Assert.Single(chart);
        Assert.Equal(ceo, root.Id);
        Assert.Equal(dev, root.Reports.Single().Reports.Single().Id);
    }

    [Fact]
    public async Task Bank_account_and_pan_are_encrypted_at_rest_and_masked()
    {
        var id = await AddEmployeeAsync(_orgA, "Gita");
        _host.Context.AsTenant(_orgA);
        Assert.True((await _host.RunAsync(sp => Svc<IEmployeeService>(sp).SaveBankAsync(id, new BankInput
            { BankName = "HDFC Bank", AccountHolderName = "Gita Kumar", AccountNumber = "50100012345678", Ifsc = "hdfc0001234" }))).Succeeded);
        Assert.True((await _host.RunAsync(sp => Svc<IEmployeeService>(sp).UpdatePersonalAsync(id, new PersonalInput { Nationality = "Indian", Pan = "abcde1234f" }, canEditSensitive: true))).Succeeded);

        var raw = await _host.RunAsync(async sp =>
        {
            var db = Svc<AppDbContext>(sp);
            return (await db.EmployeeBankDetails.SingleAsync(b => b.EmployeeId == id), await db.Employees.SingleAsync(e => e.Id == id));
        });
        Assert.DoesNotContain("50100012345678", raw.Item1.AccountNumberEncrypted);
        Assert.Equal("5678", raw.Item1.AccountNumberLast4);
        Assert.Equal("HDFC0001234", raw.Item1.Ifsc);
        Assert.DoesNotContain("ABCDE1234F", raw.Item2.PanEncrypted);
        Assert.Equal("XXXXXX234F", raw.Item2.PanMasked);

        var withoutPermission = (await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(id, includeSensitive: false)))!;
        Assert.Null(withoutPermission.BankAccountPlain);
        Assert.Null(withoutPermission.PanPlain);
        var withPermission = (await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(id, includeSensitive: true)))!;
        Assert.Equal("50100012345678", withPermission.BankAccountPlain);
        Assert.Equal("ABCDE1234F", withPermission.PanPlain);

        // A caller without the sensitive permission cannot overwrite the PAN.
        Assert.True((await _host.RunAsync(sp => Svc<IEmployeeService>(sp).UpdatePersonalAsync(id, new PersonalInput { Nationality = "Indian", Pan = "ZZZZZ9999Z" }, canEditSensitive: false))).Succeeded);
        Assert.Equal("ABCDE1234F", (await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(id, true)))!.PanPlain);

        // Audit trail never contains the encrypted values.
        _host.Context.AsPlatform();
        var audits = await _host.RunAsync(sp => Svc<AppDbContext>(sp).AuditLogs.Where(a => a.EntityType == "EmployeeBankDetail" || a.EntityType == "Employee").ToListAsync());
        Assert.All(audits, a => Assert.DoesNotContain("Encrypted", (a.NewValues ?? "") + (a.OldValues ?? "")));
    }

    private static UploadedFile Pdf(string name = "pan.pdf") =>
        new(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\n%test document\n")), name, 24);

    [Fact]
    public async Task Documents_are_validated_isolated_and_counted_against_storage()
    {
        var a = await AddEmployeeAsync(_orgA, "Hari");
        _host.Context.AsTenant(_orgA);
        var docs = (Func<IServiceProvider, IDocumentService>)Svc<IDocumentService>;

        var fake = await _host.RunAsync(sp => docs(sp).UploadAsync(a, new DocumentUploadInput { Title = "Fake" },
            new UploadedFile(new MemoryStream(Encoding.ASCII.GetBytes("MZ-not-a-pdf")), "evil.pdf", 12)));
        Assert.False(fake.Succeeded); // content does not match extension

        var exe = await _host.RunAsync(sp => docs(sp).UploadAsync(a, new DocumentUploadInput { Title = "Exe" },
            new UploadedFile(new MemoryStream([0x4D, 0x5A]), "tool.exe", 2)));
        Assert.False(exe.Succeeded);

        var ok = await _host.RunAsync(sp => docs(sp).UploadAsync(a, new DocumentUploadInput { Title = "PAN card", ExpiresOn = _host.Clock.Today.AddDays(10) }, Pdf()));
        Assert.True(ok.Succeeded, ok.Error);
        var docId = (await _host.RunAsync(sp => docs(sp).ForEmployeeAsync(a, false))).Single().Id;

        await using (var opened = (await _host.RunAsync(sp => docs(sp).OpenAsync(docId, null)))!.Content)
            Assert.True(opened.Length > 0);
        Assert.Single(await _host.RunAsync(sp => docs(sp).ExpiringAsync(30)));

        // Another organization cannot open it, even knowing the id.
        _host.Context.AsTenant(_orgB);
        Assert.Null(await _host.RunAsync(sp => docs(sp).OpenAsync(docId, null)));
        await AddEmployeeAsync(_orgB, "Other");
        _host.Context.AsTenant(_orgB);
        Assert.False((await _host.RunAsync(sp => docs(sp).DeleteAsync(docId))).Succeeded);

        // Self-service restriction: only the owner's visible documents.
        _host.Context.AsTenant(_orgA);
        Assert.Null(await _host.RunAsync(sp => docs(sp).OpenAsync(docId, restrictToEmployeeId: a + 999)));

        _host.Context.AsPlatform();
        Assert.Equal(1, await _host.RunAsync(sp => Svc<AppDbContext>(sp).Organizations.Where(o => o.Id == _orgA).Select(o => o.StorageUsedMb).SingleAsync()));
        Assert.Single(Directory.GetFiles(Path.Combine(_host.StorageRoot, _orgA.ToString()), "*", SearchOption.AllDirectories));

        _host.Context.AsTenant(_orgA);
        Assert.True((await _host.RunAsync(sp => docs(sp).DeleteAsync(docId))).Succeeded);
        Assert.Empty(Directory.GetFiles(Path.Combine(_host.StorageRoot, _orgA.ToString()), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Exit_workflow_requires_checklist_and_disables_login_on_completion()
    {
        var manager = await AddEmployeeAsync(_orgA, "Mgr");
        var leaver = await AddEmployeeAsync(_orgA, "Lee", login: true);
        var report = await AddEmployeeAsync(_orgA, "Rep", managerId: leaver);
        _host.Context.AsTenant(_orgA);
        var profile = (await _host.RunAsync(sp => Svc<IEmployeeService>(sp).GetProfileAsync(leaver, false)))!;
        await _host.RunAsync(sp => Svc<IEmployeeService>(sp).UpdateEmploymentAsync(leaver, new EmploymentInput
            { FirstName = "Lee", LastName = "Kumar", WorkEmail = profile.Employee.WorkEmail, DateOfJoining = profile.Employee.DateOfJoining, ReportingManagerId = manager }));

        var exits = (Func<IServiceProvider, IExitService>)Svc<IExitService>;
        var resign = await _host.RunAsync(sp => exits(sp).InitiateAsync(leaver, new ExitInitiateInput
            { Type = ExitType.Resignation, Reason = "Higher studies", RequestedLastWorkingDay = _host.Clock.Today.AddDays(30) }, byEmployee: true));
        Assert.True(resign.Succeeded, resign.Error);
        var duplicate = await _host.RunAsync(sp => exits(sp).InitiateAsync(leaver, new ExitInitiateInput
            { Reason = "again", RequestedLastWorkingDay = _host.Clock.Today.AddDays(30) }, byEmployee: true));
        Assert.False(duplicate.Succeeded);

        Assert.True((await _host.RunAsync(sp => exits(sp).ApproveAsync(resign.Value, _host.Clock.Today.AddDays(20), "Approved"))).Succeeded);
        var exit = (await _host.RunAsync(sp => exits(sp).GetAsync(resign.Value)))!;
        Assert.Equal(EmployeeStatus.NoticePeriod, exit.Employee.Status);
        Assert.Equal(7, exit.Checklist.Count); // default template

        var early = await _host.RunAsync(sp => exits(sp).CompleteAsync(resign.Value));
        Assert.False(early.Succeeded);

        foreach (var item in exit.Checklist)
            Assert.True((await _host.RunAsync(sp => exits(sp).SetChecklistItemAsync(resign.Value, item.Id, true))).Succeeded);
        var done = await _host.RunAsync(sp => exits(sp).CompleteAsync(resign.Value));
        Assert.True(done.Succeeded, done.Error);

        var after = await _host.RunAsync(async sp =>
        {
            var db = Svc<AppDbContext>(sp);
            var e = await db.Employees.Include(x => x.User).SingleAsync(x => x.Id == leaver);
            var r = await db.Employees.SingleAsync(x => x.Id == report);
            return (e, r);
        });
        Assert.Equal(EmployeeStatus.Exited, after.e.Status);
        Assert.Equal(_host.Clock.Today.AddDays(20), after.e.DateOfExit);
        Assert.False(after.e.User!.IsActive);
        Assert.Equal(manager, after.r.ReportingManagerId); // reports moved up to the leaver's manager

        _host.Context.AsAnonymous();
        var login = await _host.RunAsync(sp => Svc<ITenantAuthService>(sp).SignInAsync("alpha", $"lee@{_orgA}.test", "Welcome@123", null, null));
        Assert.Equal(AuthStatus.Disabled, login.Status);
    }

    [Fact]
    public async Task Reports_count_headcount_and_export_valid_excel()
    {
        await AddEmployeeAsync(_orgA, "One");
        await AddEmployeeAsync(_orgA, "Two");
        await AddEmployeeAsync(_orgB, "NotMine");
        _host.Context.AsTenant(_orgA);
        var summary = await _host.RunAsync(sp => Svc<IHrReportService>(sp).HeadcountAsync());
        Assert.Equal(2, summary.Current);
        Assert.Equal(2, summary.JoinedThisMonth);

        var file = await _host.RunAsync(sp => Svc<IHrReportService>(sp).ExportEmployeesAsync(new EmployeeQuery()));
        Assert.Equal((byte)'P', file.Content[0]); // xlsx is a zip container
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(file.Content));
        var ws = wb.Worksheet(1);
        Assert.Equal("Employee ID", ws.Cell(1, 1).GetString());
        Assert.Equal(3, ws.LastRowUsed()!.RowNumber()); // header + 2 employees of org A only
    }

    [Fact]
    public async Task Upgrade_seeding_grants_new_permissions_once_and_keeps_admin_removals()
    {
        // Simulate org A created before "org.exit.manage" existed, and an admin who removed "org.reports.view" from HR Admin.
        _host.Context.AsPlatform();
        await _host.RunAsync(async sp =>
        {
            var db = Svc<AppDbContext>(sp);
            var exitPerm = await db.Permissions.SingleAsync(p => p.Code == TenantPermissions.ExitManage);
            db.RolePermissions.RemoveRange(await db.RolePermissions.Where(rp => rp.PermissionId == exitPerm.Id).ToListAsync());
            db.Permissions.Remove(exitPerm);
            var hrAdminA = await db.Roles.SingleAsync(r => r.OrganizationId == _orgA && r.Name == "HR Admin");
            db.RolePermissions.RemoveRange(await db.RolePermissions
                .Where(rp => rp.RoleId == hrAdminA.Id && rp.Permission.Code == TenantPermissions.ReportsView).ToListAsync());
            await db.SaveChangesAsync();
        });

        await _host.RunAsync(sp => Svc<DbSeeder>(sp).SeedAsync());

        _host.Context.AsTenant(_orgA);
        Task<List<string>> PermsOf(string role) => _host.RunAsync(sp => Svc<AppDbContext>(sp).Roles.Where(r => r.Name == role)
            .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code)).ToListAsync());

        var hrAdmin = await PermsOf("HR Admin");
        Assert.Contains(TenantPermissions.ExitManage, hrAdmin);          // new permission granted
        Assert.DoesNotContain(TenantPermissions.ReportsView, hrAdmin);   // admin's earlier removal respected
        Assert.Contains(TenantPermissions.ExitManage, await PermsOf(TenantPermissions.OrgAdminRole));
        Assert.DoesNotContain(TenantPermissions.ExitManage, await PermsOf("HR Executive")); // not in its defaults
        Assert.Empty(await PermsOf("Employee"));
    }
}
