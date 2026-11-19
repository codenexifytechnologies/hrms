using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Engagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Jobs;

/// <summary>Every minute: emails copies of new in-app notifications (plan and user preference permitting).</summary>
public sealed class NotificationEmailJob(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationEmailJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var platform = scope.ServiceProvider.GetRequiredService<ICurrentContext>().BeginPlatformScope();
                var sent = await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>().RunAsync(config["Platform:PublicUrl"], stoppingToken);
                if (sent > 0) logger.LogInformation("Emailed {Count} notification(s).", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification email job failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
