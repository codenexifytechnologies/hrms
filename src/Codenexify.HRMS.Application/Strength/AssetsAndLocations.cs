using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

// ====================================================================== assets

public sealed class AssetInput
{
    [Required, StringLength(40), Display(Name = "Asset tag")] public string AssetTag { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(60)] public string Category { get; set; } = "Laptop";
    [StringLength(100), Display(Name = "Serial number")] public string? SerialNumber { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Purchased on")] public DateOnly? PurchaseDate { get; set; }
    [Range(0, 100_000_000), Display(Name = "Purchase cost (₹)")] public decimal? PurchaseCost { get; set; }
    [Display(Name = "Warranty until")] public DateOnly? WarrantyUntil { get; set; }
    public AssetCondition Condition { get; set; } = AssetCondition.New;
}

public sealed record AssetRow(int Id, string AssetTag, string Name, string Category, string? SerialNumber, AssetStatus Status, AssetCondition Condition,
    int? EmployeeId, string? EmployeeName, DateOnly? AssignedOn, DateOnly? WarrantyUntil, bool Acknowledged);
public sealed record AssetHistoryRow(int AssignmentId, int EmployeeId, string EmployeeName, DateOnly AssignedOn, DateOnly? ReturnedOn, AssetCondition ConditionOut,
    AssetCondition? ConditionIn, string AssignedBy, string? ReturnedTo, DateTime? AcknowledgedAtUtc, string? Notes);
public sealed record MyAssetRow(int AssignmentId, string AssetTag, string Name, string Category, string? SerialNumber, DateOnly AssignedOn, AssetCondition Condition, bool Acknowledged);

public sealed class AssetQuery
{
    public AssetStatus? Status { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
}

public interface IAssetService
{
    Task<PagedResult<AssetRow>> ListAsync(AssetQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default);
    Task<AssetInput?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<AssetHistoryRow>> HistoryAsync(int assetId, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, AssetInput input, CancellationToken ct = default);
    Task<Result> AssignAsync(int assetId, int employeeId, DateOnly? expectedReturn, string? notes, CancellationToken ct = default);
    Task<Result> ReturnAsync(int assetId, AssetCondition condition, string? notes, CancellationToken ct = default);
    Task<Result> SetStatusAsync(int assetId, AssetStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<MyAssetRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default);
    Task<Result> AcknowledgeAsync(int assignmentId, int employeeId, CancellationToken ct = default);
}

public sealed class AssetService(IAppDbContext db, ICurrentContext context, IClock clock, Time.INotificationService notifications) : IAssetService
{
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    public async Task<PagedResult<AssetRow>> ListAsync(AssetQuery q, CancellationToken ct = default)
    {
        const int size = 30;
        var page = Math.Max(1, q.Page);
        var query = db.Assets.AsNoTracking();
        if (q.Status is { } s) query = query.Where(a => a.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Category)) query = query.Where(a => a.Category == q.Category);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Trim();
            query = query.Where(a => a.AssetTag.Contains(t) || a.Name.Contains(t) || (a.SerialNumber != null && a.SerialNumber.Contains(t))
                || db.Employees.Any(e => e.Id == a.CurrentEmployeeId && (e.FirstName + " " + e.LastName).Contains(t)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(a => a.AssetTag).Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                a.Id, a.AssetTag, a.Name, a.Category, a.SerialNumber, a.Status, a.Condition, a.CurrentEmployeeId, a.WarrantyUntil,
                Employee = db.Employees.Where(e => e.Id == a.CurrentEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                Open = db.AssetAssignments.Where(x => x.AssetId == a.Id && x.ReturnedOn == null).Select(x => new { x.AssignedOn, x.AcknowledgedAtUtc }).FirstOrDefault()
            }).ToListAsync(ct);
        return new PagedResult<AssetRow>(rows.Select(r => new AssetRow(r.Id, r.AssetTag, r.Name, r.Category, r.SerialNumber, r.Status, r.Condition,
            r.CurrentEmployeeId, r.Employee, r.Open?.AssignedOn, r.WarrantyUntil, r.Open?.AcknowledgedAtUtc is not null)).ToList(), total, page, size);
    }

    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default)
    {
        var used = await db.Assets.AsNoTracking().Select(a => a.Category).Distinct().ToListAsync(ct);
        return used.Union(["Laptop", "Desktop", "Monitor", "Mobile phone", "SIM card", "Access card", "Headset", "Vehicle", "Furniture", "Other"])
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
    }

