using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Identity;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Infrastructure.Jobs;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool runBackgroundJobs = true)
    {
        // The connection string only ever comes from configuration (appsettings*.json / ConnectionStrings__Default).
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default is not set. Add it to appsettings.json (or the environment variable ConnectionStrings__Default).");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
            connectionString,
            // No retrying execution strategy: the audit trail and provisioning use explicit transactions.
            sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<SecurityOptions>(config.GetSection(SecurityOptions.Section));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IExcelExporter, ClosedXmlExporter>();
        services.AddSingleton<Application.Documents.IPdfRenderer, MigraDocPdfRenderer>();
        services.AddScoped<IPlatformDashboardService, PlatformDashboardService>();
        services.AddScoped<Application.Support.IDataResetService, DataResetService>();
        services.AddScoped<DbSeeder>();
        services.AddHttpClient(RazorpayGateway.HttpClientName, c =>
        {
            c.BaseAddress = new Uri("https://api.razorpay.com/v1/");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<Application.Billing.IPaymentGateway, RazorpayGateway>();
        services.AddSingleton<Application.Billing.IEmailSender, SmtpEmailSender>();
        services.AddHttpClient(Msg91SmsSender.HttpClientName, c =>
        {
            c.BaseAddress = new Uri("https://control.msg91.com/api/v5/");
            c.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton<ISmsSender, Msg91SmsSender>();

        if (runBackgroundJobs)
        {
            services.AddHostedService<BillingJob>();
            services.AddHostedService<NotificationEmailJob>();
            services.AddHostedService<SubscriptionLifecycleJob>();
            services.AddHostedService<TimeAttendanceJob>();
        }
        return services;
    }
}
