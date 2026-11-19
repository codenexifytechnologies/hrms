using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class OneOnOneInput
{
    [Display(Name = "With")] public int OtherEmployeeId { get; set; }
    [Display(Name = "Date")] public DateOnly OnDate { get; set; }
    [Display(Name = "Time")] public TimeOnly StartsAt { get; set; } = new(10, 0);
    [Range(10, 240), Display(Name = "Minutes")] public int DurationMinutes { get; set; } = 30;
    public MeetingRepeat Repeat { get; set; } = MeetingRepeat.None;
    [StringLength(500), Display(Name = "First agenda topic (optional)")] public string? FirstTopic { get; set; }
}

public sealed record OneOnOneRow(int Id, int ManagerId, string ManagerName, int EmployeeId, string EmployeeName, DateOnly OnDate, TimeOnly StartsAt, int DurationMinutes,
    MeetingRepeat Repeat, MeetingStatus Status, int OpenItems);
public sealed record MeetingItemView(MeetingItem Item, string AddedByName, string? OwnerName, DateOnly MeetingDate);
public sealed record OneOnOneView(OneOnOne Meeting, string ManagerName, string EmployeeName, bool IAmManager, IReadOnlyList<MeetingItemView> Agenda,
    IReadOnlyList<MeetingItemView> Actions, IReadOnlyList<MeetingItemView> CarriedOver, string? MyPrivateNotes, int? PreviousId, int? NextId)
{
    public bool IsOpen => Meeting.Status == MeetingStatus.Scheduled;
}

public interface IOneOnOneService
{
    /// <summary>People I can hold 1:1s with: everyone in my reporting line, plus my managers.</summary>
    Task<IReadOnlyList<LookupItem>> PartnersAsync(int me, CancellationToken ct = default);
    Task<Result<int>> ScheduleAsync(int me, OneOnOneInput input, CancellationToken ct = default);
    Task<IReadOnlyList<OneOnOneRow>> MineAsync(int me, CancellationToken ct = default);
    Task<OneOnOneView?> GetAsync(int id, int me, CancellationToken ct = default);
    Task<Result> SaveNotesAsync(int id, int me, string? shared, string? myPrivate, CancellationToken ct = default);
    Task<Result> AddItemAsync(int id, int me, MeetingItemKind kind, string text, int? ownerId, DateOnly? dueOn, CancellationToken ct = default);
    Task<Result<int>> ToggleItemAsync(int itemId, int me, CancellationToken ct = default);
    Task<Result<int>> RemoveItemAsync(int itemId, int me, CancellationToken ct = default);
    Task<Result> RescheduleAsync(int id, int me, DateOnly onDate, TimeOnly startsAt, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, int me, CancellationToken ct = default);
    /// <summary>Marks the meeting held; for a repeating 1:1 the next one is scheduled. Returns the next meeting's id, if any.</summary>
    Task<Result<int?>> CompleteAsync(int id, int me, CancellationToken ct = default);
}