    public async Task<AssetInput?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Assets.AsNoTracking().Where(a => a.Id == id).Select(a => new AssetInput
        {
            AssetTag = a.AssetTag, Name = a.Name, Category = a.Category, SerialNumber = a.SerialNumber, Description = a.Description,
            PurchaseDate = a.PurchaseDate, PurchaseCost = a.PurchaseCost, WarrantyUntil = a.WarrantyUntil, Condition = a.Condition
        }).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AssetHistoryRow>> HistoryAsync(int assetId, CancellationToken ct = default) =>
        await db.AssetAssignments.AsNoTracking().Where(x => x.AssetId == assetId).OrderByDescending(x => x.Id)
            .Join(db.Employees, x => x.EmployeeId, e => e.Id, (x, e) => new AssetHistoryRow(x.Id, x.EmployeeId, e.FirstName + " " + e.LastName, x.AssignedOn,
                x.ReturnedOn, x.ConditionOut, x.ConditionIn, x.AssignedBy, x.ReturnedTo, x.AcknowledgedAtUtc, x.Notes)).ToListAsync(ct);

    public async Task<Result<int>> SaveAsync(int? id, AssetInput i, CancellationToken ct = default)
    {
        var tag = i.AssetTag.Trim().ToUpperInvariant();
        if (await db.Assets.AnyAsync(a => a.AssetTag == tag && a.Id != id, ct)) return Result<int>.Fail($"Asset tag {tag} is already used.");
        if (i.WarrantyUntil is { } w && i.PurchaseDate is { } p && w < p) return Result<int>.Fail("The warranty can't end before the purchase date.");
        var a = id is null ? db.Assets.Add(new Asset()).Entity : await db.Assets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result<int>.Fail("Asset not found.");
        a.AssetTag = tag; a.Name = i.Name.Trim(); a.Category = i.Category.Trim(); a.SerialNumber = i.SerialNumber?.Trim(); a.Description = i.Description?.Trim();
        a.PurchaseDate = i.PurchaseDate; a.PurchaseCost = i.PurchaseCost; a.WarrantyUntil = i.WarrantyUntil; a.Condition = i.Condition;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(a.Id);
    }

    public async Task<Result> AssignAsync(int assetId, int employeeId, DateOnly? expectedReturn, string? notes, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Asset not found.");
        if (asset.Status != AssetStatus.Available) return Result.Fail($"The asset is {asset.Status.ToString().ToLowerInvariant()}; only available assets can be assigned.");
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null || !emp.IsCurrent) return Result.Fail("Choose a current employee.");
        var today = await TodayAsync(ct);
        if (expectedReturn is { } r && r < today) return Result.Fail("The expected return date is in the past.");
        db.AssetAssignments.Add(new AssetAssignment
        {
            AssetId = assetId, EmployeeId = employeeId, AssignedOn = today, ExpectedReturnOn = expectedReturn, ConditionOut = asset.Condition,
            AssignedBy = await MyNameAsync(ct), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
        asset.Status = AssetStatus.Assigned;
        asset.CurrentEmployeeId = employeeId;
        if (emp.UserId is int uid) notifications.Queue([uid], "Asset assigned to you", $"{asset.Name} ({asset.AssetTag}). Please confirm you received it.", "/me/assets");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail("The asset changed meanwhile. Reload and try again."); }
        return Result.Ok();
    }

