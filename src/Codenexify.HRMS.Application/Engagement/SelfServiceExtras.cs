using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Engagement;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Engagement;

// ====================================================================== profile change requests

/// <summary>Personal details an employee may ask to correct. Bank, PAN and employment data stay with HR.</summary>
public sealed class ProfileChangeInput
{
    [Phone, StringLength(20), Display(Name = "Mobile")] public string? MobilePhone { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "Personal email")] public string? PersonalEmail { get; set; }
    [StringLength(500), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    [StringLength(500), Display(Name = "Permanent address")] public string? PermanentAddress { get; set; }
    [Display(Name = "Marital status")] public MaritalStatus? MaritalStatus { get; set; }
    [StringLength(5), RegularExpression("^(A|B|AB|O)[+-]$", ErrorMessage = "Use a blood group like O+ or AB-."), Display(Name = "Blood group")] public string? BloodGroup { get; set; }
    [StringLength(150), Display(Name = "Emergency contact name")] public string? EmergencyContactName { get; set; }
    [StringLength(50), Display(Name = "Relation")] public string? EmergencyContactRelation { get; set; }
    [Phone, StringLength(20), Display(Name = "Emergency contact phone")] public string? EmergencyContactPhone { get; set; }
    [StringLength(500), Display(Name = "Note for HR (optional)")] public string? Note { get; set; }
}

public sealed record FieldChange(string Field, string Label, string? From, string? To);
public sealed record ProfileChangeRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, ProfileChangeStatus Status, DateTime RequestedAtUtc,
    IReadOnlyList<FieldChange> Changes, string? Note, string? DecidedBy, DateTime? DecidedAtUtc, string? DecisionNotes);

public interface IProfileChangeService
{
    Task<ProfileChangeInput?> CurrentAsync(int employeeId, CancellationToken ct = default);
    Task<Result> SubmitAsync(int employeeId, ProfileChangeInput input, CancellationToken ct = default);
    Task<Result> CancelAsync(int requestId, int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<ProfileChangeRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default);
    Task<PagedResult<ProfileChangeRow>> ListAsync(ProfileChangeStatus? status, int page, CancellationToken ct = default);
    Task<int> PendingCountAsync(CancellationToken ct = default);
    Task<Result> ApproveAsync(int requestId, string? notes, CancellationToken ct = default);
    Task<Result> RejectAsync(int requestId, string notes, CancellationToken ct = default);
}

public sealed class ProfileChangeService(IAppDbContext db, ICurrentContext context, IClock clock, INotificationService notifications) : IProfileChangeService
{
    private sealed record Field(string Label, Func<Employee, string?> Get, Action<Employee, string?> Set, Func<ProfileChangeInput, string?> FromInput);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static readonly IReadOnlyDictionary<string, Field> Fields = new Dictionary<string, Field>
    {
        [nameof(Employee.MobilePhone)] = new("Mobile", e => e.MobilePhone, (e, v) => e.MobilePhone = v, i => Clean(i.MobilePhone)),
        [nameof(Employee.PersonalEmail)] = new("Personal email", e => e.PersonalEmail, (e, v) => e.PersonalEmail = v, i => Clean(i.PersonalEmail)),
        [nameof(Employee.CurrentAddress)] = new("Current address", e => e.CurrentAddress, (e, v) => e.CurrentAddress = v, i => Clean(i.CurrentAddress)),
        [nameof(Employee.PermanentAddress)] = new("Permanent address", e => e.PermanentAddress, (e, v) => e.PermanentAddress = v, i => Clean(i.PermanentAddress)),
        [nameof(Employee.MaritalStatus)] = new("Marital status", e => e.MaritalStatus?.ToString(),
            (e, v) => e.MaritalStatus = v is null ? null : Enum.Parse<MaritalStatus>(v), i => i.MaritalStatus?.ToString()),
        [nameof(Employee.BloodGroup)] = new("Blood group", e => e.BloodGroup, (e, v) => e.BloodGroup = v, i => Clean(i.BloodGroup)?.ToUpperInvariant()),
        [nameof(Employee.EmergencyContactName)] = new("Emergency contact", e => e.EmergencyContactName, (e, v) => e.EmergencyContactName = v, i => Clean(i.EmergencyContactName)),
        [nameof(Employee.EmergencyContactRelation)] = new("Emergency contact relation", e => e.EmergencyContactRelation, (e, v) => e.EmergencyContactRelation = v, i => Clean(i.EmergencyContactRelation)),
        [nameof(Employee.EmergencyContactPhone)] = new("Emergency contact phone", e => e.EmergencyContactPhone, (e, v) => e.EmergencyContactPhone = v, i => Clean(i.EmergencyContactPhone)),
    };

