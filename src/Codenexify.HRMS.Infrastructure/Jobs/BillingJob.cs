using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Jobs;

/// <summary>Every 6 hours: renewal invoices, payment reminders and trial-ending notices (each idempotent per day).</summary>
public sealed class BillingJob(IServiceScopeFactory scopes, ILogger<BillingJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var platform = scope.ServiceProvider.GetRequiredService<ICurrentContext>().BeginPlatformScope();
                var r = await scope.ServiceProvider.GetRequiredService<IBillingAutomation>().RunAsync(stoppingToken);
                if (r.RenewalInvoices + r.Reminders + r.TrialNotices > 0)
                    logger.LogInformation("Billing job: {Renewals} renewal invoice(s), {Reminders} reminder(s), {Trials} trial notice(s).", r.RenewalInvoices, r.Reminders, r.TrialNotices);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Billing job failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
