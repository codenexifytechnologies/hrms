using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class CoreValueInput
{
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [StringLength(500), Display(Name = "What it means here")] public string? Description { get; set; }
    public string Icon { get; set; } = "star";
    [Range(0, 999), Display(Name = "Order")] public int SortOrder { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed class ShoutoutInput
{
    [Display(Name = "To")] public int ToEmployeeId { get; set; }
    [Display(Name = "Value")] public int CoreValueId { get; set; }
    [Required, StringLength(1000, MinimumLength = 5), Display(Name = "What they did")] public string Message { get; set; } = string.Empty;
}

public sealed record CoreValueRow(int Id, string Name, string? Description, string Icon, int SortOrder, bool IsActive, int RecentShoutouts);
public sealed record ShoutoutRow(int Id, int ValueId, string ValueName, string ValueIcon, int FromId, string FromName, int ToId, string ToName, string Message, DateTime AtUtc);
public sealed record ValueCount(string Value, string Icon, int Count);
public sealed record PersonCount(int EmployeeId, string Name, int Count);

public interface ICoreValueService
{
    /// <summary>Allowed icons (Bootstrap Icons names).</summary>
    IReadOnlyList<string> Icons { get; }
    /// <param name="days">Window for the shout-out counts.</param>
    Task<IReadOnlyList<CoreValueRow>> ValuesAsync(bool includeInactive, int days = 90, CancellationToken ct = default);
    Task<CoreValueInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, CoreValueInput input, CancellationToken ct = default);
    Task<Result> ShoutAsync(int me, ShoutoutInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ShoutoutRow>> FeedAsync(int? valueId, int? toEmployeeId, int take = 50, CancellationToken ct = default);
    /// <summary>The author or (with the values permission) HR can remove a shout-out.</summary>
    Task<Result> RemoveAsync(int id, int? me, bool isHr, CancellationToken ct = default);
    Task<IReadOnlyList<PersonCount>> TopRecognisedAsync(int days, int take, CancellationToken ct = default);
    /// <summary>How often the employee was recognised for each value in a period (for reviews).</summary>
    Task<IReadOnlyList<ValueCount>> ForPeriodAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public sealed class CoreValueService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : ICoreValueService
{
    public const int MaxActiveValues = 12;
    public const int MaxShoutoutsPerDay = 20;

    private static readonly string[] AllowedIcons =
        ["star", "heart", "people", "lightning", "shield-check", "lightbulb", "rocket-takeoff", "award", "emoji-smile", "graph-up-arrow", "hand-thumbs-up", "gem", "globe2", "tools"];

    public IReadOnlyList<string> Icons => AllowedIcons;

    public async Task<IReadOnlyList<CoreValueRow>> ValuesAsync(bool includeInactive, int days = 90, CancellationToken ct = default)
    {
        var since = clock.UtcNow.AddDays(-days);
        var counts = await db.ValueShoutouts.AsNoTracking().Where(s => s.AtUtc >= since).GroupBy(s => s.CoreValueId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        return (await db.CoreValues.AsNoTracking().Where(v => includeInactive || v.IsActive).OrderBy(v => v.SortOrder).ThenBy(v => v.Name).ToListAsync(ct))
            .Select(v => new CoreValueRow(v.Id, v.Name, v.Description, v.Icon, v.SortOrder, v.IsActive, counts.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<CoreValueInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var v = await db.CoreValues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? null : new CoreValueInput { Name = v.Name, Description = v.Description, Icon = v.Icon, SortOrder = v.SortOrder, IsActive = v.IsActive };
    }

    public async Task<Result<int>> SaveAsync(int? id, CoreValueInput i, CancellationToken ct = default)
    {
        var name = i.Name.Trim();
        if (!AllowedIcons.Contains(i.Icon)) return Result<int>.Fail("Choose one of the listed icons.");
        if (await db.CoreValues.AnyAsync(v => v.Name == name && v.Id != (id ?? 0), ct)) return Result<int>.Fail("A value with this name already exists.");
        if (i.IsActive && await db.CoreValues.CountAsync(v => v.IsActive && v.Id != (id ?? 0), ct) >= MaxActiveValues)
            return Result<int>.Fail($"Keep it memorable: at most {MaxActiveValues} active values.");
        CoreValue v;
        if (id is int existing) v = await db.CoreValues.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new InvalidOperationException();
        else { v = new CoreValue(); db.CoreValues.Add(v); }
        v.Name = name;
        v.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
        v.Icon = i.Icon;
        v.SortOrder = i.SortOrder;
        v.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(v.Id);
    }

    public async Task<Result> ShoutAsync(int me, ShoutoutInput i, CancellationToken ct = default)
    {
        if (i.ToEmployeeId == me) return Result.Fail("Shout-outs are for colleagues.");
        var to = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == i.ToEmployeeId, ct);
        if (to is not { IsCurrent: true }) return Result.Fail("Choose a current colleague.");
        var value = await db.CoreValues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == i.CoreValueId && v.IsActive, ct);
        if (value is null) return Result.Fail("Choose one of the company's values.");
        var message = (i.Message ?? "").Trim();
        if (message.Length < 5) return Result.Fail("Say what they did.");
        var dayAgo = clock.UtcNow.AddDays(-1);
        if (await db.ValueShoutouts.CountAsync(s => s.FromEmployeeId == me && s.AtUtc >= dayAgo, ct) >= MaxShoutoutsPerDay)
            return Result.Fail("You've sent a lot of shout-outs today. Try again tomorrow.");

        db.ValueShoutouts.Add(new ValueShoutout { CoreValueId = value.Id, FromEmployeeId = me, ToEmployeeId = to.Id, Message = message, AtUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        var from = await db.Employees.Where(e => e.Id == me).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
        // The person and their manager hear about it.
        var users = new List<int>();
        if (to.UserId is int u) users.Add(u);
        if (to.ReportingManagerId is int m && await db.Employees.Where(e => e.Id == m).Select(e => e.UserId).FirstOrDefaultAsync(ct) is int mu && !users.Contains(mu)) users.Add(mu);
        if (users.Count > 0) notifications.Queue(users, $"Shout-out: {value.Name}", $"{from} recognised {to.FullName}.", "/me/values");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ShoutoutRow>> FeedAsync(int? valueId, int? toEmployeeId, int take = 50, CancellationToken ct = default)
    {
        var q = db.ValueShoutouts.AsNoTracking().AsQueryable();
        if (valueId is int v) q = q.Where(s => s.CoreValueId == v);
        if (toEmployeeId is int t) q = q.Where(s => s.ToEmployeeId == t);
        return (await (from s in q
                       join val in db.CoreValues on s.CoreValueId equals val.Id
                       join f in db.Employees on s.FromEmployeeId equals f.Id
                       join r in db.Employees on s.ToEmployeeId equals r.Id
                       orderby s.Id descending
                       select new ShoutoutRow(s.Id, val.Id, val.Name, val.Icon, f.Id, f.FirstName + " " + f.LastName, r.Id, r.FirstName + " " + r.LastName, s.Message, s.AtUtc))
            .Take(Math.Clamp(take, 1, 200)).ToListAsync(ct)).OrderByDescending(s => s.AtUtc).ToList();
    }

    public async Task<Result> RemoveAsync(int id, int? me, bool isHr, CancellationToken ct = default)
    {
        var s = await db.ValueShoutouts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null || (!isHr && s.FromEmployeeId != me)) return Result.Fail("Shout-out not found.");
        db.ValueShoutouts.Remove(s);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<PersonCount>> TopRecognisedAsync(int days, int take, CancellationToken ct = default)
    {
        var since = clock.UtcNow.AddDays(-days);
        var top = await db.ValueShoutouts.AsNoTracking().Where(s => s.AtUtc >= since).GroupBy(s => s.ToEmployeeId)
            .Select(g => new { Id = g.Key, N = g.Count() }).OrderByDescending(x => x.N).Take(take).ToListAsync(ct);
        var ids = top.Select(t => t.Id).ToList();
        var names = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return top.Select(t => new PersonCount(t.Id, names.GetValueOrDefault(t.Id, "—"), t.N)).ToList();
    }

    public async Task<IReadOnlyList<ValueCount>> ForPeriodAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);
        var end = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc).AddDays(1);
        return await (from s in db.ValueShoutouts.AsNoTracking().Where(s => s.ToEmployeeId == employeeId && s.AtUtc >= start && s.AtUtc <= end)
                      join v in db.CoreValues on s.CoreValueId equals v.Id
                      group s by new { v.Name, v.Icon } into g
                      orderby g.Count() descending
                      select new ValueCount(g.Key.Name, g.Key.Icon, g.Count())).ToListAsync(ct);
    }
}