    public async Task<ProfileChangeInput?> CurrentAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        return e is null ? null : new ProfileChangeInput
        {
            MobilePhone = e.MobilePhone, PersonalEmail = e.PersonalEmail, CurrentAddress = e.CurrentAddress, PermanentAddress = e.PermanentAddress,
            MaritalStatus = e.MaritalStatus, BloodGroup = e.BloodGroup, EmergencyContactName = e.EmergencyContactName,
            EmergencyContactRelation = e.EmergencyContactRelation, EmergencyContactPhone = e.EmergencyContactPhone
        };
    }

    public async Task<Result> SubmitAsync(int employeeId, ProfileChangeInput input, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null || !e.IsCurrent) return Result.Fail("Employee not found.");
        var changes = new Dictionary<string, string?>();
        var previous = new Dictionary<string, string?>();
        foreach (var (key, f) in Fields)
        {
            var requested = f.FromInput(input);
            var current = f.Get(e);
            if (!string.Equals(requested, current, StringComparison.Ordinal)) { changes[key] = requested; previous[key] = current; }
        }
        if (changes.Count == 0) return Result.Fail("Nothing has changed.");

        // One open request per employee: a new submission replaces the pending one.
        foreach (var old in await db.ProfileChangeRequests.Where(r => r.EmployeeId == employeeId && r.Status == ProfileChangeStatus.Pending).ToListAsync(ct))
            old.Status = ProfileChangeStatus.Cancelled;
        db.ProfileChangeRequests.Add(new ProfileChangeRequest
        {
            EmployeeId = employeeId, RequestedByUserId = context.UserId ?? 0, Status = ProfileChangeStatus.Pending,
            ChangesJson = JsonSerializer.Serialize(changes), PreviousJson = JsonSerializer.Serialize(previous), Note = Clean(input.Note)
        });
        var hr = await notifications.UsersWithPermissionAsync(TenantPermissions.EmployeesManage, ct);
        notifications.Queue(hr.Where(u => u != context.UserId), "Profile change request", $"{e.FullName} asked to update {string.Join(", ", changes.Keys.Select(k => Fields[k].Label.ToLowerInvariant()))}.", "/org/employees/requests");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int requestId, int employeeId, CancellationToken ct = default)
    {
        var r = await db.ProfileChangeRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.EmployeeId == employeeId, ct);
        if (r is null) return Result.Fail("Request not found.");
        if (r.Status != ProfileChangeStatus.Pending) return Result.Fail("Only pending requests can be cancelled.");
        r.Status = ProfileChangeStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static IReadOnlyList<FieldChange> Describe(ProfileChangeRequest r)
    {
        var to = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.ChangesJson) ?? [];
        var from = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.PreviousJson) ?? [];
        return to.Where(kv => Fields.ContainsKey(kv.Key))
            .Select(kv => new FieldChange(kv.Key, Fields[kv.Key].Label, from.GetValueOrDefault(kv.Key), kv.Value)).ToList();
    }

    private async Task<List<ProfileChangeRow>> MaterializeAsync(IQueryable<ProfileChangeRequest> q, CancellationToken ct)
    {
        var rows = await q.Join(db.Employees, r => r.EmployeeId, e => e.Id, (r, e) => new { r, e.FirstName, e.MiddleName, e.LastName, e.EmployeeCode }).ToListAsync(ct);
        return rows.Select(x => new ProfileChangeRow(x.r.Id, x.r.EmployeeId,
            string.Join(' ', new[] { x.FirstName, x.MiddleName, x.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), x.EmployeeCode,
            x.r.Status, x.r.CreatedAtUtc, Describe(x.r), x.r.Note, x.r.DecidedBy, x.r.DecidedAtUtc, x.r.DecisionNotes)).ToList();
    }

    public async Task<IReadOnlyList<ProfileChangeRow>> ForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        await MaterializeAsync(db.ProfileChangeRequests.AsNoTracking().Where(r => r.EmployeeId == employeeId).OrderByDescending(r => r.Id).Take(10), ct);

    public async Task<PagedResult<ProfileChangeRow>> ListAsync(ProfileChangeStatus? status, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.ProfileChangeRequests.AsNoTracking();
        if (status is { } s) q = q.Where(r => r.Status == s);
        var total = await q.CountAsync(ct);
        return new PagedResult<ProfileChangeRow>(await MaterializeAsync(q.OrderByDescending(r => r.Id).Skip((page - 1) * size).Take(size), ct), total, page, size);
    }

    public Task<int> PendingCountAsync(CancellationToken ct = default) =>
        db.ProfileChangeRequests.CountAsync(r => r.Status == ProfileChangeStatus.Pending, ct);

    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public async Task<Result> ApproveAsync(int requestId, string? notes, CancellationToken ct = default)
    {
        var r = await db.ProfileChangeRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null) return Result.Fail("Request not found.");
        if (r.Status != ProfileChangeStatus.Pending) return Result.Fail("This request has already been decided.");
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == r.EmployeeId, ct);
        if (e is null) return Result.Fail("Employee not found.");
        if (e.UserId is int owner && owner == context.UserId) return Result.Fail("Another HR user must approve changes to your own profile.");
        foreach (var c in Describe(r)) Fields[c.Field].Set(e, c.To);
        r.Status = ProfileChangeStatus.Approved;
        r.DecidedBy = await MyNameAsync(ct);
        r.DecidedAtUtc = clock.UtcNow;
        r.DecisionNotes = Clean(notes);
        if (e.UserId is int uid) notifications.Queue([uid], "Profile updated", "HR approved your requested changes.", "/me");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int requestId, string notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notes)) return Result.Fail("Tell the employee why.");
        var r = await db.ProfileChangeRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null) return Result.Fail("Request not found.");
        if (r.Status != ProfileChangeStatus.Pending) return Result.Fail("This request has already been decided.");
        r.Status = ProfileChangeStatus.Rejected;
        r.DecidedBy = await MyNameAsync(ct);
        r.DecidedAtUtc = clock.UtcNow;
        r.DecisionNotes = notes.Trim();
        var uid = await db.Employees.Where(x => x.Id == r.EmployeeId).Select(x => x.UserId).FirstOrDefaultAsync(ct);
        if (uid is int u) notifications.Queue([u], "Profile change not approved", notes.Trim(), "/me/profile");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ====================================================================== directory, celebrations, preferences

