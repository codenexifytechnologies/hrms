using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Enums;
using Codenexify.HRMS.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Planning;

public sealed class PreboardingSettingsInput
{
    [StringLength(4000), Display(Name = "Welcome message")] public string? WelcomeMessage { get; set; }
    [StringLength(4000), Display(Name = "Your first day")] public string? FirstDayInfo { get; set; }
    [StringLength(150), Display(Name = "Contact person")] public string? ContactName { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "Contact email")] public string? ContactEmail { get; set; }
    [Phone, StringLength(20), Display(Name = "Contact phone")] public string? ContactPhone { get; set; }
    [Display(Name = "Ask for bank details (for salary)")] public bool AskBankDetails { get; set; } = true;
    [Display(Name = "Ask for PAN")] public bool AskPan { get; set; } = true;
    [Range(0, 30), Display(Name = "Link works for this many days after joining")] public int LinkDaysAfterJoining { get; set; } = 3;
}

public sealed class PreboardingItemInput
{
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [StringLength(2000), Display(Name = "Instructions / text to accept")] public string? Description { get; set; }
    [Display(Name = "Type")] public PreboardingItemKind Kind { get; set; } = PreboardingItemKind.Document;
    [Display(Name = "Document category")] public DocumentCategory DocumentCategory { get; set; } = DocumentCategory.Identity;
    [Display(Name = "Required")] public bool IsRequired { get; set; } = true;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Range(0, 1000), Display(Name = "Order")] public int SortOrder { get; set; }
}