public sealed class OneOnOneService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IOneOnOneService
{
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Employees.AsNoTracking().Where(e => list.Contains(e.Id)).Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
    }

    public static DateOnly Next(DateOnly from, MeetingRepeat repeat) => repeat switch
    {
        MeetingRepeat.Weekly => from.AddDays(7),
        MeetingRepeat.Fortnightly => from.AddDays(14),
        MeetingRepeat.Monthly => from.AddMonths(1),
        _ => from
    };

    public async Task<IReadOnlyList<LookupItem>> PartnersAsync(int me, CancellationToken ct = default)
    {
        var line = await ReportingLine.LoadAsync(db, ct);
        var ids = line.ReportsOf(me).Concat(line.ManagersOf(me)).ToHashSet();
        return await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id) && e.Status != Domain.CoreHr.EmployeeStatus.Exited && e.Status != Domain.CoreHr.EmployeeStatus.Inactive)
            .OrderBy(e => e.FirstName).Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName)).ToListAsync(ct);
    }

    public async Task<Result<int>> ScheduleAsync(int me, OneOnOneInput i, CancellationToken ct = default)
    {
        var line = await ReportingLine.LoadAsync(db, ct);
        int manager, employee;
        if (line.IsAbove(me, i.OtherEmployeeId)) (manager, employee) = (me, i.OtherEmployeeId);
        else if (line.IsAbove(i.OtherEmployeeId, me)) (manager, employee) = (i.OtherEmployeeId, me);
        else return Result<int>.Fail("1:1s are between a manager and someone in their reporting line.");
        var other = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == i.OtherEmployeeId, ct);
        if (other is not { IsCurrent: true }) return Result<int>.Fail("That person is no longer with the company.");
        var today = await TodayAsync(ct);
        if (i.OnDate < today) return Result<int>.Fail("Choose today or a later date.");
        if (i.OnDate > today.AddYears(1)) return Result<int>.Fail("Schedule at most a year ahead.");
        if (await db.OneOnOnes.AnyAsync(m => m.ManagerEmployeeId == manager && m.EmployeeId == employee && m.OnDate == i.OnDate && m.Status == MeetingStatus.Scheduled, ct))
            return Result<int>.Fail("There's already a 1:1 between you two on that day.");

        var meeting = new OneOnOne { ManagerEmployeeId = manager, EmployeeId = employee, OnDate = i.OnDate, StartsAt = i.StartsAt, DurationMinutes = i.DurationMinutes, Repeat = i.Repeat };
        if (!string.IsNullOrWhiteSpace(i.FirstTopic))
            meeting.Items.Add(new MeetingItem { Kind = MeetingItemKind.Agenda, Text = i.FirstTopic.Trim(), AddedByEmployeeId = me });
        db.OneOnOnes.Add(meeting);
        await db.SaveChangesAsync(ct);

        var myName = (await NamesAsync([me], ct)).GetValueOrDefault(me, "Your colleague");
        if (other.UserId is int u) { notifications.Queue([u], "1:1 scheduled", $"{myName}: {i.OnDate:ddd dd MMM} at {i.StartsAt:HH\\:mm}.", $"/me/one-on-ones/{meeting.Id}"); await db.SaveChangesAsync(ct); }
        return Result<int>.Ok(meeting.Id);
    }

    public async Task<IReadOnlyList<OneOnOneRow>> MineAsync(int me, CancellationToken ct = default)
    {
        var meetings = await db.OneOnOnes.AsNoTracking().Where(m => m.ManagerEmployeeId == me || m.EmployeeId == me)
            .OrderByDescending(m => m.OnDate).Take(200)
            .Select(m => new { m.Id, m.ManagerEmployeeId, m.EmployeeId, m.OnDate, m.StartsAt, m.DurationMinutes, m.Repeat, m.Status, Open = m.Items.Count(x => !x.IsDone) })
            .ToListAsync(ct);
        var names = await NamesAsync(meetings.SelectMany(m => new[] { m.ManagerEmployeeId, m.EmployeeId }), ct);
        return meetings.Select(m => new OneOnOneRow(m.Id, m.ManagerEmployeeId, names.GetValueOrDefault(m.ManagerEmployeeId, "—"), m.EmployeeId, names.GetValueOrDefault(m.EmployeeId, "—"),
            m.OnDate, m.StartsAt, m.DurationMinutes, m.Repeat, m.Status, m.Open)).ToList();
    }

    private async Task<OneOnOne?> MineOrNullAsync(int id, int me, bool track, CancellationToken ct)
    {
        var q = db.OneOnOnes.Include(m => m.Items).Where(m => m.Id == id && (m.ManagerEmployeeId == me || m.EmployeeId == me));
        return await (track ? q : q.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    public async Task<OneOnOneView?> GetAsync(int id, int me, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, false, ct);
        if (m is null) return null;
        // Open action items from this pair's earlier meetings carry over.
        var earlier = await db.MeetingItems.AsNoTracking()
            .Join(db.OneOnOnes.Where(o => o.ManagerEmployeeId == m.ManagerEmployeeId && o.EmployeeId == m.EmployeeId && o.OnDate < m.OnDate && o.Id != m.Id),
                it => it.OneOnOneId, o => o.Id, (it, o) => new { Item = it, o.OnDate })
            .Where(x => x.Item.Kind == MeetingItemKind.Action && !x.Item.IsDone).ToListAsync(ct);
        var items = m.Items.Where(x => !x.IsDeleted).ToList();
        var names = await NamesAsync(items.Concat(earlier.Select(e => e.Item)).SelectMany(x => new[] { x.AddedByEmployeeId, x.OwnerEmployeeId ?? 0 })
            .Concat([m.ManagerEmployeeId, m.EmployeeId]), ct);
        MeetingItemView V(MeetingItem x, DateOnly d) => new(x, names.GetValueOrDefault(x.AddedByEmployeeId, "—"), x.OwnerEmployeeId is int o ? names.GetValueOrDefault(o) : null, d);

        var siblings = await db.OneOnOnes.AsNoTracking().Where(o => o.ManagerEmployeeId == m.ManagerEmployeeId && o.EmployeeId == m.EmployeeId && o.Status != MeetingStatus.Cancelled && o.Id != m.Id)
            .Select(o => new { o.Id, o.OnDate }).ToListAsync(ct);
        var iAmManager = m.ManagerEmployeeId == me;
        return new OneOnOneView(m, names[m.ManagerEmployeeId], names[m.EmployeeId], iAmManager,
            items.Where(x => x.Kind == MeetingItemKind.Agenda).OrderBy(x => x.Id).Select(x => V(x, m.OnDate)).ToList(),
            items.Where(x => x.Kind == MeetingItemKind.Action).OrderBy(x => x.IsDone).ThenBy(x => x.DueOn ?? DateOnly.MaxValue).Select(x => V(x, m.OnDate)).ToList(),
            earlier.OrderBy(e => e.OnDate).Select(e => V(e.Item, e.OnDate)).ToList(),
            iAmManager ? m.ManagerPrivateNotes : m.EmployeePrivateNotes,
            siblings.Where(s => s.OnDate < m.OnDate).OrderByDescending(s => s.OnDate).Select(s => (int?)s.Id).FirstOrDefault(),
            siblings.Where(s => s.OnDate > m.OnDate).OrderBy(s => s.OnDate).Select(s => (int?)s.Id).FirstOrDefault());
    }

    private static string? Clip(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim() is var t && t.Length > max ? t[..max] : s.Trim();

    public async Task<Result> SaveNotesAsync(int id, int me, string? shared, string? myPrivate, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, true, ct);
        if (m is null) return Result.Fail("1:1 not found.");
        if (m.Status == MeetingStatus.Cancelled) return Result.Fail("This 1:1 was cancelled.");
        m.SharedNotes = Clip(shared, 8000);
        if (m.ManagerEmployeeId == me) m.ManagerPrivateNotes = Clip(myPrivate, 4000);
        else m.EmployeePrivateNotes = Clip(myPrivate, 4000);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddItemAsync(int id, int me, MeetingItemKind kind, string text, int? ownerId, DateOnly? dueOn, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, true, ct);
        if (m is null) return Result.Fail("1:1 not found.");
        if (m.Status == MeetingStatus.Cancelled) return Result.Fail("This 1:1 was cancelled.");
        if (string.IsNullOrWhiteSpace(text)) return Result.Fail("Write the item first.");
        if (m.Items.Count(x => !x.IsDeleted) >= 50) return Result.Fail("A 1:1 can have at most 50 items.");
        if (kind == MeetingItemKind.Action && ownerId is int o && o != m.ManagerEmployeeId && o != m.EmployeeId) return Result.Fail("An action item belongs to one of the two of you.");
        m.Items.Add(new MeetingItem
        {
            Kind = kind, Text = Clip(text, 500)!, AddedByEmployeeId = me,
            OwnerEmployeeId = kind == MeetingItemKind.Action ? ownerId ?? me : null, DueOn = kind == MeetingItemKind.Action ? dueOn : null
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>Items can be ticked in any meeting of the pair (carried-over actions are ticked from the newer meeting).</summary>
    private async Task<(MeetingItem Item, OneOnOne Meeting)?> ItemAsync(int itemId, int me, CancellationToken ct)
    {
        var item = await db.MeetingItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return null;
        var m = await db.OneOnOnes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == item.OneOnOneId && (o.ManagerEmployeeId == me || o.EmployeeId == me), ct);
        return m is null ? null : (item, m);
    }

    public async Task<Result<int>> ToggleItemAsync(int itemId, int me, CancellationToken ct = default)
    {
        if (await ItemAsync(itemId, me, ct) is not { } found) return Result<int>.Fail("Item not found.");
        found.Item.IsDone = !found.Item.IsDone;
        found.Item.DoneAtUtc = found.Item.IsDone ? clock.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(found.Meeting.Id);
    }

    public async Task<Result<int>> RemoveItemAsync(int itemId, int me, CancellationToken ct = default)
    {
        if (await ItemAsync(itemId, me, ct) is not { } found) return Result<int>.Fail("Item not found.");
        if (found.Item.AddedByEmployeeId != me) return Result<int>.Fail("Only the person who added an item can remove it.");
        if (found.Meeting.Status == MeetingStatus.Completed) return Result<int>.Fail("Items of a held 1:1 are kept as a record; tick them done instead.");
        db.MeetingItems.Remove(found.Item);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(found.Meeting.Id);
    }

    public async Task<Result> RescheduleAsync(int id, int me, DateOnly onDate, TimeOnly startsAt, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, true, ct);
        if (m is null || m.Status != MeetingStatus.Scheduled) return Result.Fail("Only an upcoming 1:1 can be moved.");
        if (onDate < await TodayAsync(ct)) return Result.Fail("Choose today or a later date.");
        m.OnDate = onDate;
        m.StartsAt = startsAt;
        await db.SaveChangesAsync(ct);
        await NotifyOtherAsync(m, me, "1:1 moved", $"Now {onDate:ddd dd MMM} at {startsAt:HH\\:mm}.", ct);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int id, int me, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, true, ct);
        if (m is null || m.Status != MeetingStatus.Scheduled) return Result.Fail("Only an upcoming 1:1 can be cancelled.");
        m.Status = MeetingStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await NotifyOtherAsync(m, me, "1:1 cancelled", $"The 1:1 on {m.OnDate:ddd dd MMM} was cancelled.", ct);
        return Result.Ok();
    }

    public async Task<Result<int?>> CompleteAsync(int id, int me, CancellationToken ct = default)
    {
        var m = await MineOrNullAsync(id, me, true, ct);
        if (m is null || m.Status != MeetingStatus.Scheduled) return Result<int?>.Fail("This 1:1 isn't open.");
        if (m.OnDate > await TodayAsync(ct)) return Result<int?>.Fail("A 1:1 can be marked held on or after its date.");
        m.Status = MeetingStatus.Completed;
        m.CompletedAtUtc = clock.UtcNow;
        OneOnOne? next = null;
        if (m.Repeat != MeetingRepeat.None)
        {
            var date = Next(m.OnDate, m.Repeat);
            var already = await db.OneOnOnes.AnyAsync(o => o.ManagerEmployeeId == m.ManagerEmployeeId && o.EmployeeId == m.EmployeeId && o.Status == MeetingStatus.Scheduled && o.OnDate > m.OnDate, ct);
            if (!already)
            {
                next = new OneOnOne { ManagerEmployeeId = m.ManagerEmployeeId, EmployeeId = m.EmployeeId, OnDate = date, StartsAt = m.StartsAt, DurationMinutes = m.DurationMinutes, Repeat = m.Repeat };
                db.OneOnOnes.Add(next);
            }
        }
        await db.SaveChangesAsync(ct);
        if (next is not null) await NotifyOtherAsync(next, me, "Next 1:1 scheduled", $"{next.OnDate:ddd dd MMM} at {next.StartsAt:HH\\:mm}.", ct);
        return Result<int?>.Ok(next?.Id);
    }

    private async Task NotifyOtherAsync(OneOnOne m, int me, string title, string body, CancellationToken ct)
    {
        var other = m.ManagerEmployeeId == me ? m.EmployeeId : m.ManagerEmployeeId;
        var user = await db.Employees.Where(e => e.Id == other).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (user is int u) { notifications.Queue([u], title, body, $"/me/one-on-ones/{m.Id}"); await db.SaveChangesAsync(ct); }
    }
}
