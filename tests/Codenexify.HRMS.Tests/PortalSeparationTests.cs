using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Codenexify.HRMS.Tests;

/// <summary>Runs the real web host (in memory, SQLite) to verify the two administration levels stay separate.</summary>
public sealed class HrmsWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:AutoMigrate"] = "false",
            ["Database:SeedOnStartup"] = "false", // the fixture creates and seeds the in-memory database itself
            ["BackgroundJobs:Enabled"] = "false",
            ["Seed:SuperAdminEmail"] = "owner@cnx.test",
            ["Seed:SuperAdminPassword"] = "Owner#Pass123"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            services.AddScoped<AppDbContext>(sp => new TestAppDbContext(options, sp.GetRequiredService<ICurrentContext>(), sp.GetRequiredService<IClock>()));
        });
    }

    public async Task<int> InitialiseAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        await sp.GetRequiredService<DbSeeder>().SeedAsync();
        using var platform = sp.GetRequiredService<ICurrentContext>().BeginPlatformScope();
        var planId = await sp.GetRequiredService<AppDbContext>().SubscriptionPlans.Where(p => p.Code == "FOUNDATION").Select(p => p.Id).SingleAsync();
        var result = await sp.GetRequiredService<IProvisioningService>().ProvisionAsync(new OrganizationRegistrationInput
        {
            CompanyName = "Acme Corp", Code = "acme", CompanyEmail = "hr@acme.test", PlanId = planId,
            AdminName = "Asha Admin", AdminEmail = "admin@acme.test", AdminPassword = "Passw0rd!x"
        }, startAsTrial: true);
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public sealed class PortalSeparationTests : IClassFixture<HrmsWebFactory>, IAsyncLifetime
{
    private readonly HrmsWebFactory _factory;
    private static bool _initialised;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public PortalSeparationTests(HrmsWebFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await Gate.WaitAsync();
        try { if (!_initialised) { await _factory.InitialiseAsync(); _initialised = true; } }
        finally { Gate.Release(); }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(m.Success, "antiforgery token not found on " + url);
        return m.Groups[1].Value;
    }

    private async Task<HttpClient> TenantClientAsync()
    {
        var client = NewClient();
        var token = await AntiforgeryTokenAsync(client, "/login");
        var resp = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CompanyCode"] = "acme", ["Email"] = "admin@acme.test", ["Password"] = "Passw0rd!x", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/org", resp.Headers.Location?.OriginalString);
        return client;
    }

    [Fact]
    public async Task Anonymous_users_are_sent_to_the_right_login_page()
    {
        var client = NewClient();
        var sa = await client.GetAsync("/superadmin/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, sa.StatusCode);
        Assert.StartsWith("http://localhost/superadmin/account/login", sa.Headers.Location!.ToString());

        var org = await client.GetAsync("/org/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, org.StatusCode);
        Assert.StartsWith("http://localhost/login", org.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Organization_admin_can_use_org_portal_but_never_super_admin()
    {
        var client = await TenantClientAsync();

        var dashboard = await client.GetAsync("/org/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains("Acme Corp", await dashboard.Content.ReadAsStringAsync());

        foreach (var url in new[] { "/superadmin", "/superadmin/dashboard", "/superadmin/organizations", "/superadmin/plans", "/superadmin/users" })
        {
            var resp = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
            Assert.Contains("/superadmin/account/login", resp.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Core_hr_pages_render_for_organization_admin()
    {
        var client = await TenantClientAsync();
        foreach (var url in new[] { "/org/employees", "/org/employees/create", "/org/employees/orgchart", "/org/structure", "/org/exits", "/org/reports", "/me",
                     "/org/leave", "/org/leave/type", "/org/attendance", "/org/attendance/register", "/org/shifts", "/org/shifts/edit",
                     "/me/notifications", "/me/approvals", "/me/leaderboard",
                     "/org/payroll", "/org/payroll/salaries", "/org/payroll/structures", "/org/payroll/structure", "/org/payroll/component",
                     "/org/payroll/settings", "/org/payroll/loans", "/org/payroll/declarations", "/org/payroll/gratuity", "/org/payroll/settlements" })
        {
            var resp = await client.GetAsync(url);
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"{url} returned {(int)resp.StatusCode}");
        }
        var export = await client.GetAsync("/org/employees/export");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var client = await TenantClientAsync();
        var resp = await client.PostAsync("/org/profile", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Hacked" }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Super_admin_can_sign_in_to_control_center()
    {
        var client = NewClient();
        var loginHtml = await client.GetStringAsync("/superadmin/account/login");
        Assert.Contains("action=\"/superadmin/account/login\"", loginHtml); // never posts to the tenant /login
        var token = await AntiforgeryTokenAsync(client, "/superadmin/account/login");
        var resp = await client.PostAsync("/superadmin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "owner@cnx.test", ["Password"] = "Owner#Pass123", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        // Seeded owner must change the initial password first.
        Assert.Contains("/superadmin/account/changepassword", resp.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var orgs = await client.GetAsync("/superadmin/organizations");
        Assert.Equal(HttpStatusCode.Redirect, orgs.StatusCode); // still forced to change password
        Assert.Contains("changepassword", orgs.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        // The platform cookie is path-scoped and is not accepted by the organization portal.
        var orgPortal = await client.GetAsync("/org/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, orgPortal.StatusCode);
        Assert.StartsWith("http://localhost/login", orgPortal.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Api_requires_bearer_token_and_returns_tenant_scoped_profile()
    {
        var client = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);

        var bad = await client.PostAsJsonAsync("/api/v1/auth/token", new { companyCode = "acme", email = "admin@acme.test", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var tokenResp = await client.PostAsJsonAsync("/api/v1/auth/token", new { companyCode = "acme", email = "admin@acme.test", password = "Passw0rd!x" });
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);
        var token = (await tokenResp.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["accessToken"].ToString();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadAsStringAsync();
        Assert.Contains("\"code\":\"acme\"", body);
        Assert.Contains("PAYROLL", body);

        // A tenant cookie is not accepted by the API.
        var cookieClient = await TenantClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cookieClient.GetAsync("/api/v1/me")).StatusCode);
    }
}
