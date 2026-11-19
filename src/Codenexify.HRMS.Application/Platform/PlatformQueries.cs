using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Audit;
using Codenexify.HRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Platform;

public sealed record LabelValue(string Label, decimal Value);

public sealed record RenewalRow(int OrganizationId, string OrganizationName, string PlanName, SubscriptionStatus Status, DateOnly EndDate, bool AutoRenew);

public sealed class PlatformDashboard
{
    public int TotalOrganizations { get; init; }
    public IReadOnlyDictionary<OrganizationStatus, int> ByStatus { get; init; } = new Dictionary<OrganizationStatus, int>();
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int TotalEmployees { get; init; }
    public int ActiveEmployees { get; init; }
    public decimal Mrr { get; init; }
    public decimal Arr => Mrr * 12;
    public int NewOrganizationsThisMonth { get; init; }
    public int PaidSubscriptions { get; init; }
    public int TrialSubscriptions { get; init; }
    public int PastDueSubscriptions { get; init; }
    public int FailedLogins24h { get; init; }
    public IReadOnlyList<LabelValue> RegistrationsByMonth { get; init; } = [];
    public IReadOnlyList<LabelValue> OrganizationsByPlan { get; init; } = [];
    public IReadOnlyList<LabelValue> UsersByOrganization { get; init; } = [];
    public IReadOnlyList<RenewalRow> UpcomingRenewals { get; init; } = [];
    public IReadOnlyList<AuditLog> RecentActivity { get; init; } = [];
}

/// <summary>Aggregated platform metrics (implemented with Dapper for efficient SQL aggregation).</summary>
public interface IPlatformDashboardService
{
    Task<PlatformDashboard> GetAsync(CancellationToken ct = default);
}

public sealed class AuditQuery
{
    public string? Search { get; set; }
    public int? OrganizationId { get; set; }
    public PrincipalType? ActorType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLog>> SearchAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed class AuditQueryService(IAppDbContext db) : IAuditQueryService
{
    public async Task<PagedResult<AuditLog>> SearchAsync(AuditQuery q, CancellationToken ct = default)
    {
        const int size = 50;
        var page = Math.Max(1, q.Page);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (q.OrganizationId is { } org) query = query.Where(a => a.OrganizationId == org);
        if (q.ActorType is { } t) query = query.Where(a => a.ActorType == t);
        if (q.From is { } from) { var f = from.ToDateTime(TimeOnly.MinValue); query = query.Where(a => a.TimestampUtc >= f); }
        if (q.To is { } to) { var t2 = to.AddDays(1).ToDateTime(TimeOnly.MinValue); query = query.Where(a => a.TimestampUtc < t2); }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(a => a.Action.Contains(s) || a.EntityType.Contains(s) || (a.ActorName != null && a.ActorName.Contains(s)) || (a.Details != null && a.Details.Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, page, size);
    }
}
