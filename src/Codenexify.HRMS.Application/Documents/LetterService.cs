using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Documents;

public sealed class LetterTemplateInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    public LetterKind Kind { get; set; } = LetterKind.Other;
    [Required, StringLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(8000, MinimumLength = 20)] public string Body { get; set; } = string.Empty;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed class LetterIssueInput
{
    [Required, Display(Name = "Template")] public int TemplateId { get; set; }
    [Required, StringLength(150), Display(Name = "Signed by")] public string SignatoryName { get; set; } = string.Empty;
    [StringLength(150), Display(Name = "Signatory designation")] public string? SignatoryDesignation { get; set; } = "Human Resources";
    [Display(Name = "Letter date")] public DateOnly? LetterDate { get; set; }
    [Display(Name = "Share with the employee (visible in their documents)")] public bool ShareWithEmployee { get; set; } = true;
    /// <summary>Optional final text edited after preview; must not contain unresolved placeholders.</summary>
    [StringLength(200)] public string? Subject { get; set; }
    [StringLength(8000)] public string? Body { get; set; }
}

public sealed record PlaceholderInfo(string Key, string Description);
public sealed record LetterTemplateRow(int Id, string Name, LetterKind Kind, string Subject, bool IsActive, int IssuedCount);
/// <param name="Subject">Editable subject (the letter number stays as {{Letter.Number}} until issue).</param>
/// <param name="Body">Editable body (same rule).</param>
public sealed record LetterPreview(string Subject, string Body, string SubjectDisplay, string BodyHtml, IReadOnlyList<string> Missing, string NumberPreview);
public sealed record LetterRow(int Id, int EmployeeId, string EmployeeName, string EmployeeCode, LetterKind Kind, string Number, string Subject,
    DateOnly IssuedOn, string IssuedByName, bool Shared, DateTime? RevokedAtUtc, string? RevokedReason, int DocumentId);

public interface ILetterService
{
    IReadOnlyList<PlaceholderInfo> Placeholders { get; }
    Task<IReadOnlyList<LetterTemplateRow>> TemplatesAsync(bool activeOnly, CancellationToken ct = default);
    Task<LetterTemplateInput?> GetTemplateAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveTemplateAsync(int? id, LetterTemplateInput input, CancellationToken ct = default);
    Task<Result> DeleteTemplateAsync(int id, CancellationToken ct = default);
    Task<Result<LetterPreview>> PreviewAsync(int employeeId, LetterIssueInput input, CancellationToken ct = default);
    Task<Result<int>> IssueAsync(int employeeId, LetterIssueInput input, CancellationToken ct = default);
    Task<Result> RevokeAsync(int letterId, string reason, CancellationToken ct = default);
    Task<PagedResult<LetterRow>> ListAsync(int? employeeId, string? search, int page, CancellationToken ct = default);
    /// <summary>Stored PDF document id of an issued letter (in the current organization).</summary>
    Task<int?> DocumentIdAsync(int letterId, CancellationToken ct = default);
}