/// <summary>What the new hire fills in. Aadhaar is deliberately not collected.</summary>
public sealed class PreboardingPersonalInput
{
    public Gender? Gender { get; set; }
    [Required, Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [Display(Name = "Marital status")] public MaritalStatus? MaritalStatus { get; set; }
    [StringLength(5), Display(Name = "Blood group")] public string? BloodGroup { get; set; }
    [Required, StringLength(500), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    [StringLength(500), Display(Name = "Permanent address (if different)")] public string? PermanentAddress { get; set; }
    [Required, StringLength(150), Display(Name = "Emergency contact")] public string? EmergencyContactName { get; set; }
    [Required, StringLength(50), Display(Name = "Relationship")] public string? EmergencyContactRelation { get; set; }
    [Required, Phone, StringLength(20), Display(Name = "Emergency phone")] public string? EmergencyContactPhone { get; set; }
    [StringLength(10), RegularExpression("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$", ErrorMessage = "Enter a valid PAN."), Display(Name = "PAN")] public string? Pan { get; set; }
}

public enum PreboardingState { NotInvited, Invited, Opened, Submitted, Expired, Revoked }

public sealed record PreboardingJoinerRow(int EmployeeId, string Name, string Code, string? Department, DateOnly DateOfJoining, string? PersonalEmail,
    PreboardingState State, int? InviteId, DateTime? SentAtUtc, DateTime? SubmittedAtUtc, int Done, int Total);
public sealed record PreboardingStepRow(string Title, bool Required, bool Done, DateTime? DoneAtUtc, int? DocumentId, int? ItemId, PreboardingItemKind? Kind, string? Description, DocumentCategory? Category);

public sealed class PreboardingStatus
{
    public required Employee Employee { get; init; }
    public PreboardingInvite? Invite { get; init; }
    public PreboardingState State { get; init; }
    public required IReadOnlyList<PreboardingStepRow> Steps { get; init; }
    public bool AskBank { get; init; }
    public bool AskPan { get; init; }
}

/// <summary>Everything the new hire's page shows. No salary, no other people's data beyond names.</summary>
public sealed class PreboardingPortal
{
    public required string OrganizationName { get; init; }
    public string? LogoUrl { get; init; }
    public required string FirstName { get; init; }
    public required DateOnly DateOfJoining { get; init; }
    public string? Designation { get; init; }
    public string? Department { get; init; }
    public string? ManagerName { get; init; }
    public string? Location { get; init; }
    public PreboardingSettings Settings { get; init; } = new();
    public required IReadOnlyList<PreboardingStepRow> Steps { get; init; }
    public bool PersonalDone { get; init; }
    public bool BankDone { get; init; }
    public string? BankLast4 { get; init; }
    public string? PanMasked { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public required PreboardingPersonalInput Personal { get; init; }
    public bool CanSubmit { get; init; }
}

public interface IPreboardingService
{
    // ---- HR
    Task<PreboardingSettingsInput> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(PreboardingSettingsInput input, CancellationToken ct = default);
    Task<IReadOnlyList<PreboardingItem>> ItemsAsync(bool activeOnly, CancellationToken ct = default);
    Task<PreboardingItemInput?> GetItemAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveItemAsync(int? id, PreboardingItemInput input, CancellationToken ct = default);
    Task<Result> DeleteItemAsync(int id, CancellationToken ct = default);
    /// <summary>Upcoming joiners (joining today or later) and anyone with a link still open.</summary>
    Task<IReadOnlyList<PreboardingJoinerRow>> JoinersAsync(CancellationToken ct = default);
    Task<PreboardingStatus?> StatusAsync(int employeeId, CancellationToken ct = default);
    /// <summary>Creates a new link (older ones stop working) and emails it. Returns the link so HR can also share it another way.</summary>
    Task<Result<string>> SendAsync(int employeeId, string email, string baseUrl, CancellationToken ct = default);
    Task<Result> RevokeAsync(int employeeId, CancellationToken ct = default);
    Task<Result> ReopenAsync(int employeeId, CancellationToken ct = default);

    // ---- new hire (secure link, no sign-in)
    Task<PreboardingPortal?> PortalAsync(string token, CancellationToken ct = default);
    Task<Result> SavePersonalAsync(string token, PreboardingPersonalInput input, CancellationToken ct = default);
    Task<Result> SaveBankAsync(string token, BankInput input, CancellationToken ct = default);
    Task<Result> UploadAsync(string token, int itemId, UploadedFile file, CancellationToken ct = default);
    Task<Result> AcknowledgeAsync(string token, int itemId, CancellationToken ct = default);
    Task<Result> SubmitAsync(string token, CancellationToken ct = default);
}

public sealed class PreboardingService(IAppDbContext db, ICurrentContext context, IClock clock, IEmailSender email, IFeatureAccessService features,
    IEmployeeService employees, IDocumentService documents, INotificationService notifications, IAuditWriter audit) : IPreboardingService
{
    public const int MaxItems = 30;
    public const int MaxSendsPerDay = 5;
    private const string Expired = "This link has expired or is no longer active. Contact the company's HR team for a new one.";

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("preboard:" + token.Trim()))).ToLowerInvariant();

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;

    private async Task<PreboardingSettings> SettingsAsync(CancellationToken ct) =>
        await db.PreboardingSettings.FirstOrDefaultAsync(ct) ?? db.PreboardingSettings.Add(new PreboardingSettings()).Entity;

    // ================================================================== HR

    public async Task<PreboardingSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        return new PreboardingSettingsInput
        {
            WelcomeMessage = s.WelcomeMessage, FirstDayInfo = s.FirstDayInfo, ContactName = s.ContactName, ContactEmail = s.ContactEmail, ContactPhone = s.ContactPhone,
            AskBankDetails = s.AskBankDetails, AskPan = s.AskPan, LinkDaysAfterJoining = s.LinkDaysAfterJoining
        };
    }

