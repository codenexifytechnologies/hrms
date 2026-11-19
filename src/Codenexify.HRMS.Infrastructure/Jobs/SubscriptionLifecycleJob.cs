using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Jobs;

/// <summary>Hourly job: expires lapsed trials, moves unpaid renewals to past-due and ends grace periods.</summary>
public sealed class SubscriptionLifecycleJob(IServiceScopeFactory scopes, ILogger<SubscriptionLifecycleJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // let the app finish starting
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ICurrentContext>();
                using (context.BeginPlatformScope())
                {
                    var changed = await scope.ServiceProvider.GetRequiredService<ISubscriptionService>().ProcessLifecycleAsync(stoppingToken);
                    if (changed > 0) logger.LogInformation("Subscription lifecycle updated {Count} subscription(s).", changed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscription lifecycle job failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