public sealed record DirectoryRow(int EmployeeId, string Name, string? Designation, string? Department, string? Branch, string WorkEmail, string? Manager);

public enum CelebrationKind { Birthday, WorkAnniversary }
public sealed record Celebration(int EmployeeId, string Name, string? Designation, CelebrationKind Kind, DateOnly Date, int Years, bool IsToday);

public sealed record Preferences(bool HideBirthday, bool MuteEmailNotifications, bool HasEmployee);

public interface IPeopleService
{
    Task<PagedResult<DirectoryRow>> DirectoryAsync(string? search, int? departmentId, int page, CancellationToken ct = default);
    Task<IReadOnlyList<Celebration>> CelebrationsAsync(int daysAhead, CancellationToken ct = default);
    Task<Preferences> PreferencesAsync(CancellationToken ct = default);
    Task<Result> SavePreferencesAsync(bool hideBirthday, bool muteEmail, CancellationToken ct = default);
}

public sealed class PeopleService(IAppDbContext db, ICurrentContext context, IClock clock) : IPeopleService
{
    private static string Name(string f, string? m, string l) => string.Join(' ', new[] { f, m, l }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public async Task<PagedResult<DirectoryRow>> DirectoryAsync(string? search, int? departmentId, int page, CancellationToken ct = default)
    {
        const int size = 30;
        page = Math.Max(1, page);
        // Work contact details only: personal phone, address and dates stay private.
        var q = db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive);
        if (departmentId is int d) q = q.Where(e => e.DepartmentId == d);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => (e.FirstName + " " + e.LastName).Contains(s) || e.WorkEmail.Contains(s)
                || (e.Designation != null && e.Designation.Name.Contains(s)) || (e.Department != null && e.Department.Name.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Skip((page - 1) * size).Take(size)
            .Select(e => new
            {
                e.Id, e.FirstName, e.MiddleName, e.LastName, Designation = e.Designation != null ? e.Designation.Name : null,
                Department = e.Department != null ? e.Department.Name : null, Branch = e.Branch != null ? e.Branch.Name : null, e.WorkEmail,
                Manager = e.ReportingManager != null ? e.ReportingManager.FirstName + " " + e.ReportingManager.LastName : null
            }).ToListAsync(ct);
        return new PagedResult<DirectoryRow>(rows.Select(r => new DirectoryRow(r.Id, Name(r.FirstName, r.MiddleName, r.LastName), r.Designation, r.Department, r.Branch, r.WorkEmail, r.Manager)).ToList(), total, page, size);
    }

    public async Task<IReadOnlyList<Celebration>> CelebrationsAsync(int daysAhead, CancellationToken ct = default)
    {
        var today = (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
        var people = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName, e.DateOfBirth, e.HideBirthday, e.DateOfJoining, Designation = e.Designation != null ? e.Designation.Name : null })
            .ToListAsync(ct);

        static DateOnly NextOccurrence(DateOnly date, DateOnly from)
        {
            DateOnly On(int year) => new(year, date.Month, date.Month == 2 && date.Day == 29 && !DateTime.IsLeapYear(year) ? 28 : date.Day);
            var d = On(from.Year);
            return d < from ? On(from.Year + 1) : d;
        }

        var result = new List<Celebration>();
        var until = today.AddDays(daysAhead);
        foreach (var p in people)
        {
            var name = Name(p.FirstName, p.MiddleName, p.LastName);
            if (p.DateOfBirth is { } dob && !p.HideBirthday)
            {
                var next = NextOccurrence(dob, today);
                if (next <= until) result.Add(new Celebration(p.Id, name, p.Designation, CelebrationKind.Birthday, next, 0, next == today));
            }
            var anniversary = NextOccurrence(p.DateOfJoining, today);
            var years = anniversary.Year - p.DateOfJoining.Year;
            if (years >= 1 && anniversary <= until)
                result.Add(new Celebration(p.Id, name, p.Designation, CelebrationKind.WorkAnniversary, anniversary, years, anniversary == today));
        }
        return result.OrderBy(c => c.Date).ThenBy(c => c.Name).ToList();
    }