    public async Task<Result> SaveSettingsAsync(PreboardingSettingsInput i, CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        s.WelcomeMessage = Clean(i.WelcomeMessage);
        s.FirstDayInfo = Clean(i.FirstDayInfo);
        s.ContactName = Clean(i.ContactName);
        s.ContactEmail = Clean(i.ContactEmail)?.ToLowerInvariant();
        s.ContactPhone = Clean(i.ContactPhone);
        s.AskBankDetails = i.AskBankDetails;
        s.AskPan = i.AskPan;
        s.LinkDaysAfterJoining = Math.Clamp(i.LinkDaysAfterJoining, 0, 30);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<IReadOnlyList<PreboardingItem>> ItemsAsync(bool activeOnly, CancellationToken ct = default) =>
        await db.PreboardingItems.AsNoTracking().Where(x => !activeOnly || x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);

    public async Task<PreboardingItemInput?> GetItemAsync(int id, CancellationToken ct = default) =>
        await db.PreboardingItems.AsNoTracking().Where(x => x.Id == id).Select(x => new PreboardingItemInput
        {
            Title = x.Title, Description = x.Description, Kind = x.Kind, DocumentCategory = x.DocumentCategory, IsRequired = x.IsRequired, IsActive = x.IsActive, SortOrder = x.SortOrder
        }).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> SaveItemAsync(int? id, PreboardingItemInput i, CancellationToken ct = default)
    {
        if (i.Kind == PreboardingItemKind.Acknowledgement && string.IsNullOrWhiteSpace(i.Description))
            return Result<int>.Fail("Write the text the new hire reads and accepts.");
        PreboardingItem item;
        if (id is int existing)
        {
            var found = await db.PreboardingItems.FirstOrDefaultAsync(x => x.Id == existing, ct);
            if (found is null) return Result<int>.Fail("Item not found.");
            item = found;
        }
        else
        {
            if (await db.PreboardingItems.CountAsync(ct) >= MaxItems) return Result<int>.Fail($"You can have at most {MaxItems} items.");
            item = new PreboardingItem();
            db.PreboardingItems.Add(item);
        }
        item.Title = i.Title.Trim();
        item.Description = Clean(i.Description);
        item.Kind = i.Kind;
        item.DocumentCategory = i.DocumentCategory;
        item.IsRequired = i.IsRequired;
        item.IsActive = i.IsActive;
        item.SortOrder = i.SortOrder;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(item.Id);
    }

    public async Task<Result> DeleteItemAsync(int id, CancellationToken ct = default)
    {
        var item = await db.PreboardingItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return Result.Fail("Item not found.");
        // Answers already given keep pointing at the item, so it is switched off rather than removed.
        if (await db.PreboardingResponses.AnyAsync(r => r.ItemId == id, ct))
        {
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Result.Fail("New hires have already answered this item, so it was switched off instead of deleted.");
        }
        db.PreboardingItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static PreboardingState StateOf(PreboardingInvite? i, DateTime now) =>
        i is null ? PreboardingState.NotInvited
        : i.RevokedAtUtc is not null ? PreboardingState.Revoked
        : i.SubmittedAtUtc is not null ? PreboardingState.Submitted
        : i.ExpiresAtUtc <= now ? PreboardingState.Expired
        : i.FirstOpenedAtUtc is not null ? PreboardingState.Opened
        : PreboardingState.Invited;

    public async Task<IReadOnlyList<PreboardingJoinerRow>> JoinersAsync(CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var now = clock.UtcNow;
        var openIds = await db.PreboardingInvites.AsNoTracking().Where(i => i.RevokedAtUtc == null && i.ExpiresAtUtc > now).Select(i => i.EmployeeId).ToListAsync(ct);
        var people = await db.Employees.AsNoTracking().Include(e => e.Department)
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && (e.DateOfJoining >= today || openIds.Contains(e.Id)))
            .OrderBy(e => e.DateOfJoining).Take(500).ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();
        var invites = (await db.PreboardingInvites.AsNoTracking().Include(i => i.Responses).Where(i => ids.Contains(i.EmployeeId)).ToListAsync(ct))
            .GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Id).First());
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        var items = await ItemsAsync(true, ct);
        var total = 1 + (settings.AskBankDetails ? 1 : 0) + items.Count;
        return people.Select(p =>
        {
            var inv = invites.GetValueOrDefault(p.Id);
            var done = inv is null ? 0 : (inv.PersonalSavedAtUtc is null ? 0 : 1) + (settings.AskBankDetails && inv.BankSavedAtUtc is not null ? 1 : 0)
                + inv.Responses.Count(r => !r.IsDeleted && items.Any(it => it.Id == r.ItemId));
            return new PreboardingJoinerRow(p.Id, p.FullName, p.EmployeeCode, p.Department?.Name, p.DateOfJoining, p.PersonalEmail, StateOf(inv, now), inv?.Id,
                inv?.SentAtUtc, inv?.SubmittedAtUtc, done, total);
        }).ToList();
    }

    public async Task<PreboardingStatus?> StatusAsync(int employeeId, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null) return null;
        var invite = await db.PreboardingInvites.AsNoTracking().Include(i => i.Responses).Where(i => i.EmployeeId == employeeId).OrderByDescending(i => i.Id).FirstOrDefaultAsync(ct);
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        return new PreboardingStatus
        {
            Employee = e, Invite = invite, State = StateOf(invite, clock.UtcNow), AskBank = settings.AskBankDetails, AskPan = settings.AskPan,
            Steps = await StepsAsync(invite, settings, ct)
        };
    }

    private async Task<List<PreboardingStepRow>> StepsAsync(PreboardingInvite? invite, PreboardingSettings settings, CancellationToken ct)
    {
        var items = await ItemsAsync(true, ct);
        var responses = invite?.Responses.Where(r => !r.IsDeleted).ToDictionary(r => r.ItemId) ?? [];
        List<PreboardingStepRow> steps = [new("Personal details", true, invite?.PersonalSavedAtUtc is not null, invite?.PersonalSavedAtUtc, null, null, null, null, null)];
        if (settings.AskBankDetails) steps.Add(new("Bank account for salary", true, invite?.BankSavedAtUtc is not null, invite?.BankSavedAtUtc, null, null, null, null, null));
        steps.AddRange(items.Select(it =>
        {
            var r = responses.GetValueOrDefault(it.Id);
            return new PreboardingStepRow(it.Title, it.IsRequired, r is not null, r?.CompletedAtUtc, r?.EmployeeDocumentId, it.Id, it.Kind, it.Description, it.DocumentCategory);
        }));
        return steps;
    }

    public async Task<Result<string>> SendAsync(int employeeId, string address, string baseUrl, CancellationToken ct = default)
    {
        var to = (address ?? string.Empty).Trim().ToLowerInvariant();
        if (to.Length is 0 or > 200 || !new EmailAddressAttribute().IsValid(to)) return Result<string>.Fail("Enter the new hire's personal email address.");
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (e is null || !e.IsCurrent) return Result<string>.Fail("Employee not found.");
        var today = await TodayAsync(ct);
        if (e.DateOfJoining < today) return Result<string>.Fail("Pre-boarding is for people who haven't joined yet.");
        var now = clock.UtcNow;
        if (await db.PreboardingInvites.CountAsync(i => i.EmployeeId == employeeId && i.SentAtUtc >= now.AddDays(-1), ct) >= MaxSendsPerDay)
            return Result<string>.Fail($"At most {MaxSendsPerDay} links a day can be sent to one person. Try again tomorrow.");

        var settings = await SettingsAsync(ct);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == context.OrgId()).Select(o => new { o.Name }).FirstAsync(ct);
        // Earlier links stop working; answers already given are carried over to the new link.
        var previous = await db.PreboardingInvites.Include(i => i.Responses).Where(i => i.EmployeeId == employeeId).OrderByDescending(i => i.Id).ToListAsync(ct);
        foreach (var old in previous.Where(i => i.RevokedAtUtc is null)) old.RevokedAtUtc = now;
        var last = previous.FirstOrDefault();

        var token = NewToken();
        var invite = new PreboardingInvite
        {
            EmployeeId = employeeId, Email = to, TokenHash = HashToken(token), SentAtUtc = now, SentByUserId = context.UserId,
            ExpiresAtUtc = e.DateOfJoining.AddDays(settings.LinkDaysAfterJoining + 1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            PersonalSavedAtUtc = last?.PersonalSavedAtUtc, BankSavedAtUtc = last?.BankSavedAtUtc
        };
        foreach (var r in last?.Responses.Where(r => !r.IsDeleted) ?? [])
            invite.Responses.Add(new PreboardingResponse { ItemId = r.ItemId, EmployeeDocumentId = r.EmployeeDocumentId, CompletedAtUtc = r.CompletedAtUtc });
        db.PreboardingInvites.Add(invite);
        if (string.IsNullOrWhiteSpace(e.PersonalEmail)) e.PersonalEmail = to;
        audit.Add("Preboarding.LinkSent", "Employee", employeeId.ToString(), $"Pre-boarding link sent to {to}.");
        await db.SaveChangesAsync(ct);

        var link = $"{baseUrl.TrimEnd('/')}/welcome?token={token}";
        await email.SendAsync(new EmailMessage(to, $"Welcome to {org.Name}! A few things before your first day",
            $"<p>Hello {E(e.FirstName)},</p>" +
            $"<p>We're looking forward to having you at <strong>{E(org.Name)}</strong> from <strong>{e.DateOfJoining:dd MMM yyyy}</strong>.</p>" +
            "<p>Please use the link below to share your details and documents before you join. It takes about ten minutes.</p>" +
            $"<p><a href=\"{E(link)}\">Open your welcome page</a></p>" +
            $"<p>The link is personal: please don't forward it. It works until {invite.ExpiresAtUtc.AddDays(-1):dd MMM yyyy}.</p>", "Preboarding", context.OrgId()), ct);
        return Result<string>.Ok(link);
    }

    public async Task<Result> RevokeAsync(int employeeId, CancellationToken ct = default)
    {
        var open = await db.PreboardingInvites.Where(i => i.EmployeeId == employeeId && i.RevokedAtUtc == null).ToListAsync(ct);
        if (open.Count == 0) return Result.Fail("There is no active link to cancel.");
        foreach (var i in open) i.RevokedAtUtc = clock.UtcNow;
        audit.Add("Preboarding.LinkRevoked", "Employee", employeeId.ToString(), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReopenAsync(int employeeId, CancellationToken ct = default)
    {
        var invite = await db.PreboardingInvites.Where(i => i.EmployeeId == employeeId && i.RevokedAtUtc == null && i.SubmittedAtUtc != null)
            .OrderByDescending(i => i.Id).FirstOrDefaultAsync(ct);
        if (invite is null) return Result.Fail("Nothing submitted to reopen.");
        if (invite.ExpiresAtUtc <= clock.UtcNow) return Result.Fail("The link has expired. Send a new one instead.");
        invite.SubmittedAtUtc = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ================================================================== new hire

    /// <summary>
    /// Finds the invite behind a token across organizations, then runs <paramref name="work"/> inside that organization's scope.
    /// Returns null when the link is unknown, revoked, expired, the company is not operating or no longer has pre-boarding,
    /// or the person is no longer joining.
    /// </summary>
    private async Task<T?> WithInviteAsync<T>(string token, Func<PreboardingInvite, Task<T>> work, CancellationToken ct) where T : class
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = HashToken(token);
        var now = clock.UtcNow;
        int orgId, inviteId;
        using (context.BeginPlatformScope())
        {
            var row = await db.PreboardingInvites.AsNoTracking().Where(i => i.TokenHash == hash && i.RevokedAtUtc == null && i.ExpiresAtUtc > now)
                .Select(i => new { i.Id, i.OrganizationId }).FirstOrDefaultAsync(ct);
            if (row is null) return null;
            (orgId, inviteId) = (row.OrganizationId, row.Id);
        }
        if (!await features.IsEnabledAsync(orgId, FeatureCodes.Preboarding, ct)) return null;
        using var scope = context.BeginOrganizationScope(orgId);
        var invite = await db.PreboardingInvites.Include(i => i.Responses).Include(i => i.Employee).FirstOrDefaultAsync(i => i.Id == inviteId, ct);
        if (invite is null || !invite.Employee.IsCurrent) return null;
        return await work(invite);
    }

    private async Task<Result> WithOpenInviteAsync(string token, Func<PreboardingInvite, Task<Result>> work, CancellationToken ct) =>
        await WithInviteAsync(token, async invite => invite.SubmittedAtUtc is not null
            ? Result.Fail("You've already sent your details. To change something, contact HR.")
            : await work(invite), ct) ?? Result.Fail(Expired);

    public Task<PreboardingPortal?> PortalAsync(string token, CancellationToken ct = default) => WithInviteAsync(token, async invite =>
    {
        var now = clock.UtcNow;
        invite.FirstOpenedAtUtc ??= now;
        invite.LastOpenedAtUtc = now;
        await db.SaveChangesAsync(ct);

        var e = await db.Employees.AsNoTracking().Include(x => x.Department).Include(x => x.Designation).Include(x => x.Branch).Include(x => x.ReportingManager)
            .FirstAsync(x => x.Id == invite.EmployeeId, ct);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == invite.OrganizationId).Select(o => new { o.Name, o.LogoUrl }).FirstAsync(ct);
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        var bankLast4 = settings.AskBankDetails ? await db.EmployeeBankDetails.AsNoTracking().Where(b => b.EmployeeId == e.Id).Select(b => b.AccountNumberLast4).FirstOrDefaultAsync(ct) : null;
        var steps = await StepsAsync(invite, settings, ct);
        return new PreboardingPortal
        {
            OrganizationName = org.Name, LogoUrl = org.LogoUrl, FirstName = e.FirstName, DateOfJoining = e.DateOfJoining,
            Designation = e.Designation?.Name, Department = e.Department?.Name, ManagerName = e.ReportingManager?.FullName,
            Location = e.Branch is null ? null : string.Join(", ", new[] { e.Branch.Name, e.Branch.City }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Settings = settings, Steps = steps, PersonalDone = invite.PersonalSavedAtUtc is not null, BankDone = invite.BankSavedAtUtc is not null,
            BankLast4 = invite.BankSavedAtUtc is null ? null : bankLast4, PanMasked = settings.AskPan ? e.PanMasked : null,
            SubmittedAtUtc = invite.SubmittedAtUtc, ExpiresAtUtc = invite.ExpiresAtUtc,
            Personal = new PreboardingPersonalInput
            {
                Gender = e.Gender, DateOfBirth = e.DateOfBirth, MaritalStatus = e.MaritalStatus, BloodGroup = e.BloodGroup, CurrentAddress = e.CurrentAddress,
                PermanentAddress = e.PermanentAddress, EmergencyContactName = e.EmergencyContactName, EmergencyContactRelation = e.EmergencyContactRelation,
                EmergencyContactPhone = e.EmergencyContactPhone
            },
            CanSubmit = invite.SubmittedAtUtc is null && steps.All(s => s.Done || !s.Required)
        };
    }, ct);

    public Task<Result> SavePersonalAsync(string token, PreboardingPersonalInput i, CancellationToken ct = default) => WithOpenInviteAsync(token, async invite =>
    {
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        var e = invite.Employee;
        var r = await employees.UpdatePersonalAsync(e.Id, new PersonalInput
        {
            Gender = i.Gender, DateOfBirth = i.DateOfBirth, MaritalStatus = i.MaritalStatus, BloodGroup = i.BloodGroup, Nationality = e.Nationality,
            PersonalEmail = e.PersonalEmail ?? invite.Email, CurrentAddress = i.CurrentAddress,
            PermanentAddress = string.IsNullOrWhiteSpace(i.PermanentAddress) ? i.CurrentAddress : i.PermanentAddress,
            EmergencyContactName = i.EmergencyContactName, EmergencyContactRelation = i.EmergencyContactRelation, EmergencyContactPhone = i.EmergencyContactPhone,
            Uan = e.Uan, Pan = settings.AskPan ? i.Pan : null
        }, canEditSensitive: settings.AskPan, ct);
        if (!r.Succeeded) return r;
        invite.PersonalSavedAtUtc = clock.UtcNow;
        audit.Add("Preboarding.PersonalSaved", "Employee", e.Id.ToString(), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }, ct);

    public Task<Result> SaveBankAsync(string token, BankInput input, CancellationToken ct = default) => WithOpenInviteAsync(token, async invite =>
    {
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        if (!settings.AskBankDetails) return Result.Fail("Bank details aren't collected here.");
        var r = await employees.SaveBankAsync(invite.EmployeeId, input, ct);
        if (!r.Succeeded) return r;
        invite.BankSavedAtUtc = clock.UtcNow;
        audit.Add("Preboarding.BankSaved", "Employee", invite.EmployeeId.ToString(), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }, ct);

    public Task<Result> UploadAsync(string token, int itemId, UploadedFile file, CancellationToken ct = default) => WithOpenInviteAsync(token, async invite =>
    {
        var item = await db.PreboardingItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId && x.IsActive && x.Kind == PreboardingItemKind.Document, ct);
        if (item is null) return Result.Fail("That item isn't on your list.");
        var existing = invite.Responses.FirstOrDefault(r => r.ItemId == itemId && !r.IsDeleted);
        if (existing?.EmployeeDocumentId is int oldDoc && await db.EmployeeDocuments.AnyAsync(d => d.Id == oldDoc && d.IsVerified, ct))
            return Result.Fail("HR has already checked this document, so it can't be replaced here.");
        var up = await documents.UploadAsync(invite.EmployeeId, new DocumentUploadInput
        {
            Category = item.DocumentCategory, Title = item.Title, VisibleToEmployee = true, Notes = "Uploaded by the new hire during pre-boarding."
        }, file, ct);
        if (!up.Succeeded) return up;
        if (existing is null)
        {
            invite.Responses.Add(new PreboardingResponse { ItemId = itemId, EmployeeDocumentId = up.Value, CompletedAtUtc = clock.UtcNow });
        }
        else
        {
            // The replaced file was only ever theirs and unchecked; remove it so HR sees one current copy.
            if (existing.EmployeeDocumentId is int previous) await documents.DeleteAsync(previous, ct);
            existing.EmployeeDocumentId = up.Value;
            existing.CompletedAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }, ct);

    public Task<Result> AcknowledgeAsync(string token, int itemId, CancellationToken ct = default) => WithOpenInviteAsync(token, async invite =>
    {
        if (!await db.PreboardingItems.AnyAsync(x => x.Id == itemId && x.IsActive && x.Kind == PreboardingItemKind.Acknowledgement, ct))
            return Result.Fail("That item isn't on your list.");
        if (invite.Responses.Any(r => r.ItemId == itemId && !r.IsDeleted)) return Result.Ok();
        invite.Responses.Add(new PreboardingResponse { ItemId = itemId, CompletedAtUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }, ct);

    public Task<Result> SubmitAsync(string token, CancellationToken ct = default) => WithOpenInviteAsync(token, async invite =>
    {
        var settings = await db.PreboardingSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PreboardingSettings();
        var missing = (await StepsAsync(invite, settings, ct)).Where(s => s.Required && !s.Done).Select(s => s.Title).ToList();
        if (missing.Count > 0) return Result.Fail("Still to do: " + string.Join(", ", missing) + ".");
        invite.SubmittedAtUtc = clock.UtcNow;
        var e = invite.Employee;
        var to = (await notifications.UsersWithPermissionAsync(TenantPermissions.PreboardingManage, ct)).ToList();
        notifications.Queue(to, "Pre-boarding complete", $"{e.FullName} (joining {e.DateOfJoining:dd MMM}) sent their details and documents.", $"/org/preboarding/{e.Id}");
        audit.Add("Preboarding.Submitted", "Employee", e.Id.ToString(), null);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }, ct);
}
