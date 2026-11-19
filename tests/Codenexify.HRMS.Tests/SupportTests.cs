using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Domain.Support;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class SupportTests : IAsyncLifetime
{
    private TestHost _host = null!;

    private int _staffId;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _host.Context.AsPlatform();
        _staffId = await _host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var staff = new Domain.Platform.PlatformUser { Email = "asha@codenexify.test", FullName = "Asha Support", PasswordHash = "x", Role = Domain.Enums.PlatformRole.SupportAdmin };
            db.PlatformUsers.Add(staff);
            await db.SaveChangesAsync();
            return staff.Id;
        });
    }
    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<(int Org, int Admin)> OrgAsync(string code)
    {
        var org = await _host.ProvisionAsync(code);
        _host.Context.AsPlatform();
        var admin = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().Where(u => u.OrganizationId == org).Select(u => u.Id).FirstAsync());
        return (org, admin);
    }

    private Task<T> As<T, TService>(Action set, Func<TService, Task<T>> action) where TService : notnull
    {
        set();
        return _host.RunAsync(sp => action(sp.GetRequiredService<TService>()));
    }

    private Task<T> Tickets<T>(Action set, Func<ISupportTicketService, Task<T>> a) => As(set, a);
    private Task<T> Access<T>(Action set, Func<ISupportAccessService, Task<T>> a) => As(set, a);
    private Action Tenant(int org, int user) => () => _host.Context.AsTenant(org, user);
    private Action Platform => () => { _host.Context.AsPlatform(); _host.Context.User = _staffId; };

    private async Task SetSupportInboxAsync()
    {
        var s = await As<MaintenanceInput, IAnnouncementService>(Platform, x => x.GetSystemSettingsAsync());
        s.SupportEmail = "support@codenexify.test";
        Assert.True((await As<Result, IAnnouncementService>(Platform, x => x.SaveSystemSettingsAsync(s))).Succeeded);
    }

    // ------------------------------------------------------------------ tickets

    [Fact]
    public async Task Ticket_conversation_flows_between_customer_and_support_with_private_notes()
    {
        await SetSupportInboxAsync();
        var (org, admin) = await OrgAsync("tick1");
        var created = await Tickets(Tenant(org, admin), t => t.CreateAsync(new TicketInput { Subject = "Payslip shows wrong PF", Category = TicketCategory.Payroll, Body = "PF is computed on gross instead of basic." }));
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value;
        Assert.Contains(_host.Email.Sent, m => m.To == "support@codenexify.test" && m.Subject.Contains($"#{id}"));

        // Customers cannot write internal notes or triage.
        Assert.False((await Tickets(Tenant(org, admin), t => t.ReplyAsync(id, "secret", internalNote: true))).Succeeded);
        await Assert.ThrowsAsync<TenantIsolationException>(() => Tickets(Tenant(org, admin), t => t.UpdateAsync(id, TicketStatus.Closed, TicketPriority.Low, null)));

        Assert.True((await Tickets(Platform, t => t.ReplyAsync(id, "Looks like a structure setup issue.", internalNote: true))).Succeeded);
        Assert.True((await Tickets(Platform, t => t.ReplyAsync(id, "Please check the PF wage setting under Payroll → Settings."))).Succeeded);

        var customerView = await Tickets(Tenant(org, admin), t => t.GetAsync(id));
        Assert.Equal(2, customerView!.Messages.Count); // the internal note is never sent to the customer
        Assert.DoesNotContain(customerView.Messages, m => m.IsInternal);
        Assert.Equal(TicketStatus.WaitingOnCustomer, customerView.Ticket.Status);
        Assert.NotNull(customerView.Ticket.FirstResponseAtUtc);
        Assert.Contains(_host.Email.Sent, m => m.To == $"admin@tick1.test" && m.Subject.Contains($"#{id}"));
        Assert.DoesNotContain(_host.Email.Sent, m => m.HtmlBody.Contains("structure setup issue"));

        Assert.Equal(3, (await Tickets(Platform, t => t.GetAsync(id)))!.Messages.Count);

        // A customer reply puts it back in the queue.
        Assert.True((await Tickets(Tenant(org, admin), t => t.ReplyAsync(id, "Fixed, thanks. One more question…"))).Succeeded);
        var row = (await Tickets(Platform, t => t.ListAsync(new TicketQuery { OpenOnly = true }))).Items.Single(r => r.Id == id);
        Assert.Equal(TicketStatus.Open, row.Status);
        Assert.True(row.AwaitingSupport);

        Assert.True((await Tickets(Tenant(org, admin), t => t.CloseAsync(id))).Succeeded);
        Assert.False((await Tickets(Tenant(org, admin), t => t.ReplyAsync(id, "after close"))).Succeeded);
        Assert.True((await Tickets(Tenant(org, admin), t => t.ReopenAsync(id))).Succeeded);
    }

    [Fact]
    public async Task Organizations_cannot_see_each_others_tickets()
    {
        var (a, adminA) = await OrgAsync("ticka");
        var (b, adminB) = await OrgAsync("tickb");
        var id = (await Tickets(Tenant(a, adminA), t => t.CreateAsync(new TicketInput { Subject = "Org A problem", Body = "Something is wrong here." }))).Value;

        Assert.Null(await Tickets(Tenant(b, adminB), t => t.GetAsync(id)));
        Assert.Empty((await Tickets(Tenant(b, adminB), t => t.ListAsync(new TicketQuery { OrganizationId = a }))).Items);
        Assert.False((await Tickets(Tenant(b, adminB), t => t.ReplyAsync(id, "hijack"))).Succeeded);
        Assert.False((await Tickets(Tenant(b, adminB), t => t.CloseAsync(id))).Succeeded);
        Assert.Single((await Tickets(Platform, t => t.ListAsync(new TicketQuery { OrganizationId = a }))).Items);
    }

    // ------------------------------------------------------------------ support access

    [Fact]
    public async Task Read_only_access_needs_consent_is_time_boxed_and_revocable()
    {
        var (org, admin) = await OrgAsync("acces");
        var (other, otherAdmin) = await OrgAsync("other");

        // No consent → nothing to open; platform staff cannot grant on the customer's behalf.
        Assert.Empty(await Access(Platform, a => a.GrantsAsync()));
        await Assert.ThrowsAsync<TenantIsolationException>(() => Access(Platform, a => a.GrantAsync(new GrantInput { Hours = 2, Reason = "self-granted" })));

        Assert.True((await Access(Tenant(org, admin), a => a.GrantAsync(new GrantInput { Hours = 24, Reason = "Check leave policy" }))).Succeeded);
        var grant = (await Access(Platform, a => a.GrantsAsync())).Single();
        Assert.Equal((org, false), (grant.OrganizationId, grant.IncludePayroll));

        var started = await Access(Platform, a => a.StartSessionAsync(grant.Id));
        Assert.True(started.Succeeded, started.Error);
        var session = started.Value!;
        Assert.Equal(_host.Clock.UtcNow.AddHours(2), session.ExpiresAtUtc); // one visit ≤ 2 h even with a 24 h grant
        Assert.DoesNotContain(TenantPermissions.EmployeesSensitive, session.Permissions);
        Assert.DoesNotContain(TenantPermissions.PayrollView, session.Permissions);
        Assert.DoesNotContain(TenantPermissions.UsersManage, session.Permissions);
        Assert.DoesNotContain(TenantPermissions.BillingManage, session.Permissions);
        Assert.Contains(TenantPermissions.EmployeesView, session.Permissions);

        // Page views are counted and written to the organization's own audit trail.
        Assert.True(await Access(Tenant(org, 0), a => a.IsSessionActiveAsync(session.SessionId)));
        await Access(Tenant(org, 0), async a => { await a.RecordViewAsync(session.SessionId, "/org/employees"); return true; });
        _host.Context.AsPlatform();
        Assert.True(await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().AuditLogs
            .AnyAsync(l => l.OrganizationId == org && l.Action == "SupportSession.View" && l.Details == "/org/employees")));

        // Another organization can neither see nor revoke it.
        Assert.False((await Access(Tenant(other, otherAdmin), a => a.RevokeAsync(grant.Id))).Succeeded);
        Assert.False(await Access(Tenant(other, otherAdmin), a => a.IsSessionActiveAsync(session.SessionId)));

        // The customer revokes: the running session dies immediately.
        Assert.True((await Access(Tenant(org, admin), a => a.RevokeAsync(grant.Id))).Succeeded);
        Assert.False(await Access(Tenant(org, 0), a => a.IsSessionActiveAsync(session.SessionId)));
        Assert.False((await Access(Platform, a => a.StartSessionAsync(grant.Id))).Succeeded);
        var history = (await Access(Tenant(org, admin), a => a.GrantsAsync())).Single();
        Assert.Equal(1, history.Sessions.Single().PageViews);
        Assert.NotNull(history.Sessions.Single().EndedAtUtc);
    }

    [Fact]
    public async Task Expired_consent_ends_the_session_and_payroll_is_opt_in()
    {
        var (org, admin) = await OrgAsync("expir");
        Assert.True((await Access(Tenant(org, admin), a => a.GrantAsync(new GrantInput { Hours = 1, Reason = "Payroll check", IncludePayroll = true }))).Succeeded);
        var grant = (await Access(Platform, a => a.GrantsAsync())).Single();
        var session = (await Access(Platform, a => a.StartSessionAsync(grant.Id))).Value!;
        Assert.Contains(TenantPermissions.PayrollView, session.Permissions);
        Assert.DoesNotContain(TenantPermissions.PayrollManage, session.Permissions);
        Assert.Equal(_host.Clock.UtcNow.AddHours(1), session.ExpiresAtUtc); // capped by the consent

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddMinutes(61);
        Assert.False(await Access(Tenant(org, 0), a => a.IsSessionActiveAsync(session.SessionId)));
        Assert.Empty(await Access(Platform, a => a.GrantsAsync()));
    }

    // ------------------------------------------------------------------ middleware

    private sealed class FakeAccess(bool active) : ISupportAccessService
    {
        public List<string> Views { get; } = [];
        public Task<bool> IsSessionActiveAsync(int sessionId, CancellationToken ct = default) => Task.FromResult(active);
        public Task RecordViewAsync(int sessionId, string path, CancellationToken ct = default) { Views.Add(path); return Task.CompletedTask; }
        public Task<Result> GrantAsync(GrantInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> RevokeAsync(int grantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GrantRow>> GrantsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<SupportSessionTicket>> StartSessionAsync(int grantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EndSessionAsync(int sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(int Status, bool NextCalled)> RunSupportMiddleware(string method, string path, FakeAccess access)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.User = SupportSessionMiddleware.CreatePrincipal(new SupportSessionTicket(7, 1, "demo", "Demo", "Asha", "asha@codenexify.test",
            DateTime.UtcNow.AddHours(1), TenantPermissions.SupportSessionPermissions(false)));
        ctx.Response.Body = new MemoryStream();
        var services = new ServiceCollection().AddLogging();
        services.AddAuthentication().AddCookie(Schemes.Tenant);
        ctx.RequestServices = services.BuildServiceProvider();
        var called = false;
        var mw = new SupportSessionMiddleware(_ => { called = true; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, access);
        return (ctx.Response.StatusCode, called);
    }

    [Fact]
    public async Task Support_session_principal_can_only_read()
    {
        var access = new FakeAccess(active: true);
        Assert.Equal((200, true), await RunSupportMiddleware("GET", "/org/employees", access));
        Assert.Equal(new[] { "/org/employees" }, access.Views);

        Assert.Equal((403, false), await RunSupportMiddleware("POST", "/org/employees/create", access));
        Assert.Equal((403, false), await RunSupportMiddleware("POST", "/logout", access));
        Assert.Equal((403, false), await RunSupportMiddleware("DELETE", "/org/users/3", access));
        Assert.Equal((403, false), await RunSupportMiddleware("GET", "/api/v1/me", access));
        Assert.Equal((200, true), await RunSupportMiddleware("POST", SupportSessionMiddleware.EndPath, access));

        var principal = SupportSessionMiddleware.CreatePrincipal(new SupportSessionTicket(7, 1, "demo", "Demo", "Asha", "asha@x", DateTime.UtcNow, []));
        Assert.Equal(0, principal.GetUserId()); // never matches a real tenant user
        Assert.Equal(7, principal.GetSupportSessionId());
    }

    [Fact]
    public async Task Ended_support_session_is_signed_out()
    {
        var (status, next) = await RunSupportMiddleware("GET", "/org", new FakeAccess(active: false));
        Assert.False(next);
        Assert.Equal(302, status);
    }

    // ------------------------------------------------------------------ announcements & maintenance

    [Fact]
    public async Task Announcements_target_plans_and_respect_their_window()
    {
        var org = await _host.ProvisionAsync("annou", "FOUNDATION");
        var growth = await _host.PlanIdAsync("GROWTH");
        var nowIst = _host.Clock.UtcNow.AddHours(5.5);
        async Task Save(AnnouncementInput i) => Assert.True((await As<Result, IAnnouncementService>(Platform, s => s.SaveAsync(null, i))).Succeeded);

        await Save(new AnnouncementInput { Title = "Everyone", Body = "Scheduled maintenance Sunday", StartsAt = nowIst.AddHours(-1), Severity = AnnouncementSeverity.Critical, Dismissible = true });
        await Save(new AnnouncementInput { Title = "Growth only", Body = "New OKR module", StartsAt = nowIst.AddHours(-1), PlanId = growth });
        await Save(new AnnouncementInput { Title = "Later", Body = "Not yet", StartsAt = nowIst.AddDays(1) });
        await Save(new AnnouncementInput { Title = "Draft", Body = "Unpublished", StartsAt = nowIst.AddHours(-1), IsActive = false });
        Assert.False((await As<Result, IAnnouncementService>(Platform, s => s.SaveAsync(null, new AnnouncementInput { Title = "Bad", Body = "x", StartsAt = nowIst, EndsAt = nowIst.AddHours(-1) }))).Succeeded);

        var shown = await As<IReadOnlyList<ActiveAnnouncement>, IAnnouncementService>(() => _host.Context.AsTenant(org), s => s.ActiveForOrganizationAsync(org));
        var only = Assert.Single(shown);
        Assert.Equal("Everyone", only.Title);
        Assert.False(only.Dismissible); // critical banners cannot be dismissed

        _host.Clock.UtcNow = _host.Clock.UtcNow.AddDays(2);
        _host.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove("cnx:announcements");
        Assert.Contains(await As<IReadOnlyList<ActiveAnnouncement>, IAnnouncementService>(() => _host.Context.AsTenant(org), s => s.ActiveForOrganizationAsync(org)), a => a.Title == "Later");
    }

    [Fact]
    public async Task Maintenance_mode_blocks_portals_but_not_the_control_center()
    {
        var input = await As<MaintenanceInput, IAnnouncementService>(Platform, s => s.GetSystemSettingsAsync());
        input.Enabled = true;
        input.Message = "Upgrading";
        Assert.True((await As<Result, IAnnouncementService>(Platform, s => s.SaveSystemSettingsAsync(input))).Succeeded);
        await Assert.ThrowsAsync<TenantIsolationException>(() => As<Result, IAnnouncementService>(() => _host.Context.AsTenant(1), s => s.SaveSystemSettingsAsync(input)));

        _host.Context.AsAnonymous();
        using var scope = _host.Services.CreateScope();
        var system = scope.ServiceProvider.GetRequiredService<IAnnouncementService>();
        Assert.True((await system.MaintenanceAsync()).Enabled);

        async Task<(string Path, int Status)> Run(string path)
        {
            var ctx = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            ctx.Request.Path = path;
            ctx.Response.Body = new MemoryStream();
            string? seen = null;
            await new MaintenanceMiddleware(c => { seen = c.Request.Path; return Task.CompletedTask; }).InvokeAsync(ctx, system);
            return (seen ?? "(stopped)", ctx.Response.StatusCode);
        }

        Assert.Equal(("/maintenance", 200), await Run("/org/employees")); // rewritten; the page itself returns 503
        Assert.Equal(("/maintenance", 200), await Run("/login"));
        Assert.Equal(("(stopped)", 503), await Run("/api/v1/me"));
        Assert.Equal(("/superadmin/dashboard", 200), await Run("/superadmin/dashboard"));
        Assert.Equal(("/webhooks/razorpay", 200), await Run("/webhooks/razorpay"));
    }
}
