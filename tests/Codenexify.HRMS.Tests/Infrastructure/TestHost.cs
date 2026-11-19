using Codenexify.HRMS.Application;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codenexify.HRMS.Tests.Infrastructure;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 6, 15, 6, 0, 0, DateTimeKind.Utc);
    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
    public void AdvanceDays(int days) => UtcNow = UtcNow.AddDays(days);
}

/// <summary>Switchable principal used by tests to act as platform, a given tenant, or anonymous.</summary>
public sealed class TestContext : CurrentContextBase
{
    public PrincipalType? Type { get; set; }
    public int? OrgId { get; set; }
    public int? User { get; set; }
    protected override PrincipalType? AuthenticatedType => Type;
    protected override int? AuthenticatedOrganizationId => OrgId;
    public override int? UserId => User;
    public override string? UserName => Type is null ? null : $"test-{Type}-{OrgId}";
    public override string? IpAddress => "127.0.0.1";

    public void AsPlatform() { Type = Domain.Enums.PrincipalType.Platform; OrgId = null; User = 1; }
    public void AsTenant(int orgId, int userId = 1) { Type = Domain.Enums.PrincipalType.Tenant; OrgId = orgId; User = userId; }
    public void AsAnonymous() { Type = null; OrgId = null; User = null; }
}

/// <summary>Records gateway calls; returns predictable order/refund ids.</summary>
public sealed class FakePaymentGateway : Application.Billing.IPaymentGateway
{
    public List<(string OrderId, decimal Amount)> Orders { get; } = [];
    public List<(string PaymentId, decimal Amount)> Refunds { get; } = [];

    public Task<Application.Billing.GatewayOrder> CreateOrderAsync(decimal amount, string receipt, IReadOnlyDictionary<string, string> notes, CancellationToken ct = default)
    {
        var id = $"order_test{Orders.Count + 1}";
        Orders.Add((id, amount));
        return Task.FromResult(new Application.Billing.GatewayOrder(id, (long)Math.Round(amount * 100), "INR"));
    }

    public Task<Application.Billing.GatewayRefund> RefundAsync(string paymentId, decimal amount, string note, CancellationToken ct = default)
    {
        Refunds.Add((paymentId, amount));
        return Task.FromResult(new Application.Billing.GatewayRefund($"rfnd_test{Refunds.Count}", "processed"));
    }
}

public sealed class FakeEmailSender : Application.Billing.IEmailSender
{
    public List<Application.Billing.EmailMessage> Sent { get; } = [];
    public Task<bool> SendAsync(Application.Billing.EmailMessage message, CancellationToken ct = default)
    {
        lock (Sent) Sent.Add(message);
        return Task.FromResult(true);
    }
}

/// <summary>Records SMS codes instead of sending them.</summary>
public sealed class FakeSmsSender : Application.Identity.ISmsSender
{
    public bool Configured { get; set; } = true;
    public List<(string Mobile, string Code)> Sent { get; } = [];
    public Task<bool> IsConfiguredAsync(CancellationToken ct = default) => Task.FromResult(Configured);
    public Task<bool> SendOtpAsync(string mobile, string code, CancellationToken ct = default)
    {
        lock (Sent) Sent.Add((mobile, code));
        return Task.FromResult(Configured);
    }
}

/// <summary>SQLite does not generate SQL Server rowversions; treat them as plain columns in tests.</summary>
public sealed class TestAppDbContext(DbContextOptions<AppDbContext> options, ICurrentContext ctx, IClock clock) : AppDbContext(options, ctx, clock)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var et in modelBuilder.Model.GetEntityTypes())
        {
            var rv = et.FindProperty("RowVersion");
            if (rv is null) continue;
            modelBuilder.Entity(et.ClrType).Property("RowVersion").ValueGeneratedNever().IsConcurrencyToken(false);
        }
    }
}

/// <summary>Real application services over an in-memory SQLite database.</summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }
    public TestContext Context { get; } = new();
    public FakeClock Clock { get; } = new();
    public string StorageRoot { get; private set; } = string.Empty;
    public FakePaymentGateway Gateway { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeSmsSender Sms { get; } = new();

    private TestHost()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ICurrentContext>(Context);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.Configure<SecurityOptions>(_ => { });
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        services.AddScoped<AppDbContext>(sp => new TestAppDbContext(options, Context, Clock));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<DbSeeder>();
        StorageRoot = Path.Combine(Path.GetTempPath(), "cnx-hrms-tests", Guid.NewGuid().ToString("N"));
        services.AddSingleton<IFileStorage>(new LocalFileStorage(StorageRoot));
        services.AddSingleton<IExcelExporter, ClosedXmlExporter>();
        services.AddSingleton<Application.Documents.IPdfRenderer, MigraDocPdfRenderer>();
        services.AddSingleton<Application.Billing.IPaymentGateway>(Gateway);
        services.AddSingleton<Application.Billing.IEmailSender>(Email);
        services.AddSingleton<Application.Identity.ISmsSender>(Sms);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false });
    }

    public static async Task<TestHost> CreateAsync()
    {
        var host = new TestHost();
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        await new DbSeeder(scope.ServiceProvider.GetRequiredService<AppDbContext>(), host.Context,
            scope.ServiceProvider.GetRequiredService<IPasswordService>(), new ConfigurationBuilder().Build(), NullLogger<DbSeeder>.Instance).SeedAsync();
        return host;
    }

    /// <summary>Runs <paramref name="action"/> in a fresh DI scope (fresh DbContext) and returns its result.</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task RunAsync(Func<IServiceProvider, Task> action) => RunAsync<bool>(async sp => { await action(sp); return true; });

    public async Task<int> PlanIdAsync(string code) =>
        await RunAsync(sp => sp.GetRequiredService<AppDbContext>().SubscriptionPlans.Where(p => p.Code == code).Select(p => p.Id).SingleAsync());

    /// <summary>Provisions an organization as the platform owner and returns its id.</summary>
    public async Task<int> ProvisionAsync(string code, string plan = "FOUNDATION", bool trial = true, string password = "Passw0rd!x")
    {
        var previous = (Context.Type, Context.OrgId, Context.User);
        Context.AsPlatform();
        var planId = await PlanIdAsync(plan);
        var result = await RunAsync(sp => sp.GetRequiredService<IProvisioningService>().ProvisionAsync(new OrganizationRegistrationInput
        {
            CompanyName = $"{code} Pvt Ltd", Code = code, CompanyEmail = $"hr@{code}.test", PlanId = planId,
            AdminName = $"{code} Admin", AdminEmail = $"admin@{code}.test", AdminPassword = password
        }, trial));
        (Context.Type, Context.OrgId, Context.User) = previous;
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
        try { if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
