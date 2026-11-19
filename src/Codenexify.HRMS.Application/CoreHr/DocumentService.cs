using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.CoreHr;

public sealed class DocumentUploadInput
{
    public DocumentCategory Category { get; set; } = DocumentCategory.Identity;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Display(Name = "Expires on")] public DateOnly? ExpiresOn { get; set; }
    [Display(Name = "Visible to the employee")] public bool VisibleToEmployee { get; set; } = true;
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed record UploadedFile(Stream Content, string FileName, long Length);
public sealed record DocumentDownload(Stream Content, string FileName, string ContentType);
public sealed record ExpiringDocumentRow(int DocumentId, int EmployeeId, string EmployeeName, string EmployeeCode, string Title, DocumentCategory Category, DateOnly ExpiresOn);

public interface IDocumentService
{
    Task<Result<int>> UploadAsync(int employeeId, DocumentUploadInput input, UploadedFile file, CancellationToken ct = default);
    /// <param name="restrictToEmployeeId">When set (self-service), only that employee's visible documents are returned.</param>
    Task<DocumentDownload?> OpenAsync(int documentId, int? restrictToEmployeeId, CancellationToken ct = default);
    Task<Result> VerifyAsync(int documentId, CancellationToken ct = default);
    Task<Result> DeleteAsync(int documentId, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeDocument>> ForEmployeeAsync(int employeeId, bool visibleOnly, CancellationToken ct = default);
    Task<IReadOnlyList<ExpiringDocumentRow>> ExpiringAsync(int withinDays, CancellationToken ct = default);
}

public sealed class DocumentService(
    IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage, ISubscriptionService subscriptions) : IDocumentService
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [[0x50, 0x4B, 0x03, 0x04]]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [[0x50, 0x4B, 0x03, 0x04]]),
        [".doc"] = ("application/msword", [[0xD0, 0xCF, 0x11, 0xE0]]),
    };

    public static string AllowedExtensions => string.Join(", ", Allowed.Keys);

    private int OrgId => context.OrganizationId ?? throw new TenantIsolationException("No organization in the current context.");

    public async Task<Result<int>> UploadAsync(int employeeId, DocumentUploadInput input, UploadedFile file, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId, ct)) return Result<int>.Fail("Employee not found.");
        if (file.Length <= 0) return Result<int>.Fail("The file is empty.");
        if (file.Length > MaxBytes) return Result<int>.Fail($"Files can be at most {MaxBytes / 1024 / 1024} MB.");
        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(ext, out var kind)) return Result<int>.Fail($"Allowed file types: {AllowedExtensions}.");
        if (input.ExpiresOn is { } exp && exp < clock.Today.AddYears(-50)) return Result<int>.Fail("Expiry date looks wrong.");

        // Read into memory (bounded by MaxBytes) to check the real file signature, not just the extension.
        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.GetBuffer();
        if (!kind.Signatures.Any(sig => buffer.Length >= sig.Length && bytes.AsSpan(0, sig.Length).SequenceEqual(sig)))
            return Result<int>.Fail("The file content does not match its type.");

        var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
        var used = await db.EmployeeDocuments.SumAsync(d => (long?)d.SizeBytes, ct) ?? 0;
        if (used + buffer.Length > (long)limits.StorageLimitGb * 1024 * 1024 * 1024)
            return Result<int>.Fail("Your storage limit has been reached. Delete old documents or upgrade your plan.");

        buffer.Position = 0;
        var key = await storage.SaveAsync(OrgId, buffer, ext.ToLowerInvariant(), ct);
        var doc = new EmployeeDocument
        {
            EmployeeId = employeeId, Category = input.Category, Title = input.Title.Trim(),
            FileName = SafeFileName(file.FileName), StorageKey = key, ContentType = kind.ContentType, SizeBytes = buffer.Length,
            ExpiresOn = input.ExpiresOn, VisibleToEmployee = input.VisibleToEmployee, Notes = input.Notes?.Trim()
        };
        db.EmployeeDocuments.Add(doc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, ct); // don't leave orphan files behind
            throw;
        }
        await RecalculateUsageAsync(ct);
        return Result<int>.Ok(doc.Id);
    }

    private static string SafeFileName(string name)
    {
        var file = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
        return file.Length > 200 ? file[^200..] : file;
    }

    public async Task<DocumentDownload?> OpenAsync(int documentId, int? restrictToEmployeeId, CancellationToken ct = default)
    {
        var q = db.EmployeeDocuments.AsNoTracking().Where(d => d.Id == documentId); // tenant-filtered
        if (restrictToEmployeeId is int emp) q = q.Where(d => d.EmployeeId == emp && d.VisibleToEmployee);
        var doc = await q.FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        var stream = await storage.OpenReadAsync(doc.StorageKey, ct);
        return stream is null ? null : new DocumentDownload(stream, doc.FileName, doc.ContentType);
    }

    public async Task<Result> VerifyAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result.Fail("Document not found.");
        doc.IsVerified = true;
        doc.VerifiedBy = context.UserName;
        doc.VerifiedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result.Fail("Document not found.");
        db.EmployeeDocuments.Remove(doc); // soft delete keeps the record for the audit trail
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(doc.StorageKey, ct);
        await RecalculateUsageAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<EmployeeDocument>> ForEmployeeAsync(int employeeId, bool visibleOnly, CancellationToken ct = default) =>
        await db.EmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && (!visibleOnly || d.VisibleToEmployee))
            .OrderBy(d => d.Category).ThenBy(d => d.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<ExpiringDocumentRow>> ExpiringAsync(int withinDays, CancellationToken ct = default)
    {
        var until = clock.Today.AddDays(withinDays);
        return await db.EmployeeDocuments.AsNoTracking()
            .Where(d => d.ExpiresOn != null && d.ExpiresOn <= until && d.Employee.Status != EmployeeStatus.Exited)
            .OrderBy(d => d.ExpiresOn)
            .Select(d => new ExpiringDocumentRow(d.Id, d.EmployeeId, d.Employee.FirstName + " " + d.Employee.LastName, d.Employee.EmployeeCode, d.Title, d.Category, d.ExpiresOn!.Value))
            .ToListAsync(ct);
    }

    private async Task RecalculateUsageAsync(CancellationToken ct)
    {
        var bytes = await db.EmployeeDocuments.SumAsync(d => (long?)d.SizeBytes, ct) ?? 0;
        var org = await db.Organizations.FirstAsync(o => o.Id == OrgId, ct);
        org.StorageUsedMb = (int)Math.Ceiling(bytes / 1024d / 1024d);
        await db.SaveChangesAsync(ct);
    }
}