public sealed partial class LetterService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage,
    ISubscriptionService subscriptions, IPdfRenderer pdf, IAuditWriter audit) : ILetterService
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z]+\.[A-Za-z]+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public IReadOnlyList<PlaceholderInfo> Placeholders { get; } =
    [
        new("Employee.FullName", "Full name"), new("Employee.FirstName", "First name"), new("Employee.Code", "Employee ID"),
        new("Employee.Designation", "Designation"), new("Employee.Department", "Department"), new("Employee.Branch", "Branch / location"),
        new("Employee.DateOfJoining", "Date of joining"), new("Employee.ProbationEndDate", "Probation end date"),
        new("Employee.ConfirmationDate", "Confirmation date"), new("Employee.LastWorkingDay", "Last working day (exit)"),
        new("Employee.Manager", "Reporting manager"), new("Employee.WorkEmail", "Work email"), new("Employee.Address", "Current address"),
        new("Employee.NoticePeriodDays", "Notice period (days)"), new("Employee.EmploymentType", "Employment type"),
        new("Salary.AnnualGross", "Annual gross salary (₹)"), new("Salary.MonthlyGross", "Monthly gross salary (₹)"), new("Salary.EffectiveFrom", "Salary effective from"),
        new("Company.Name", "Company name"), new("Company.LegalName", "Registered (legal) name"), new("Company.Address", "Company address"), new("Company.City", "Company city"),
        new("Letter.Date", "Letter date"), new("Letter.Number", "Letter reference number"),
        new("Signatory.Name", "Signed by"), new("Signatory.Designation", "Signatory's designation"),
    ];

    private int OrgId => context.OrgId();

    private static string Date(DateOnly? d) => d is { } x ? x.ToString("dd MMMM yyyy", Invariant) : "";

    // ------------------------------------------------------------------ templates

    public async Task<IReadOnlyList<LetterTemplateRow>> TemplatesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var q = db.LetterTemplates.AsNoTracking();
        if (activeOnly) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.Kind).ThenBy(t => t.Name)
            .Select(t => new LetterTemplateRow(t.Id, t.Name, t.Kind, t.Subject, t.IsActive, db.GeneratedLetters.Count(l => l.TemplateId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<LetterTemplateInput?> GetTemplateAsync(int id, CancellationToken ct = default) =>
        await db.LetterTemplates.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new LetterTemplateInput { Name = t.Name, Kind = t.Kind, Subject = t.Subject, Body = t.Body, IsActive = t.IsActive }).FirstOrDefaultAsync(ct);

    private IReadOnlyList<string> Unknown(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).Distinct()
            .Where(k => !Placeholders.Any(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList();

    public async Task<Result<int>> SaveTemplateAsync(int? id, LetterTemplateInput input, CancellationToken ct = default)
    {
        var unknown = Unknown(input.Subject + "\n" + input.Body);
        if (unknown.Count > 0) return Result<int>.Fail($"Unknown placeholder(s): {string.Join(", ", unknown.Select(u => "{{" + u + "}}"))}. Use the list beside the editor.");
        if (await db.LetterTemplates.AnyAsync(t => t.Name == input.Name.Trim() && t.Id != id, ct)) return Result<int>.Fail("A template with this name already exists.");
        var t = id is null ? new LetterTemplate() : await db.LetterTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result<int>.Fail("Template not found.");
        t.Name = input.Name.Trim(); t.Kind = input.Kind; t.Subject = input.Subject.Trim(); t.Body = input.Body.Trim(); t.IsActive = input.IsActive;
        if (id is null) db.LetterTemplates.Add(t);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(t.Id);
    }

    public async Task<Result> DeleteTemplateAsync(int id, CancellationToken ct = default)
    {
        var t = await db.LetterTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Fail("Template not found.");
        db.LetterTemplates.Remove(t); // soft delete: issued letters keep their final text
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ merge

    private async Task<Dictionary<string, string?>> ValuesAsync(Employee e, LetterIssueInput input, DateOnly letterDate, string number, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == OrgId, ct);
        var salary = await db.EmployeeSalaries.AsNoTracking().Where(s => s.EmployeeId == e.Id && s.EffectiveFrom <= letterDate)
            .OrderByDescending(s => s.EffectiveFrom).FirstOrDefaultAsync(ct)
            ?? await db.EmployeeSalaries.AsNoTracking().Where(s => s.EmployeeId == e.Id).OrderBy(s => s.EffectiveFrom).FirstOrDefaultAsync(ct);
        var exitDay = e.DateOfExit ?? await db.ExitRequests.AsNoTracking()
            .Where(x => x.EmployeeId == e.Id && (x.Status == ExitStatus.Approved || x.Status == ExitStatus.Completed))
            .OrderByDescending(x => x.Id).Select(x => x.ApprovedLastWorkingDay ?? (DateOnly?)x.RequestedLastWorkingDay).FirstOrDefaultAsync(ct);
        var address = string.Join(", ", new[] { org.AddressLine, org.City, org.State, org.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Employee.FullName"] = e.FullName, ["Employee.FirstName"] = e.FirstName, ["Employee.Code"] = e.EmployeeCode,
            ["Employee.Designation"] = e.Designation?.Name, ["Employee.Department"] = e.Department?.Name, ["Employee.Branch"] = e.Branch?.Name,
            ["Employee.DateOfJoining"] = Date(e.DateOfJoining), ["Employee.ProbationEndDate"] = Date(e.ProbationEndDate),
            ["Employee.ConfirmationDate"] = Date(e.ConfirmationDate), ["Employee.LastWorkingDay"] = Date(exitDay),
            ["Employee.Manager"] = e.ReportingManager?.FullName, ["Employee.WorkEmail"] = e.WorkEmail, ["Employee.Address"] = e.CurrentAddress,
            ["Employee.NoticePeriodDays"] = e.NoticePeriodDays.ToString(Invariant), ["Employee.EmploymentType"] = Regex.Replace(e.EmploymentType.ToString(), "(?<=[a-z])([A-Z])", " $1").ToLowerInvariant(),
            ["Salary.AnnualGross"] = salary is null ? null : Money.Inr(salary.AnnualGross),
            ["Salary.MonthlyGross"] = salary is null ? null : Money.Inr(Math.Round(salary.AnnualGross / 12, 2)),
            ["Salary.EffectiveFrom"] = salary is null ? null : Date(salary.EffectiveFrom),
            ["Company.Name"] = org.Name, ["Company.LegalName"] = org.LegalName ?? org.Name, ["Company.Address"] = address.Length > 0 ? address : null, ["Company.City"] = org.City,
            ["Letter.Date"] = Date(letterDate), ["Letter.Number"] = number,
            ["Signatory.Name"] = input.SignatoryName?.Trim(), ["Signatory.Designation"] = input.SignatoryDesignation?.Trim(),
        };
    }

    private string Merge(string text, IReadOnlyDictionary<string, string?> values, ISet<string> missing) =>
        PlaceholderRegex().Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            if (values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            missing.Add(Placeholders.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Description ?? key);
            return m.Value;
        });

    private Task<Employee?> EmployeeAsync(int id, CancellationToken ct) =>
        db.Employees.AsNoTracking().Include(e => e.Designation).Include(e => e.Department).Include(e => e.Branch).Include(e => e.ReportingManager)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    private async Task<(int Fy, int Seq, string Number)> NextNumberAsync(DateOnly date, CancellationToken ct)
    {
        var fy = date.Month >= 4 ? date.Year : date.Year - 1;
        var seq = (await db.GeneratedLetters.IgnoreQueryFilters().Where(l => l.OrganizationId == OrgId && l.FinancialYear == fy).MaxAsync(l => (int?)l.Sequence, ct) ?? 0) + 1;
        var code = await db.Organizations.Where(o => o.Id == OrgId).Select(o => o.Code).FirstAsync(ct);
        return (fy, seq, $"{code.ToUpperInvariant()}/HR/{fy % 100:00}-{(fy + 1) % 100:00}/{seq:0000}");
    }

    private async Task<Result<(Employee Employee, LetterTemplate Template, string Subject, string Body, IReadOnlyList<string> Missing, DateOnly Date, (int Fy, int Seq, string Number) No)>>
        BuildAsync(int employeeId, LetterIssueInput input, CancellationToken ct, bool keepNumberPlaceholder = false)
    {
        var e = await EmployeeAsync(employeeId, ct);
        if (e is null) return Result<(Employee, LetterTemplate, string, string, IReadOnlyList<string>, DateOnly, (int, int, string))>.Fail("Employee not found.");
        var t = await db.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.TemplateId && x.IsActive, ct);
        if (t is null) return Result<(Employee, LetterTemplate, string, string, IReadOnlyList<string>, DateOnly, (int, int, string))>.Fail("Choose an active template.");
        var date = input.LetterDate ?? (await db.OrgClockAsync(OrgId, clock, ct)).Today;
        var no = await NextNumberAsync(date, ct);
        var values = await ValuesAsync(e, input, date, no.Number, ct);
        if (keepNumberPlaceholder) values["Letter.Number"] = "{{Letter.Number}}";
        var missing = new SortedSet<string>();
        // Edited final text (from the preview step) takes precedence over the template.
        var subject = Merge(string.IsNullOrWhiteSpace(input.Subject) ? t.Subject : input.Subject, values, missing);
        var body = Merge(string.IsNullOrWhiteSpace(input.Body) ? t.Body : input.Body, values, missing);
        return Result<(Employee, LetterTemplate, string, string, IReadOnlyList<string>, DateOnly, (int, int, string))>.Ok((e, t, subject, body, missing.ToList(), date, no));
    }

    public async Task<Result<LetterPreview>> PreviewAsync(int employeeId, LetterIssueInput input, CancellationToken ct = default)
    {
        var r = await BuildAsync(employeeId, input, ct, keepNumberPlaceholder: true);
        if (!r.Succeeded) return Result<LetterPreview>.Fail(r.Error!);
        var v = r.Value;
        string Show(string s) => s.Replace("{{Letter.Number}}", v.No.Number);
        return Result<LetterPreview>.Ok(new LetterPreview(v.Subject, v.Body, Show(v.Subject), LetterMarkup.ToHtml(Show(v.Body)), v.Missing, v.No.Number));
    }

    // ------------------------------------------------------------------ issue

    public async Task<Result<int>> IssueAsync(int employeeId, LetterIssueInput input, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(input.Body) && Unknown(input.Body).Count > 0) return Result<int>.Fail("The edited text contains unknown placeholders.");
        var r = await BuildAsync(employeeId, input, ct);
        if (!r.Succeeded) return Result<int>.Fail(r.Error!);
        var (e, t, subject, body, missing, date, no) = r.Value;
        if (missing.Count > 0) return Result<int>.Fail($"Fill these details before issuing: {string.Join(", ", missing)}.");

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == OrgId, ct);
        var issuer = await db.Users.AsNoTracking().Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";
        var bytes = pdf.Letter(new LetterPdf(Head(org), no.Number, date, subject, body, input.SignatoryName.Trim(), input.SignatoryDesignation?.Trim(),
            $"{org.LegalName ?? org.Name} · Ref {no.Number}"));

        var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
        var used = await db.EmployeeDocuments.SumAsync(d => (long?)d.SizeBytes, ct) ?? 0;
        if (used + bytes.Length > (long)limits.StorageLimitGb * 1024 * 1024 * 1024)
            return Result<int>.Fail("Your storage limit has been reached. Delete old documents or upgrade your plan.");

        string key;
        using (var ms = new MemoryStream(bytes)) key = await storage.SaveAsync(OrgId, ms, ".pdf", ct);
        try
        {
            var doc = new EmployeeDocument
            {
                EmployeeId = e.Id, Category = DocumentCategory.Letter, Title = $"{subject} ({no.Number})"[..Math.Min(200, $"{subject} ({no.Number})".Length)],
                FileName = $"{t.Kind}-{e.EmployeeCode}-{no.Seq:0000}.pdf", StorageKey = key, ContentType = "application/pdf", SizeBytes = bytes.Length,
                VisibleToEmployee = input.ShareWithEmployee, IsVerified = true, VerifiedBy = issuer, VerifiedAtUtc = clock.UtcNow, Notes = "Issued from letter templates"
            };
            db.EmployeeDocuments.Add(doc);
            await db.SaveChangesAsync(ct);
            var letter = new GeneratedLetter
            {
                EmployeeId = e.Id, TemplateId = t.Id, Kind = t.Kind, Number = no.Number, FinancialYear = no.Fy, Sequence = no.Seq, Subject = subject, Body = body,
                IssuedOn = date, IssuedByName = issuer, SignatoryName = input.SignatoryName.Trim(), SignatoryDesignation = input.SignatoryDesignation?.Trim(),
                DocumentId = doc.Id, SharedWithEmployee = input.ShareWithEmployee
            };
            db.GeneratedLetters.Add(letter);
            audit.Add("Letter.Issue", nameof(GeneratedLetter), no.Number, $"{t.Kind} to {e.FullName} ({e.EmployeeCode})");
            await db.SaveChangesAsync(ct);
            var org2 = await db.Organizations.FirstAsync(o => o.Id == OrgId, ct);
            org2.StorageUsedMb = (int)Math.Ceiling(((await db.EmployeeDocuments.SumAsync(d => (long?)d.SizeBytes, ct)) ?? 0) / 1024d / 1024d);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(letter.Id);
        }
        catch (DbUpdateException)
        {
            await storage.DeleteAsync(key, ct);
            return Result<int>.Fail("Another letter was issued at the same moment. Please try again.");
        }
    }

    public static Letterhead Head(Domain.Platform.Organization org) => new(org.LegalName ?? org.Name,
        string.Join(", ", new[] { org.AddressLine, org.City, org.State, org.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } a ? a : null,
        org.BrandColor, string.Join(" · ", new[] { org.Email, org.Phone, org.Website }.Where(x => !string.IsNullOrWhiteSpace(x))));

    public async Task<Result> RevokeAsync(int letterId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason.");
        var l = await db.GeneratedLetters.FirstOrDefaultAsync(x => x.Id == letterId, ct);
        if (l is null) return Result.Fail("Letter not found.");
        if (l.RevokedAtUtc is not null) return Result.Fail("This letter is already revoked.");
        l.RevokedAtUtc = clock.UtcNow;
        l.RevokedReason = reason.Trim();
        // The PDF stays on record for HR but is withdrawn from the employee's documents.
        var doc = await db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == l.DocumentId, ct);
        if (doc is not null) { doc.VisibleToEmployee = false; doc.Title = ("REVOKED · " + doc.Title)[..Math.Min(200, ("REVOKED · " + doc.Title).Length)]; }
        audit.Add("Letter.Revoke", nameof(GeneratedLetter), l.Number, reason.Trim());
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<int?> DocumentIdAsync(int letterId, CancellationToken ct = default) =>
        await db.GeneratedLetters.Where(l => l.Id == letterId).Select(l => (int?)l.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<LetterRow>> ListAsync(int? employeeId, string? search, int page, CancellationToken ct = default)
    {
        const int size = 25;
        page = Math.Max(1, page);
        var q = db.GeneratedLetters.AsNoTracking().Join(db.Employees, l => l.EmployeeId, e => e.Id, (l, e) => new { l, e });
        if (employeeId is int id) q = q.Where(x => x.l.EmployeeId == id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.l.Number.Contains(s) || x.l.Subject.Contains(s) || (x.e.FirstName + " " + x.e.LastName).Contains(s) || x.e.EmployeeCode.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.l.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new { x.l, x.e.FirstName, x.e.MiddleName, x.e.LastName, x.e.EmployeeCode }).ToListAsync(ct);
        return new PagedResult<LetterRow>(rows.Select(x => new LetterRow(x.l.Id, x.l.EmployeeId,
            string.Join(' ', new[] { x.FirstName, x.MiddleName, x.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), x.EmployeeCode,
            x.l.Kind, x.l.Number, x.l.Subject, x.l.IssuedOn, x.l.IssuedByName, x.l.SharedWithEmployee, x.l.RevokedAtUtc, x.l.RevokedReason, x.l.DocumentId)).ToList(),
            total, page, size);
    }
}

/// <summary>Starting templates for every organization. Generic wording: HR must review them against company policy and law.</summary>
public static class LetterDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        foreach (var (name, kind, subject, body) in Templates)
            db.LetterTemplates.Add(new LetterTemplate { OrganizationId = organizationId, Name = name, Kind = kind, Subject = subject, Body = body });
    }

    private static readonly (string, LetterKind, string, string)[] Templates =
    [
        ("Offer letter", LetterKind.Offer, "Offer of employment — {{Employee.FullName}}",
            """
            Dear {{Employee.FirstName}},

            We are pleased to offer you the position of **{{Employee.Designation}}** in the {{Employee.Department}} team at {{Company.Name}}.

            - Date of joining: **{{Employee.DateOfJoining}}**
            - Location: {{Employee.Branch}}
            - Annual gross salary: **{{Salary.AnnualGross}}**
            - Notice period: {{Employee.NoticePeriodDays}} days

            This offer is subject to satisfactory verification of your documents and the terms of your appointment letter. Please sign and return a copy of this letter to confirm your acceptance.

            We look forward to welcoming you.
            """),
        ("Appointment letter", LetterKind.Appointment, "Letter of appointment — {{Employee.FullName}} ({{Employee.Code}})",
            """
            Dear {{Employee.FullName}},

            Further to your acceptance of our offer, we are pleased to appoint you as **{{Employee.Designation}}** with effect from **{{Employee.DateOfJoining}}**. Your employee ID is {{Employee.Code}} and you will report to {{Employee.Manager}}.

            You will be on probation until {{Employee.ProbationEndDate}}. Your annual gross salary is {{Salary.AnnualGross}}, payable monthly, subject to statutory deductions.

            Either party may end the employment by giving {{Employee.NoticePeriodDays}} days' notice in writing, as per company policy.

            You are expected to follow the company's policies and code of conduct as amended from time to time.
            """),
        ("Confirmation letter", LetterKind.Confirmation, "Confirmation of employment",
            """
            Dear {{Employee.FirstName}},

            We are pleased to inform you that, on successful completion of your probation, your employment as **{{Employee.Designation}}** is confirmed with effect from **{{Employee.ConfirmationDate}}**.

            All other terms of your appointment remain unchanged. Thank you for your contribution, and we wish you continued success with {{Company.Name}}.
            """),
        ("Experience letter", LetterKind.Experience, "Experience certificate",
            """
            **To whom it may concern**

            This is to certify that {{Employee.FullName}} (Employee ID {{Employee.Code}}) was employed with {{Company.LegalName}} from **{{Employee.DateOfJoining}}** to **{{Employee.LastWorkingDay}}**. At the time of leaving, {{Employee.FirstName}} held the position of {{Employee.Designation}} in the {{Employee.Department}} department.

            We wish {{Employee.FirstName}} every success in the future.
            """),
        ("Relieving letter", LetterKind.Relieving, "Relieving letter — {{Employee.FullName}}",
            """
            Dear {{Employee.FullName}},

            With reference to your resignation, we confirm that you have been relieved from your duties as **{{Employee.Designation}}** at the close of business on **{{Employee.LastWorkingDay}}**.

            Your full and final settlement will be processed as per company policy. We thank you for your contributions and wish you the very best.
            """),
    ];
}