    public async Task<Preferences> PreferencesAsync(CancellationToken ct = default)
    {
        var uid = context.UserId ?? 0;
        var mute = await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.MuteEmailNotifications).FirstOrDefaultAsync(ct);
        var emp = await db.Employees.AsNoTracking().Where(e => e.UserId == uid).Select(e => new { e.HideBirthday }).FirstOrDefaultAsync(ct);
        return new Preferences(emp?.HideBirthday ?? false, mute, emp is not null);
    }

    public async Task<Result> SavePreferencesAsync(bool hideBirthday, bool muteEmail, CancellationToken ct = default)
    {
        var uid = context.UserId ?? 0;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user is null) return Result.Fail("Only organization users have preferences.");
        user.MuteEmailNotifications = muteEmail;
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.UserId == uid, ct);
        if (emp is not null) emp.HideBirthday = hideBirthday;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

// ====================================================================== email copies of notifications

public interface INotificationDispatcher
{
    /// <summary>Emails pending in-app notifications (platform scope). Returns how many emails were sent.</summary>
    Task<int> RunAsync(string? portalBaseUrl, CancellationToken ct = default);
}

public sealed class NotificationDispatcher(IAppDbContext db, ICurrentContext context, IClock clock, IEmailSender email, IFeatureAccessService features) : INotificationDispatcher
{
    /// <summary>Older notifications are not emailed (avoids a burst after email is first switched on).</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(2);

    public async Task<int> RunAsync(string? portalBaseUrl, CancellationToken ct = default)
    {
        if (!context.IsPlatform) throw new TenantIsolationException("The dispatcher runs in platform scope.");
        var pending = await db.Notifications.Where(n => n.EmailPending).OrderBy(n => n.Id).Take(300).ToListAsync(ct);
        if (pending.Count == 0) return 0;
        var userIds = pending.Select(n => n.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.IsActive, u.MuteEmailNotifications }).ToDictionaryAsync(u => u.Id, ct);
        var enabled = new Dictionary<int, bool>();
        var sent = 0;
        var cutoff = clock.UtcNow - MaxAge;
        foreach (var n in pending)
        {
            n.EmailPending = false;
            if (n.CreatedAtUtc < cutoff || !users.TryGetValue(n.UserId, out var u) || !u.IsActive || u.MuteEmailNotifications || string.IsNullOrWhiteSpace(u.Email)) continue;
            if (!enabled.TryGetValue(n.OrganizationId, out var on))
                enabled[n.OrganizationId] = on = (await features.GetEnabledFeaturesAsync(n.OrganizationId, ct)).Contains(FeatureCodes.NotificationsBasic);
            if (!on) continue;
            var link = n.Link is not null && !string.IsNullOrWhiteSpace(portalBaseUrl) ? portalBaseUrl.TrimEnd('/') + n.Link : null;
            if (await email.SendAsync(new EmailMessage(u.Email, n.Title, Body(n.Title, n.Message, link), "notification", n.OrganizationId), ct)) sent++;
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }

    private static string Body(string title, string message, string? link)
    {
        static string E(string s) => System.Net.WebUtility.HtmlEncode(s);
        return $"<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:560px;color:#1e293b\"><h3>{E(title)}</h3><p style=\"white-space:pre-wrap\">{E(message)}</p>" +
            (link is null ? "" : $"<p><a href=\"{E(link)}\" style=\"color:#4f46e5\">Open in HRMS</a></p>") +
            "<p style=\"color:#64748b;font-size:12px;margin-top:24px\">You can turn off email copies under My home → Preferences.</p></div>";
    }
}
