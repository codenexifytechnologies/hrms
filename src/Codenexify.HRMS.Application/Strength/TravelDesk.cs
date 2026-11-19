using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

public sealed class TravelInput
{
    [Required, StringLength(300)] public string Purpose { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "From")] public string FromCity { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "To")] public string ToCity { get; set; } = string.Empty;
    [Display(Name = "Departure")] public DateOnly DepartOn { get; set; }
    [Display(Name = "Return (blank = one way)")] public DateOnly? ReturnOn { get; set; }
    public TravelMode Mode { get; set; } = TravelMode.Train;
    [Display(Name = "Needs a hotel")] public bool NeedsAccommodation { get; set; }
    [Range(0, 10_000_000), Display(Name = "Estimated cost (₹)")] public decimal? EstimatedCost { get; set; }
    [Range(0, 10_000_000), Display(Name = "Advance needed (₹)")] public decimal? AdvanceRequested { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed record TravelRow(int Id, int EmployeeId, string EmployeeName, string Purpose, string FromCity, string ToCity, DateOnly DepartOn, DateOnly? ReturnOn,
    TravelMode Mode, TravelStatus Status, decimal? EstimatedCost, decimal? AdvanceRequested, decimal? AdvancePaid, bool HasTicket);
public sealed record TravelView(TravelRequest Request, string EmployeeName, string EmployeeCode, string? Approver, bool CanDecide, bool CanBook, bool CanCancel);
public sealed record TravelTicket(Stream Content, string FileName, string ContentType);

public interface ITravelService
{
    Task<IReadOnlyList<TravelRow>> MineAsync(int employeeId, CancellationToken ct = default);
    Task<Result<int>> SubmitAsync(int employeeId, TravelInput input, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, int employeeId, CancellationToken ct = default);
    /// <summary>Requests waiting for the signed-in manager.</summary>
    Task<IReadOnlyList<TravelRow>> ToApproveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TravelRow>> DeskAsync(TravelStatus? status, CancellationToken ct = default);
    /// <param name="isDesk">The viewer holds the travel desk permission.</param>
    /// <param name="myEmployeeId">The viewer's own employee record, if any.</param>
    Task<TravelView?> GetAsync(int id, bool isDesk, int? myEmployeeId, CancellationToken ct = default);
    Task<Result> DecideAsync(int id, bool approve, string? note, bool isDesk, CancellationToken ct = default);
    Task<Result> BookAsync(int id, string details, decimal? bookedCost, UploadedFile? ticket, CancellationToken ct = default);
    Task<Result> RecordAdvanceAsync(int id, decimal amount, DateOnly paidOn, CancellationToken ct = default);
    Task<Result> CompleteAsync(int id, CancellationToken ct = default);
    Task<TravelTicket?> TicketAsync(int id, bool isDesk, int? myEmployeeId, CancellationToken ct = default);
}

public sealed class TravelService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage, INotificationService notifications) : ITravelService
{
    private const long MaxTicketBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> TicketTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [0x25, 0x50, 0x44, 0x46]),
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
    };

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private IQueryable<TravelRow> Rows(IQueryable<TravelRequest> q) =>
        q.Join(db.Employees, t => t.EmployeeId, e => e.Id, (t, e) => new TravelRow(t.Id, e.Id, e.FirstName + " " + e.LastName, t.Purpose, t.FromCity, t.ToCity,
            t.DepartOn, t.ReturnOn, t.Mode, t.Status, t.EstimatedCost, t.AdvanceRequested, t.AdvancePaid, t.TicketStorageKey != null));

    public async Task<IReadOnlyList<TravelRow>> MineAsync(int employeeId, CancellationToken ct = default) =>
        (await Rows(db.TravelRequests.AsNoTracking().Where(t => t.EmployeeId == employeeId).OrderByDescending(t => t.DepartOn).Take(100)).ToListAsync(ct))
            .OrderByDescending(t => t.DepartOn).ToList();

    public async Task<Result<int>> SubmitAsync(int employeeId, TravelInput i, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null || !emp.IsCurrent) return Result<int>.Fail("Only current employees can request travel.");
        var today = await TodayAsync(ct);
        if (i.DepartOn < today) return Result<int>.Fail("The departure date is in the past.");
        if (i.DepartOn > today.AddYears(1)) return Result<int>.Fail("Trips can be requested up to a year ahead.");
        if (i.ReturnOn is { } r && r < i.DepartOn) return Result<int>.Fail("The return date is before departure.");
        if (i.AdvanceRequested is > 0 && i.EstimatedCost is { } est && i.AdvanceRequested > est) return Result<int>.Fail("The advance can't be more than the estimated cost.");
        if (string.Equals(i.FromCity.Trim(), i.ToCity.Trim(), StringComparison.OrdinalIgnoreCase)) return Result<int>.Fail("From and to are the same place.");

        // The reporting manager approves; without an active manager login the travel desk does.
        int? approver = emp.ReportingManagerId is int m
            ? await db.Employees.Where(e => e.Id == m && e.UserId != null && db.Users.Any(u => u.Id == e.UserId && u.IsActive)).Select(e => e.UserId).FirstOrDefaultAsync(ct)
            : null;
        if (approver == emp.UserId) approver = null;
        var request = new TravelRequest
        {
            EmployeeId = employeeId, Purpose = i.Purpose.Trim(), FromCity = i.FromCity.Trim(), ToCity = i.ToCity.Trim(), DepartOn = i.DepartOn, ReturnOn = i.ReturnOn,
            Mode = i.Mode, NeedsAccommodation = i.NeedsAccommodation, EstimatedCost = i.EstimatedCost, AdvanceRequested = i.AdvanceRequested is > 0 ? i.AdvanceRequested : null,
            Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim(), ApproverUserId = approver
        };
        db.TravelRequests.Add(request);
        await db.SaveChangesAsync(ct);
        var who = emp.FullName;
        if (approver is int a) notifications.Queue([a], "Travel request to approve", $"{who}: {request.FromCity} → {request.ToCity} on {request.DepartOn:dd MMM}.", $"/me/travel/{request.Id}");
        else notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.TravelManage, ct), "Travel request", $"{who}: {request.FromCity} → {request.ToCity} on {request.DepartOn:dd MMM} (no manager to approve).", $"/org/travel/{request.Id}");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(request.Id);
    }

    public async Task<Result> CancelAsync(int id, int employeeId, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (t is null) return Result.Fail("Request not found.");
        if (t.Status is not (TravelStatus.Submitted or TravelStatus.Approved or TravelStatus.Booked)) return Result.Fail("This trip can no longer be cancelled.");
        var wasBooked = t.Status == TravelStatus.Booked;
        t.Status = TravelStatus.Cancelled;
        if (wasBooked || t.AdvancePaid is > 0)
            notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.TravelManage, ct), "Booked trip cancelled",
                $"{t.FromCity} → {t.ToCity} on {t.DepartOn:dd MMM} was cancelled by the employee{(t.AdvancePaid is > 0 ? $"; an advance of ₹{t.AdvancePaid:N0} was paid" : "")}.", $"/org/travel/{t.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<TravelRow>> ToApproveAsync(CancellationToken ct = default)
    {
        var me = context.UserId;
        return (await Rows(db.TravelRequests.AsNoTracking().Where(t => t.Status == TravelStatus.Submitted && t.ApproverUserId == me)).ToListAsync(ct))
            .OrderBy(t => t.DepartOn).ToList();
    }

    public async Task<IReadOnlyList<TravelRow>> DeskAsync(TravelStatus? status, CancellationToken ct = default)
    {
        var q = db.TravelRequests.AsNoTracking();
        q = status is { } s ? q.Where(t => t.Status == s) : q.Where(t => t.Status != TravelStatus.Cancelled && t.Status != TravelStatus.Rejected);
        return (await Rows(q.OrderByDescending(t => t.DepartOn).Take(300)).ToListAsync(ct)).OrderBy(t => t.Status).ThenBy(t => t.DepartOn).ToList();
    }

    /// <summary>The desk decides only when no manager is set, or the manager's login is no longer active.</summary>
    private async Task<bool> DeskMayDecideAsync(TravelRequest t, CancellationToken ct) =>
        t.ApproverUserId is null || !await db.Users.AnyAsync(u => u.Id == t.ApproverUserId && u.IsActive, ct);

    public async Task<TravelView?> GetAsync(int id, bool isDesk, int? myEmployeeId, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;
        var mine = myEmployeeId == t.EmployeeId;
        var approver = t.ApproverUserId == context.UserId && t.ApproverUserId is not null;
        if (!isDesk && !mine && !approver) return null;
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == t.EmployeeId, ct);
        var approverName = t.ApproverUserId is int a ? await db.Users.Where(u => u.Id == a).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        var canDecide = t.Status == TravelStatus.Submitted && !mine && (approver || (isDesk && await DeskMayDecideAsync(t, ct)));
        return new TravelView(t, emp.FullName, emp.EmployeeCode, approverName, canDecide,
            isDesk && t.Status is TravelStatus.Approved or TravelStatus.Booked, mine && t.Status is TravelStatus.Submitted or TravelStatus.Approved or TravelStatus.Booked);
    }

    public async Task<Result> DecideAsync(int id, bool approve, string? note, bool isDesk, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Request not found.");
        if (t.Status != TravelStatus.Submitted) return Result.Fail("This request has already been decided.");
        var myEmployee = await db.Employees.Where(e => e.UserId == context.UserId).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        if (myEmployee == t.EmployeeId) return Result.Fail("You can't approve your own travel.");
        var allowed = (t.ApproverUserId is not null && t.ApproverUserId == context.UserId) || (isDesk && await DeskMayDecideAsync(t, ct));
        if (!allowed) return Result.Fail("You're not the approver for this request.");
        if (!approve && string.IsNullOrWhiteSpace(note)) return Result.Fail("Give a reason for rejecting.");
        t.Status = approve ? TravelStatus.Approved : TravelStatus.Rejected;
        t.DecidedBy = await MyNameAsync(ct);
        t.DecidedAtUtc = clock.UtcNow;
        t.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)];
        var empUser = await db.Employees.Where(e => e.Id == t.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (empUser is int u) notifications.Queue([u], approve ? "Travel approved" : "Travel not approved", $"{t.FromCity} → {t.ToCity} on {t.DepartOn:dd MMM}{(approve ? " — the travel desk will book it." : $": {t.DecisionNote}")}", $"/me/travel/{t.Id}");
        if (approve) notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.TravelManage, ct), "Trip to book", $"{t.FromCity} → {t.ToCity} on {t.DepartOn:dd MMM} is approved.", $"/org/travel/{t.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> BookAsync(int id, string details, decimal? bookedCost, UploadedFile? ticket, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Request not found.");
        if (t.Status is not (TravelStatus.Approved or TravelStatus.Booked)) return Result.Fail("Only approved trips can be booked.");
        if (string.IsNullOrWhiteSpace(details) || details.Length > 2000) return Result.Fail("Enter the booking details (PNR, train/flight, hotel…), up to 2000 characters.");
        if (bookedCost is < 0) return Result.Fail("The cost can't be negative.");

        string? newKey = null;
        if (ticket is { Length: > 0 })
        {
            var ext = Path.GetExtension(ticket.FileName);
            if (!TicketTypes.TryGetValue(ext, out var kind)) return Result.Fail("Tickets must be PDF, PNG or JPG.");
            if (ticket.Length > MaxTicketBytes) return Result.Fail("Tickets can be at most 5 MB.");
            using var buffer = new MemoryStream();
            await ticket.Content.CopyToAsync(buffer, ct);
            if (buffer.Length < kind.Signature.Length || !buffer.GetBuffer().AsSpan(0, kind.Signature.Length).SequenceEqual(kind.Signature))
                return Result.Fail("The ticket file content does not match its type.");
            buffer.Position = 0;
            newKey = await storage.SaveAsync(context.OrgId(), buffer, ext.ToLowerInvariant(), ct);
            var oldKey = t.TicketStorageKey;
            t.TicketStorageKey = newKey;
            t.TicketFileName = SafeName(ticket.FileName);
            t.TicketContentType = kind.ContentType;
            if (oldKey is not null) await storage.DeleteAsync(oldKey, ct);
        }
        var first = t.Status == TravelStatus.Approved;
        t.Status = TravelStatus.Booked;
        t.BookingDetails = details.Trim();
        t.BookedCost = bookedCost;
        t.BookedBy = await MyNameAsync(ct);
        t.BookedAtUtc = clock.UtcNow;
        var empUser = await db.Employees.Where(e => e.Id == t.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (empUser is int u) notifications.Queue([u], first ? "Your trip is booked" : "Booking updated", $"{t.FromCity} → {t.ToCity} on {t.DepartOn:dd MMM}. See the details and ticket.", $"/me/travel/{t.Id}");
        try { await db.SaveChangesAsync(ct); }
        catch
        {
            if (newKey is not null) await storage.DeleteAsync(newKey, ct);
            throw;
        }
        return Result.Ok();
    }

    private static string SafeName(string name)
    {
        var file = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
        return file.Length > 200 ? file[^200..] : file;
    }

    public async Task<Result> RecordAdvanceAsync(int id, decimal amount, DateOnly paidOn, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Request not found.");
        if (t.Status is not (TravelStatus.Approved or TravelStatus.Booked or TravelStatus.Completed)) return Result.Fail("Advances are paid only for approved trips.");
        if (amount <= 0 || amount > 10_000_000) return Result.Fail("Enter the amount paid.");
        if (paidOn > await TodayAsync(ct)) return Result.Fail("The payment date can't be in the future.");
        t.AdvancePaid = amount;
        t.AdvancePaidOn = paidOn;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CompleteAsync(int id, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Request not found.");
        if (t.Status != TravelStatus.Booked) return Result.Fail("Only booked trips can be completed.");
        if ((t.ReturnOn ?? t.DepartOn) > await TodayAsync(ct)) return Result.Fail("The trip hasn't ended yet.");
        t.Status = TravelStatus.Completed;
        var empUser = await db.Employees.Where(e => e.Id == t.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync(ct);
        if (empUser is int u) notifications.Queue([u], "Trip closed", $"Claim your {t.ToCity} trip expenses under Expenses{(t.AdvancePaid is > 0 ? $" (the ₹{t.AdvancePaid:N0} advance will be adjusted)" : "")}.", "/me/expenses");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<TravelTicket?> TicketAsync(int id, bool isDesk, int? myEmployeeId, CancellationToken ct = default)
    {
        var t = await db.TravelRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t?.TicketStorageKey is null || (!isDesk && t.EmployeeId != myEmployeeId)) return null;
        var stream = await storage.OpenReadAsync(t.TicketStorageKey, ct);
        return stream is null ? null : new TravelTicket(stream, t.TicketFileName ?? "ticket", t.TicketContentType ?? "application/octet-stream");
    }
}
