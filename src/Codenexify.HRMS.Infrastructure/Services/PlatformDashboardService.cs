using System.Data;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Platform;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Infrastructure.Persistence;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Codenexify.HRMS.Infrastructure.Services;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public sealed class SqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    public IDbConnection Create() => new SqlConnection(config.GetConnectionString("Default"));
}

/// <summary>
/// Cross-tenant aggregates for the Super Admin dashboard. Uses Dapper with parameterised SQL;
/// this service is only reachable through the Platform authorization policy.
/// </summary>
public sealed class PlatformDashboardService(IDbConnectionFactory connections, AppDbContext db, ICurrentContext context, IClock clock)
    : IPlatformDashboardService
{
    public async Task<PlatformDashboard> GetAsync(CancellationToken ct = default)
    {
        if (!context.IsPlatform) throw new TenantIsolationException("Platform dashboard requires a platform principal.");
        using var conn = connections.Create();
        var today = clock.Today.ToDateTime(TimeOnly.MinValue);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var p = new { today, monthStart, since = clock.UtcNow.AddHours(-24), from12 = monthStart.AddMonths(-11), horizon = today.AddDays(30) };

        const string sql = """
            SELECT Status, COUNT(*) AS Cnt FROM Organizations WHERE IsDeleted = 0 GROUP BY Status;

            SELECT COUNT(*) AS Total, SUM(CASE WHEN u.IsActive = 1 THEN 1 ELSE 0 END) AS Active
            FROM Users u JOIN Organizations o ON o.Id = u.OrganizationId
            WHERE u.IsDeleted = 0 AND o.IsDeleted = 0;

            SELECT ISNULL(SUM(
                CASE WHEN p.IsPerEmployeePricing = 1 THEN s.Price * CASE WHEN uc.Cnt > 0 THEN uc.Cnt ELSE 1 END ELSE s.Price END
                / CASE WHEN s.BillingCycle = 'Yearly' THEN 12.0 ELSE 1.0 END), 0)
            FROM OrganizationSubscriptions s
            JOIN SubscriptionPlans p ON p.Id = s.PlanId
            JOIN Organizations o ON o.Id = s.OrganizationId AND o.IsDeleted = 0
            OUTER APPLY (SELECT COUNT(*) AS Cnt FROM Employees e WHERE e.OrganizationId = s.OrganizationId AND e.IsDeleted = 0 AND e.Status NOT IN ('Exited', 'Inactive')) uc
            WHERE s.IsCurrent = 1 AND s.Status IN ('Active', 'PastDue');

            SELECT s.Status, COUNT(*) AS Cnt FROM OrganizationSubscriptions s
            JOIN Organizations o ON o.Id = s.OrganizationId AND o.IsDeleted = 0
            WHERE s.IsCurrent = 1 GROUP BY s.Status;

            SELECT COUNT(*) FROM Organizations WHERE IsDeleted = 0 AND CreatedAtUtc >= @monthStart;

            SELECT COUNT(*) FROM LoginAudits WHERE Succeeded = 0 AND TimestampUtc >= @since;

            SELECT FORMAT(CreatedAtUtc, 'yyyy-MM') AS Label, CAST(COUNT(*) AS DECIMAL(18,2)) AS Value
            FROM Organizations WHERE CreatedAtUtc >= @from12
            GROUP BY FORMAT(CreatedAtUtc, 'yyyy-MM');

            SELECT p.Name AS Label, CAST(COUNT(s.Id) AS DECIMAL(18,2)) AS Value
            FROM SubscriptionPlans p
            LEFT JOIN OrganizationSubscriptions s ON s.PlanId = p.Id AND s.IsCurrent = 1
                AND s.Status IN ('Trial', 'Active', 'PastDue', 'Paused')
                AND EXISTS (SELECT 1 FROM Organizations o WHERE o.Id = s.OrganizationId AND o.IsDeleted = 0)
            GROUP BY p.Name, p.SortOrder ORDER BY p.SortOrder;

            SELECT TOP 8 o.Name AS Label, CAST(COUNT(u.Id) AS DECIMAL(18,2)) AS Value
            FROM Organizations o JOIN Users u ON u.OrganizationId = o.Id AND u.IsDeleted = 0
            WHERE o.IsDeleted = 0 GROUP BY o.Name ORDER BY COUNT(u.Id) DESC;

            SELECT COUNT(*) AS Total, SUM(CASE WHEN e.Status NOT IN ('Exited', 'Inactive') THEN 1 ELSE 0 END) AS Active
            FROM Employees e JOIN Organizations o ON o.Id = e.OrganizationId
            WHERE e.IsDeleted = 0 AND o.IsDeleted = 0;

            SELECT TOP 10 o.Id AS OrganizationId, o.Name AS OrganizationName, p.Name AS PlanName, s.Status, s.EndDate, s.AutoRenew
            FROM OrganizationSubscriptions s
            JOIN Organizations o ON o.Id = s.OrganizationId AND o.IsDeleted = 0
            JOIN SubscriptionPlans p ON p.Id = s.PlanId
            WHERE s.IsCurrent = 1 AND s.Status IN ('Trial', 'Active', 'PastDue') AND s.EndDate <= @horizon
            ORDER BY s.EndDate;
            """;

        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        var byStatusRaw = (await grid.ReadAsync<(string Status, int Cnt)>()).ToList();
        var users = await grid.ReadSingleAsync<(int Total, int? Active)>();
        var mrr = await grid.ReadSingleAsync<decimal>();
        var subs = (await grid.ReadAsync<(string Status, int Cnt)>()).ToDictionary(x => x.Status, x => x.Cnt);
        var newThisMonth = await grid.ReadSingleAsync<int>();
        var failed = await grid.ReadSingleAsync<int>();
        var regs = (await grid.ReadAsync<LabelValue>()).ToDictionary(x => x.Label, x => x.Value);
        var byPlan = (await grid.ReadAsync<LabelValue>()).ToList();
        var byOrg = (await grid.ReadAsync<LabelValue>()).ToList();
        var employees = await grid.ReadSingleAsync<(int Total, int? Active)>();
        var renewals = (await grid.ReadAsync<(int OrganizationId, string OrganizationName, string PlanName, string Status, DateTime EndDate, bool AutoRenew)>())
            .Select(r => new RenewalRow(r.OrganizationId, r.OrganizationName, r.PlanName, Enum.Parse<SubscriptionStatus>(r.Status), DateOnly.FromDateTime(r.EndDate), r.AutoRenew))
            .ToList();

        var byStatus = Enum.GetValues<OrganizationStatus>().ToDictionary(s => s,
            s => byStatusRaw.FirstOrDefault(r => r.Status == s.ToString()).Cnt);
        var months = Enumerable.Range(0, 12).Select(i => monthStart.AddMonths(i - 11).ToString("yyyy-MM"))
            .Select(m => new LabelValue(m, regs.GetValueOrDefault(m))).ToList();

        return new PlatformDashboard
        {
            TotalOrganizations = byStatus.Values.Sum(),
            ByStatus = byStatus,
            TotalUsers = users.Total,
            ActiveUsers = users.Active ?? 0,
            TotalEmployees = employees.Total,
            ActiveEmployees = employees.Active ?? 0,
            Mrr = Math.Round(mrr, 2),
            NewOrganizationsThisMonth = newThisMonth,
            PaidSubscriptions = subs.GetValueOrDefault("Active") + subs.GetValueOrDefault("PastDue"),
            TrialSubscriptions = subs.GetValueOrDefault("Trial"),
            PastDueSubscriptions = subs.GetValueOrDefault("PastDue"),
            FailedLogins24h = failed,
            RegistrationsByMonth = months,
            OrganizationsByPlan = byPlan,
            UsersByOrganization = byOrg,
            UpcomingRenewals = renewals,
            RecentActivity = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Id).Take(10).ToListAsync(ct)
        };
    }
}
