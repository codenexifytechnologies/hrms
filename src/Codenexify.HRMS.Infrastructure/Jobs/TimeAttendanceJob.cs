using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Codenexify.HRMS.Infrastructure.Jobs;

/// <summary>
/// Hourly, per organization: finalizes the last two days of attendance (absences become visible once a day ends)
/// and refreshes leave accruals for the current year; also deletes expired clock-in photos, sends reminder-rule
/// notifications and scheduled report emails. Each organization runs in its own tenant scope.
/// </summary>
public sealed class TimeAttendanceJob(IServiceScopeFactory scopes, ILogger<TimeAttendanceJob> logger, Microsoft.Extensions.Configuration.IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Time & attendance job failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<int> orgIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            using var platform = scope.ServiceProvider.GetRequiredService<ICurrentContext>().BeginPlatformScope();
            orgIds = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Organizations
                .Where(o => o.Status == OrganizationStatus.Active || o.Status == OrganizationStatus.Trial)
                .Select(o => o.Id).ToListAsync(ct);
        }

        foreach (var orgId in orgIds)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sp = scope.ServiceProvider;
                var features = await sp.GetRequiredService<IFeatureAccessService>().GetEnabledFeaturesAsync(orgId, ct);
                using var tenant = sp.GetRequiredService<ICurrentContext>().BeginOrganizationScope(orgId);
                var attendance = sp.GetRequiredService<IAttendanceService>();
                var today = await attendance.TodayForOrganizationAsync(ct);
                if (features.Contains(FeatureCodes.Attendance))
                    await attendance.RecalculateRangeAsync(null, today.AddDays(-2), today.AddDays(-1), ct);
                if (features.Contains(FeatureCodes.Leave))
                    await sp.GetRequiredService<ILeaveService>().EnsureBalancesAsync(today.Year, null, ct);
                // Clock-in photos are deleted after the retention period even if the feature was later removed.
                await sp.GetRequiredService<Application.Strength.ISelfieService>().PurgeExpiredAsync(ct);
                if (features.Contains(FeatureCodes.NotificationsAdvanced))
                    await sp.GetRequiredService<Application.Strength.IReminderService>().RunAsync(ct);
                if (features.Contains(FeatureCodes.ReportsAdvanced))
                    await sp.GetRequiredService<Application.Strength.IReportScheduleService>().RunDueAsync(config["Platform:PublicUrl"], ct);
                // Enrol newly eligible employees in automatic benefit plans; end memberships of employees who left.
                if (features.Contains(FeatureCodes.Benefits))
                    await sp.GetRequiredService<Application.Benefits.IBenefitService>().SyncAsync(null, ct);
                // Approved promotions take effect (new title and salary) on their date.
                if (features.Contains(FeatureCodes.Promotions))
                    await sp.GetRequiredService<Application.Performance.IPromotionService>().ApplyDueAsync(ct);
                if (features.Contains(FeatureCodes.CompensationPlans))
                    await sp.GetRequiredService<Application.Performance.ICompensationService>().ApplyDueAsync(null, ct);
                // Automation rules run after the other jobs so they see today's promotions and joiners.
                if (features.Contains(FeatureCodes.WorkflowAutomation))
                    await sp.GetRequiredService<Application.Performance.IWorkflowService>().RunAsync(null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Time & attendance job failed for organization {OrganizationId}.", orgId);
            }
        }
    }
}