    public async Task<Result> ReturnAsync(int assetId, AssetCondition condition, string? notes, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Asset not found.");
        var open = await db.AssetAssignments.FirstOrDefaultAsync(x => x.AssetId == assetId && x.ReturnedOn == null, ct);
        if (asset.Status != AssetStatus.Assigned || open is null) return Result.Fail("The asset is not assigned to anyone.");
        open.ReturnedOn = await TodayAsync(ct);
        open.ConditionIn = condition;
        open.ReturnedTo = await MyNameAsync(ct);
        if (!string.IsNullOrWhiteSpace(notes)) open.Notes = (open.Notes is null ? "" : open.Notes + " · ") + notes.Trim();
        asset.Status = condition == AssetCondition.Damaged ? AssetStatus.UnderRepair : AssetStatus.Available;
        asset.Condition = condition;
        asset.CurrentEmployeeId = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetStatusAsync(int assetId, AssetStatus status, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Asset not found.");
        if (status == AssetStatus.Assigned) return Result.Fail("Use Assign to give the asset to someone.");
        if (asset.Status == AssetStatus.Assigned && status != AssetStatus.Lost) return Result.Fail("Take the asset back first.");
        if (asset.Status == AssetStatus.Assigned && status == AssetStatus.Lost)
        {
            var open = await db.AssetAssignments.FirstOrDefaultAsync(x => x.AssetId == assetId && x.ReturnedOn == null, ct);
            if (open is not null) { open.Notes = (open.Notes is null ? "" : open.Notes + " · ") + "Reported lost"; open.ReturnedOn = await TodayAsync(ct); }
            asset.CurrentEmployeeId = null;
        }
        asset.Status = status;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<MyAssetRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        await db.AssetAssignments.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.ReturnedOn == null).OrderBy(x => x.AssignedOn)
            .Select(x => new MyAssetRow(x.Id, x.Asset.AssetTag, x.Asset.Name, x.Asset.Category, x.Asset.SerialNumber, x.AssignedOn, x.ConditionOut, x.AcknowledgedAtUtc != null))
            .ToListAsync(ct);

    public async Task<Result> AcknowledgeAsync(int assignmentId, int employeeId, CancellationToken ct = default)
    {
        var x = await db.AssetAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.EmployeeId == employeeId && a.ReturnedOn == null, ct);
        if (x is null) return Result.Fail("Assignment not found.");
        x.AcknowledgedAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ====================================================================== office locations & geo settings

public sealed class OfficeLocationInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    [Range(-90, 90)] public decimal Latitude { get; set; }
    [Range(-180, 180)] public decimal Longitude { get; set; }
    [Range(30, 5000), Display(Name = "Radius (metres)")] public int RadiusMeters { get; set; } = 200;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed record OfficeLocationRow(int Id, string Name, int? BranchId, string? Branch, decimal Latitude, decimal Longitude, int RadiusMeters, bool IsActive);
public sealed record GeoPunchRow(DateTime LocalTime, string EmployeeName, string EmployeeCode, string Direction, PunchLocationStatus Status, string? Office, int? DistanceMeters,
    int? AccuracyMeters, decimal? Latitude, decimal? Longitude);

public interface IOfficeLocationService
{
    Task<IReadOnlyList<OfficeLocationRow>> ListAsync(CancellationToken ct = default);
    Task<Result> SaveAsync(int? id, OfficeLocationInput input, bool multipleAllowed, CancellationToken ct = default);
    Task<GeoAttendanceMode> ModeAsync(CancellationToken ct = default);
    Task<Result> SetModeAsync(GeoAttendanceMode mode, CancellationToken ct = default);
    Task<Result> SetExemptAsync(int employeeId, bool exempt, CancellationToken ct = default);
    Task<IReadOnlyList<CoreHr.LookupItem>> ExemptEmployeesAsync(CancellationToken ct = default);
    /// <summary>Recent located punches for HR review (outside-the-fence first).</summary>
    Task<IReadOnlyList<GeoPunchRow>> RecentPunchesAsync(DateOnly date, CancellationToken ct = default);
}

public sealed class OfficeLocationService(IAppDbContext db, ICurrentContext context, IClock clock, IAuditWriter audit) : IOfficeLocationService
{
    public async Task<IReadOnlyList<OfficeLocationRow>> ListAsync(CancellationToken ct = default) =>
        await db.OfficeLocations.AsNoTracking().OrderByDescending(o => o.IsActive).ThenBy(o => o.Name)
            .Select(o => new OfficeLocationRow(o.Id, o.Name, o.BranchId, db.Branches.Where(b => b.Id == o.BranchId).Select(b => b.Name).FirstOrDefault(),
                o.Latitude, o.Longitude, o.RadiusMeters, o.IsActive)).ToListAsync(ct);

    public async Task<Result> SaveAsync(int? id, OfficeLocationInput i, bool multipleAllowed, CancellationToken ct = default)
    {
        if (!GeoFence.IsValid(new GeoPoint(i.Latitude, i.Longitude, null))) return Result.Fail("Enter valid coordinates.");
        if (i.BranchId is int b && !await db.Branches.AnyAsync(x => x.Id == b, ct)) return Result.Fail("Branch not found.");
        if (i.IsActive && !multipleAllowed && await db.OfficeLocations.AnyAsync(o => o.IsActive && o.Id != id, ct))
            return Result.Fail("Your plan includes one office location. Deactivate the other one, or add the Multiple Office Locations module.");
        var o = id is null ? db.OfficeLocations.Add(new OfficeLocation()).Entity : await db.OfficeLocations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return Result.Fail("Location not found.");
        o.Name = i.Name.Trim(); o.BranchId = i.BranchId; o.Latitude = Math.Round(i.Latitude, 6); o.Longitude = Math.Round(i.Longitude, 6);
        o.RadiusMeters = i.RadiusMeters; o.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<GeoAttendanceMode> ModeAsync(CancellationToken ct = default) =>
        await db.OrganizationSettings.AsNoTracking().Select(s => s.GeoMode).FirstOrDefaultAsync(ct);

    public async Task<Result> SetModeAsync(GeoAttendanceMode mode, CancellationToken ct = default)
    {
        var s = await db.OrganizationSettings.FirstOrDefaultAsync(ct);
        if (s is null) return Result.Fail("Organization settings not found.");
        if (mode == GeoAttendanceMode.Enforce && !await db.OfficeLocations.AnyAsync(o => o.IsActive, ct)) return Result.Fail("Add an office location before enforcing the geo-fence.");
        s.GeoMode = mode;
        audit.Add("Attendance.GeoMode", "OrganizationSetting", null, mode.ToString());
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetExemptAsync(int employeeId, bool exempt, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return Result.Fail("Employee not found.");
        e.GeoFenceExempt = exempt;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<CoreHr.LookupItem>> ExemptEmployeesAsync(CancellationToken ct = default) =>
        await db.Employees.AsNoTracking().Where(e => e.GeoFenceExempt && e.Status != EmployeeStatus.Exited)
            .OrderBy(e => e.FirstName).Select(e => new CoreHr.LookupItem(e.Id, e.FirstName + " " + e.LastName + " (" + e.EmployeeCode + ")")).ToListAsync(ct);

    public async Task<IReadOnlyList<GeoPunchRow>> RecentPunchesAsync(DateOnly date, CancellationToken ct = default)
    {
        var clockOrg = await db.OrgClockAsync(context.OrgId(), clock, ct);
        var rows = await db.AttendancePunches.AsNoTracking().Where(p => p.AttendanceDate == date && p.LocationStatus != PunchLocationStatus.NotCaptured)
            .Join(db.Employees, p => p.EmployeeId, e => e.Id, (p, e) => new { p, e.FirstName, e.LastName, e.EmployeeCode })
            .OrderBy(x => x.p.TimestampUtc).Take(500).ToListAsync(ct);
        var offices = await db.OfficeLocations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name, ct);
        return rows.Select(x => new GeoPunchRow(clockOrg.ToLocal(x.p.TimestampUtc), $"{x.FirstName} {x.LastName}", x.EmployeeCode, x.p.Direction.ToString(), x.p.LocationStatus,
                x.p.OfficeLocationId is int o && offices.TryGetValue(o, out var n) ? n : null, x.p.DistanceMeters, x.p.AccuracyMeters, x.p.Latitude, x.p.Longitude))
            .OrderByDescending(r => r.Status == PunchLocationStatus.Outside).ThenBy(r => r.LocalTime).ToList();
    }
}
