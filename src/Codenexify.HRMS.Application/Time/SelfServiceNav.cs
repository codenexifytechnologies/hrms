using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Engagement;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

public sealed record SelfServiceInfo(
    int OrganizationId, string OrganizationName, string BrandColor, OrganizationStatus OrganizationStatus,
    int? EmployeeId, bool IsManager, int PendingApprovals, int UnreadNotifications, int PendingProfileChanges);

/// <summary>Small per-request facts used to build the portal layout (header, menu and badges).</summary>
public interface ISelfServiceNav
{
    /// <param name="countProfileChanges">Also count pending profile-change requests (only for HR who can act on them).</param>
    Task<SelfServiceInfo> GetAsync(bool countProfileChanges = false, CancellationToken ct = default);
}

public sealed class SelfServiceNav(IAppDbContext db, ICurrentContext context) : ISelfServiceNav
{
    private SelfServiceInfo? _cached;

    /// <summary>
    /// Everything the layout needs in ONE database round trip. It runs on every page, so separate queries here
    /// (organization, employee, manager check and four badge counts) made every click noticeably slower on a remote database.
    /// </summary>
    public async Task<SelfServiceInfo> GetAsync(bool countProfileChanges = false, CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;
        var orgId = context.OrganizationId ?? throw new InvalidOperationException("No organization in the current context.");
        var uid = context.UserId ?? 0; // 0 matches nobody

        var row = await db.Organizations.AsNoTracking().Where(o => o.Id == orgId).Select(o => new
        {
            o.Id, o.Name, o.BrandColor, o.Status,
            EmployeeId = db.Employees.Where(e => e.UserId == uid).Select(e => (int?)e.Id).FirstOrDefault(),
            IsManager = db.Employees.Any(r => r.Status != EmployeeStatus.Exited && r.ReportingManager != null && r.ReportingManager.UserId == uid),
            Leave = db.LeaveApplications.Count(a => a.ApproverUserId == uid && a.Status == LeaveStatus.Pending),
            Overtime = db.OvertimeRequests.Count(x => x.ApproverUserId == uid && x.Status == OvertimeStatus.Pending),
            Unread = db.Notifications.Count(n => n.UserId == uid && !n.IsRead),
            ProfileChanges = countProfileChanges ? db.ProfileChangeRequests.Count(r => r.Status == ProfileChangeStatus.Pending) : 0
        }).FirstAsync(ct);

        return _cached = new SelfServiceInfo(row.Id, row.Name, row.BrandColor, row.Status,
            row.EmployeeId, row.IsManager, row.Leave + row.Overtime, row.Unread, row.ProfileChanges);
    }
}
