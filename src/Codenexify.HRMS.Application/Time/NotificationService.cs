using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Time;

public interface INotificationService
{
    /// <summary>Queues notifications in the current unit of work (saved with the business change).</summary>
    void Queue(IEnumerable<int> userIds, string title, string message, string? link);
    /// <summary>Users holding a permission in the current organization (e.g. HR approvers).</summary>
    Task<IReadOnlyList<int>> UsersWithPermissionAsync(string permission, CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task<PagedResult<Notification>> MineAsync(int page, CancellationToken ct = default);
    Task<string?> OpenAsync(int id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}

public sealed class NotificationService(IAppDbContext db, ICurrentContext context, IClock clock) : INotificationService
{
    public void Queue(IEnumerable<int> userIds, string title, string message, string? link)
    {
        foreach (var uid in userIds.Distinct())
            db.Notifications.Add(new Notification { UserId = uid, Title = title, Message = message.Length > 500 ? message[..500] : message, Link = link, EmailPending = true });
    }

    public async Task<IReadOnlyList<int>> UsersWithPermissionAsync(string permission, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.RolePermissions.Any(rp => rp.Permission.Code == permission)))
            .Select(u => u.Id).ToListAsync(ct);

    private int Me => context.UserId ?? throw new TenantIsolationException("No signed-in user.");

    public Task<int> UnreadCountAsync(CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == Me && !n.IsRead, ct);

    public async Task<PagedResult<Notification>> MineAsync(int page, CancellationToken ct = default)
    {
        const int size = 30;
        page = Math.Max(1, page);
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == Me);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<Notification>(items, total, page, size);
    }

    /// <summary>Marks a notification read and returns its link (only for the owner).</summary>
    public async Task<string?> OpenAsync(int id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me, ct);
        if (n is null) return null;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return n.Link;
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var unread = await db.Notifications.Where(n => n.UserId == Me && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) { n.IsRead = true; n.ReadAtUtc = clock.UtcNow; }
        if (unread.Count > 0) await db.SaveChangesAsync(ct);
    }
}
