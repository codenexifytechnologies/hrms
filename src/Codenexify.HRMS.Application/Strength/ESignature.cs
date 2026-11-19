using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Billing;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Strength;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Strength;

public sealed record SignatureRow(int Id, int LetterId, string LetterNumber, string Subject, int EmployeeId, string EmployeeName, SignatureStatus Status,
    DateTime RequestedAtUtc, string RequestedBy, DateTime? SignedAtUtc, int? SignedDocumentId);
public sealed record SignatureView(SignatureRequest Request, string LetterNumber, string Subject, string BodyHtml, string EmployeeName, bool TextChanged,
    bool CodeSent, string? MaskedEmail);

public interface ISignatureService
{
    /// <summary>Asks the employee to accept an issued letter electronically.</summary>
    Task<Result<int>> RequestAsync(int letterId, string? message, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureRow>> ListAsync(SignatureStatus? status, CancellationToken ct = default);
    /// <summary>Latest request per letter (for showing status beside issued letters).</summary>
    Task<IReadOnlyDictionary<int, SignatureRow>> ForLettersAsync(IEnumerable<int> letterIds, CancellationToken ct = default);

    Task<IReadOnlyList<SignatureRow>> MineAsync(int employeeId, CancellationToken ct = default);
    Task<SignatureView?> ViewAsync(int id, int employeeId, CancellationToken ct = default);
    /// <summary>Emails a 6-digit code to the employee's login email (valid 10 minutes).</summary>
    Task<Result> SendCodeAsync(int id, int employeeId, CancellationToken ct = default);
    Task<Result> SignAsync(int id, int employeeId, string code, string typedName, bool agreed, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<Result> DeclineAsync(int id, int employeeId, string reason, CancellationToken ct = default);
}

public sealed class SignatureService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage, IPdfRenderer pdf,
    IEmailSender email, INotificationService notifications, IAuditWriter audit) : ISignatureService
{
    private const int CodeMinutes = 10;
    private const int MaxAttempts = 5;
    private const int MaxSends = 5;

    public static string Fingerprint(string subject, string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject.Trim() + "\n" + body.Trim()))).ToLowerInvariant();

    private static string HashCode(int id, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"esign:{id}:{code.Trim()}"))).ToLowerInvariant();

    private static string Normalize(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    private IQueryable<SignatureRow> Rows(IQueryable<SignatureRequest> q) =>
        q.Join(db.GeneratedLetters, s => s.GeneratedLetterId, l => l.Id, (s, l) => new { s, l })
         .Join(db.Employees, x => x.s.EmployeeId, e => e.Id, (x, e) => new SignatureRow(x.s.Id, x.l.Id, x.l.Number, x.l.Subject, e.Id, e.FirstName + " " + e.LastName,
             x.s.Status, x.s.CreatedAtUtc, x.s.RequestedBy, x.s.SignedAtUtc, x.s.SignedDocumentId));

    // ------------------------------------------------------------------ HR

    public async Task<Result<int>> RequestAsync(int letterId, string? message, CancellationToken ct = default)
    {
        var letter = await db.GeneratedLetters.AsNoTracking().FirstOrDefaultAsync(l => l.Id == letterId, ct);
        if (letter is null) return Result<int>.Fail("Letter not found.");
        if (letter.RevokedAtUtc is not null) return Result<int>.Fail("Revoked letters can't be signed.");
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == letter.EmployeeId, ct);
        if (emp.UserId is null || !await db.Users.AnyAsync(u => u.Id == emp.UserId && u.IsActive, ct))
            return Result<int>.Fail("The employee needs an active login to sign electronically.");
        if (await db.SignatureRequests.AnyAsync(s => s.GeneratedLetterId == letterId && (s.Status == SignatureStatus.Pending || s.Status == SignatureStatus.Signed), ct))
            return Result<int>.Fail("This letter is already waiting for, or has, a signature.");
        if (message is { Length: > 500 }) return Result<int>.Fail("The message can be at most 500 characters.");

        var request = new SignatureRequest
        {
            GeneratedLetterId = letterId, EmployeeId = emp.Id, DocumentHash = Fingerprint(letter.Subject, letter.Body), RequestedBy = await MyNameAsync(ct),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim()
        };
        db.SignatureRequests.Add(request);
        await db.SaveChangesAsync(ct);
        notifications.Queue([emp.UserId.Value], "Letter to sign", $"Please review and sign: {letter.Subject}.", $"/me/sign/{request.Id}");
        audit.Add("ESign.Request", nameof(SignatureRequest), request.Id.ToString(CultureInfo.InvariantCulture), $"{letter.Number} to {emp.FullName}");
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(request.Id);
    }

    public async Task<Result> CancelAsync(int id, CancellationToken ct = default)
    {
        var s = await db.SignatureRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result.Fail("Request not found.");
        if (s.Status != SignatureStatus.Pending) return Result.Fail("Only pending requests can be cancelled.");
        s.Status = SignatureStatus.Cancelled;
        s.OtpHash = null;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SignatureRow>> ListAsync(SignatureStatus? status, CancellationToken ct = default)
    {
        var q = db.SignatureRequests.AsNoTracking();
        if (status is { } st) q = q.Where(s => s.Status == st);
        return (await Rows(q.OrderByDescending(s => s.Id).Take(300)).ToListAsync(ct)).OrderByDescending(r => r.Id).ToList();
    }

    public async Task<IReadOnlyDictionary<int, SignatureRow>> ForLettersAsync(IEnumerable<int> letterIds, CancellationToken ct = default)
    {
        var ids = letterIds.Distinct().ToList();
        var rows = await Rows(db.SignatureRequests.AsNoTracking().Where(s => ids.Contains(s.GeneratedLetterId))).ToListAsync(ct);
        return rows.GroupBy(r => r.LetterId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
    }

    // ------------------------------------------------------------------ employee

    public async Task<IReadOnlyList<SignatureRow>> MineAsync(int employeeId, CancellationToken ct = default) =>
        (await Rows(db.SignatureRequests.AsNoTracking().Where(s => s.EmployeeId == employeeId && s.Status != SignatureStatus.Cancelled).OrderByDescending(s => s.Id).Take(100)).ToListAsync(ct))
            .OrderBy(r => r.Status).ThenByDescending(r => r.Id).ToList();

    private async Task<(SignatureRequest Request, GeneratedLetter Letter)?> LoadAsync(int id, int employeeId, bool track, CancellationToken ct)
    {
        var q = track ? db.SignatureRequests : db.SignatureRequests.AsNoTracking();
        var s = await q.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId && x.Status != SignatureStatus.Cancelled, ct);
        if (s is null) return null;
        var l = await db.GeneratedLetters.AsNoTracking().FirstAsync(x => x.Id == s.GeneratedLetterId, ct);
        return (s, l);
    }

    private static string? Mask(string? mail)
    {
        if (string.IsNullOrEmpty(mail) || mail.IndexOf('@') is var at && at < 1) return mail;
        return $"{mail[0]}{new string('•', Math.Max(1, at - 1))}{mail[at..]}";
    }

    public async Task<SignatureView?> ViewAsync(int id, int employeeId, CancellationToken ct = default)
    {
        if (await LoadAsync(id, employeeId, false, ct) is not var (s, l)) return null;
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId, ct);
        var mail = await db.Users.Where(u => u.Id == emp.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        return new SignatureView(s, l.Number, l.Subject, LetterMarkup.ToHtml(l.Body), emp.FullName,
            l.RevokedAtUtc is not null || Fingerprint(l.Subject, l.Body) != s.DocumentHash,
            s.OtpHash is not null && s.OtpExpiresAtUtc > clock.UtcNow, Mask(mail));
    }

    public async Task<Result> SendCodeAsync(int id, int employeeId, CancellationToken ct = default)
    {
        if (await LoadAsync(id, employeeId, true, ct) is not var (s, l)) return Result.Fail("Request not found.");
        if (s.Status != SignatureStatus.Pending) return Result.Fail("This letter is not waiting for your signature.");
        if (s.OtpSends >= MaxSends) return Result.Fail("Too many codes were requested. Ask HR to send a new signing request.");
        if (s.OtpSentAtUtc is { } last && clock.UtcNow - last < TimeSpan.FromSeconds(60)) return Result.Fail("A code was just sent. Wait a minute before asking for another.");
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId, ct);
        var mail = await db.Users.Where(u => u.Id == emp.UserId && u.IsActive).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (mail is null) return Result.Fail("Your login has no email address. Contact HR.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000", CultureInfo.InvariantCulture);
        s.OtpHash = HashCode(s.Id, code);
        s.OtpExpiresAtUtc = clock.UtcNow.AddMinutes(CodeMinutes);
        s.OtpSentAtUtc = clock.UtcNow;
        s.OtpSends++;
        s.OtpAttempts = 0;
        await db.SaveChangesAsync(ct);
        var sent = await email.SendAsync(new EmailMessage(mail, $"Your signing code: {code}",
            $"<p>Hello {WebUtility.HtmlEncode(emp.FirstName)},</p><p>Your code to sign <strong>{WebUtility.HtmlEncode(l.Subject)}</strong> ({WebUtility.HtmlEncode(l.Number)}) is:</p>" +
            $"<p style=\"font-size:24px;letter-spacing:4px\"><strong>{code}</strong></p><p>It expires in {CodeMinutes} minutes. If you didn't ask for it, tell HR.</p>",
            "esign", context.OrganizationId), ct);
        return sent ? Result.Ok() : Result.Fail("The code could not be emailed. Ask HR to check the email settings.");
    }

    public async Task<Result> SignAsync(int id, int employeeId, string code, string typedName, bool agreed, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (await LoadAsync(id, employeeId, true, ct) is not var (s, l)) return Result.Fail("Request not found.");
        if (s.Status != SignatureStatus.Pending) return Result.Fail("This letter is not waiting for your signature.");
        if (l.RevokedAtUtc is not null || Fingerprint(l.Subject, l.Body) != s.DocumentHash) return Result.Fail("The letter has changed or been withdrawn. Ask HR for a new request.");
        if (!agreed) return Result.Fail("Tick the box to confirm you have read and accept the letter.");
        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == employeeId, ct);
        if (string.IsNullOrWhiteSpace(typedName) || Normalize(typedName) != Normalize(emp.FullName))
            return Result.Fail($"Type your full name exactly as on record: {emp.FullName}.");
        if (s.OtpHash is null || s.OtpExpiresAtUtc is null || s.OtpExpiresAtUtc < clock.UtcNow) return Result.Fail("Send yourself a code first (codes expire after 10 minutes).");
        if (s.OtpAttempts >= MaxAttempts) return Result.Fail("Too many wrong codes. Send a new code.");
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashCode(s.Id, code ?? "")), Encoding.ASCII.GetBytes(s.OtpHash));
        if (!ok)
        {
            s.OtpAttempts++;
            if (s.OtpAttempts >= MaxAttempts) s.OtpHash = null;
            await db.SaveChangesAsync(ct);
            return Result.Fail("That code is not right.");
        }

        var clockOrg = await db.OrgClockAsync(context.OrgId(), clock, ct);
        var when = clockOrg.ToLocal(clock.UtcNow);
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == context.OrgId(), ct);
        var mail = await db.Users.Where(u => u.Id == emp.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        var certificate = new SignatureCertificate(emp.FullName, emp.EmployeeCode, typedName.Trim(),
            $"{when:dd MMM yyyy, HH:mm:ss} ({clockOrg.Zone.Id})", ipAddress, $"One-time code emailed to {Mask(mail)}", s.DocumentHash, $"ES-{s.Id:000000}");
        var bytes = pdf.Letter(new LetterPdf(LetterService.Head(org), l.Number, l.IssuedOn, l.Subject, l.Body, l.SignatoryName, l.SignatoryDesignation,
            $"{org.LegalName ?? org.Name} · Ref {l.Number} · signed electronically", certificate));

        string key;
        using (var ms = new MemoryStream(bytes)) key = await storage.SaveAsync(context.OrgId(), ms, ".pdf", ct);
        try
        {
            var title = $"Signed: {l.Subject} ({l.Number})";
            var doc = new EmployeeDocument
            {
                EmployeeId = employeeId, Category = DocumentCategory.Letter, Title = title[..Math.Min(200, title.Length)], FileName = $"Signed-{emp.EmployeeCode}-{l.Sequence:0000}.pdf",
                StorageKey = key, ContentType = "application/pdf", SizeBytes = bytes.Length, VisibleToEmployee = true, IsVerified = true,
                VerifiedBy = "Electronic signature", VerifiedAtUtc = clock.UtcNow, Notes = $"Accepted electronically, reference ES-{s.Id:000000}"
            };
            db.EmployeeDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            s.Status = SignatureStatus.Signed;
            s.SignedName = typedName.Trim();
            s.SignedAtUtc = clock.UtcNow;
            s.SignedIp = ipAddress?[..Math.Min(ipAddress.Length, 64)];
            s.SignedUserAgent = userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 300)];
            s.SignedDocumentId = doc.Id;
            s.OtpHash = null;
            audit.Add("ESign.Signed", nameof(SignatureRequest), s.Id.ToString(CultureInfo.InvariantCulture), $"{l.Number} signed by {emp.FullName} from {ipAddress}");
            notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.LettersManage, ct), "Letter signed", $"{emp.FullName} signed {l.Subject} ({l.Number}).", "/org/letters/signatures");
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, ct);
            throw;
        }
        return Result.Ok();
    }

    public async Task<Result> DeclineAsync(int id, int employeeId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return Result.Fail("Tell HR why (up to 500 characters).");
        if (await LoadAsync(id, employeeId, true, ct) is not var (s, l)) return Result.Fail("Request not found.");
        if (s.Status != SignatureStatus.Pending) return Result.Fail("This letter is not waiting for your signature.");
        s.Status = SignatureStatus.Declined;
        s.DeclineReason = reason.Trim();
        s.OtpHash = null;
        var name = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstAsync(ct);
        notifications.Queue(await notifications.UsersWithPermissionAsync(TenantPermissions.LettersManage, ct), "Letter declined", $"{name} declined to sign {l.Number}: {s.DeclineReason}", "/org/letters/signatures");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
