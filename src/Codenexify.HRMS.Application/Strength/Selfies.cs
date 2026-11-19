using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

public sealed record SelfiePunchRow(int PunchId, DateTime LocalTime, int EmployeeId, string EmployeeName, string EmployeeCode, PunchDirection Direction);
public sealed record SelfieSettings(bool Required, int RetentionDays);
public sealed record SelfieFile(Stream Content);

public interface ISelfieService
{
    Task<SelfieSettings> SettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(bool required, int retentionDays, CancellationToken ct = default);
    Task<IReadOnlyList<SelfiePunchRow>> ForDayAsync(DateOnly date, CancellationToken ct = default);
    Task<SelfieFile?> OpenAsync(int punchId, CancellationToken ct = default);
    /// <summary>Deletes photos older than the organization's retention period. Returns how many were removed.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken ct = default);
}

public sealed class SelfieService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage, IAuditWriter audit) : ISelfieService
{
    public async Task<SelfieSettings> SettingsAsync(CancellationToken ct = default) =>
        await db.OrganizationSettings.AsNoTracking().Select(s => new SelfieSettings(s.SelfieRequired, s.SelfieRetentionDays)).FirstOrDefaultAsync(ct) ?? new SelfieSettings(false, 90);

    public async Task<Result> SaveSettingsAsync(bool required, int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays is < 7 or > 365) return Result.Fail("Keep photos for between 7 and 365 days.");
        var s = await db.OrganizationSettings.FirstOrDefaultAsync(ct);
        if (s is null) return Result.Fail("Organization settings not found.");
        s.SelfieRequired = required;
        s.SelfieRetentionDays = retentionDays;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SelfiePunchRow>> ForDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var org = await db.OrgClockAsync(context.OrgId(), clock, ct);
        var rows = await db.AttendancePunches.AsNoTracking().Where(p => p.AttendanceDate == date && p.SelfieStorageKey != null)
            .Join(db.Employees, p => p.EmployeeId, e => e.Id, (p, e) => new { PunchId = p.Id, p.TimestampUtc, EmployeeId = e.Id, e.FirstName, e.LastName, e.EmployeeCode, p.Direction })
            .OrderBy(x => x.FirstName).ThenBy(x => x.TimestampUtc).Take(500).ToListAsync(ct);
        return rows.Select(r => new SelfiePunchRow(r.PunchId, org.ToLocal(r.TimestampUtc), r.EmployeeId, $"{r.FirstName} {r.LastName}", r.EmployeeCode, r.Direction)).ToList();
    }

    public async Task<SelfieFile?> OpenAsync(int punchId, CancellationToken ct = default)
    {
        var key = await db.AttendancePunches.AsNoTracking().Where(p => p.Id == punchId).Select(p => p.SelfieStorageKey).FirstOrDefaultAsync(ct);
        if (key is null) return null;
        var stream = await storage.OpenReadAsync(key, ct);
        return stream is null ? null : new SelfieFile(stream);
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct = default)
    {
        var days = await db.OrganizationSettings.AsNoTracking().Select(s => s.SelfieRetentionDays).FirstOrDefaultAsync(ct);
        if (days <= 0) days = 90;
        var cutoff = clock.UtcNow.AddDays(-days);
        var old = await db.AttendancePunches.Where(p => p.SelfieStorageKey != null && p.TimestampUtc < cutoff).Take(1000).ToListAsync(ct);
        foreach (var p in old)
        {
            await storage.DeleteAsync(p.SelfieStorageKey!, ct);
            p.SelfieStorageKey = null;
        }
        if (old.Count > 0)
        {
            audit.Add("Attendance.SelfiesPurged", "AttendancePunch", null, $"{old.Count} clock-in photo(s) older than {days} days deleted.");
            await db.SaveChangesAsync(ct);
        }
        return old.Count;
    }
}
