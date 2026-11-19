using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Codenexify.HRMS.Application.Support;

/// <summary>Values that are not stored in the database (product name and website come from configuration).</summary>
public sealed record LegalSiteInfo(string ProductName, string? Website, string? SupportEmail = null);

public sealed record LegalPageView(string Slug, string Title, string? Summary, string Html, DateOnly EffectiveOn, int Version);
public sealed record LegalPageRow(int Id, string Slug, string Title, bool IsPublished, bool IsReviewed, DateOnly EffectiveOn, int Version, DateTime? UpdatedAtUtc, string? UpdatedBy);
public sealed record LegalLink(string Slug, string Title);

public sealed class LegalPageInput
{
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [StringLength(300)] public string? Summary { get; set; }
    [Required, StringLength(60000, MinimumLength = 50)] public string Body { get; set; } = string.Empty;
    [Display(Name = "Published")] public bool IsPublished { get; set; } = true;
    [Display(Name = "Effective from")] public DateOnly EffectiveOn { get; set; }
    /// <summary>Material change: new sign-ups accept the new version number.</summary>
    [Display(Name = "This is a new version (material change)")] public bool NewVersion { get; set; }
    [Display(Name = "Reviewed by our lawyer")] public bool IsReviewed { get; set; }
}

public sealed class LegalSettingsInput
{
    [StringLength(150), Display(Name = "Grievance Officer name")] public string? GrievanceOfficerName { get; set; }
    [EmailAddress, StringLength(200), Display(Name = "Grievance Officer email")] public string? GrievanceOfficerEmail { get; set; }
    [StringLength(100), Display(Name = "Courts at (city)")] public string? LegalJurisdictionCity { get; set; }
}

public interface ILegalPageService
{
    Task<LegalPageView?> GetPublishedAsync(string slug, LegalSiteInfo site, CancellationToken ct = default);
    /// <summary>Published pages for footers (cached briefly).</summary>
    Task<IReadOnlyList<LegalLink>> LinksAsync(CancellationToken ct = default);
    /// <summary>Versions a new customer accepts at sign-up, e.g. "terms v2, privacy v1".</summary>
    Task<string> AcceptanceVersionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LegalPageRow>> ListAsync(CancellationToken ct = default);
    Task<(LegalPageInput Input, string Slug, int Version)?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result> SaveAsync(int id, LegalPageInput input, CancellationToken ct = default);
    Task<string> PreviewAsync(string body, DateOnly effectiveOn, LegalSiteInfo site, CancellationToken ct = default);
    Task<LegalSettingsInput> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(LegalSettingsInput input, CancellationToken ct = default);
    /// <summary>Placeholders that would print "not yet provided" on the public pages, with where to set them.</summary>
    Task<IReadOnlyList<(string Placeholder, string WhereToSet)>> MissingValuesAsync(LegalSiteInfo site, CancellationToken ct = default);
}

public sealed class LegalPageService(IAppDbContext db, IClock clock, IAuditWriter audit, IMemoryCache cache) : ILegalPageService
{
    private const string LinksKey = "legal:links";
    /// <summary>Printed for an empty setting. No brackets, so it also degrades cleanly inside a link.</summary>
    public const string Missing = "not yet provided";

    public async Task<LegalPageView?> GetPublishedAsync(string slug, LegalSiteInfo site, CancellationToken ct = default)
    {
        var s = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var page = await db.LegalPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == s && p.IsPublished, ct);
        if (page is null) return null;
        var values = await ValuesAsync(site, page.EffectiveOn, ct);
        return new LegalPageView(page.Slug, page.Title, page.Summary is null ? null : LegalMarkup.Fill(page.Summary, values),
            LegalMarkup.ToHtml(LegalMarkup.Fill(page.Body, values)), page.EffectiveOn, page.Version);
    }

    public async Task<IReadOnlyList<LegalLink>> LinksAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(LinksKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await db.LegalPages.AsNoTracking().Where(p => p.IsPublished).OrderBy(p => p.SortOrder)
                .Select(p => new LegalLink(p.Slug, p.Title)).ToListAsync(ct);
        }))!;

    public async Task<string> AcceptanceVersionsAsync(CancellationToken ct = default)
    {
        var pages = await db.LegalPages.AsNoTracking().Where(p => p.IsPublished && (p.Slug == LegalPageDefaults.Terms || p.Slug == LegalPageDefaults.Privacy))
            .Select(p => new { p.Slug, p.Version }).ToListAsync(ct);
        return string.Join(", ", pages.OrderByDescending(p => p.Slug == LegalPageDefaults.Terms).Select(p => $"{p.Slug} v{p.Version}"));
    }

    public async Task<IReadOnlyList<LegalPageRow>> ListAsync(CancellationToken ct = default) =>
        await db.LegalPages.AsNoTracking().OrderBy(p => p.SortOrder)
            .Select(p => new LegalPageRow(p.Id, p.Slug, p.Title, p.IsPublished, p.IsReviewed, p.EffectiveOn, p.Version, p.UpdatedAtUtc, p.UpdatedBy))
            .ToListAsync(ct);

    public async Task<(LegalPageInput Input, string Slug, int Version)?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.LegalPages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : (new LegalPageInput
        {
            Title = p.Title, Summary = p.Summary, Body = p.Body, IsPublished = p.IsPublished, EffectiveOn = p.EffectiveOn, IsReviewed = p.IsReviewed
        }, p.Slug, p.Version);
    }

    public async Task<Result> SaveAsync(int id, LegalPageInput input, CancellationToken ct = default)
    {
        var p = await db.LegalPages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return Result.Fail("Page not found.");
        if (LegalMarkup.UnknownPlaceholders(input.Body) is { Count: > 0 } unknown)
            return Result.Fail($"Unknown placeholder(s): {string.Join(", ", unknown)}. Use only the ones listed beside the editor.");
        if (input.NewVersion && input.EffectiveOn < clock.Today)
            return Result.Fail("A new version can't take effect in the past. Set 'Effective from' to today or later.");

        p.Title = input.Title.Trim();
        p.Summary = string.IsNullOrWhiteSpace(input.Summary) ? null : input.Summary.Trim();
        p.Body = input.Body.Replace("\r\n", "\n").Trim();
        p.IsPublished = input.IsPublished;
        p.EffectiveOn = input.EffectiveOn;
        p.IsReviewed = input.IsReviewed;
        if (input.NewVersion) p.Version++;
        audit.Add("LegalPage.Saved", nameof(LegalPage), p.Id.ToString(), $"{p.Slug} v{p.Version}{(input.NewVersion ? " (new version)" : "")}, published={p.IsPublished}");
        await db.SaveChangesAsync(ct);
        cache.Remove(LinksKey);
        return Result.Ok();
    }

    public async Task<string> PreviewAsync(string body, DateOnly effectiveOn, LegalSiteInfo site, CancellationToken ct = default) =>
        LegalMarkup.ToHtml(LegalMarkup.Fill(body ?? string.Empty, await ValuesAsync(site, effectiveOn, ct)));

    public async Task<LegalSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return new LegalSettingsInput { GrievanceOfficerName = s?.GrievanceOfficerName, GrievanceOfficerEmail = s?.GrievanceOfficerEmail, LegalJurisdictionCity = s?.LegalJurisdictionCity };
    }

    public async Task<Result> SaveSettingsAsync(LegalSettingsInput input, CancellationToken ct = default)
    {
        var s = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (s is null) { s = new PlatformSetting(); db.PlatformSettings.Add(s); }
        s.GrievanceOfficerName = Clean(input.GrievanceOfficerName);
        s.GrievanceOfficerEmail = Clean(input.GrievanceOfficerEmail)?.ToLowerInvariant();
        s.LegalJurisdictionCity = Clean(input.LegalJurisdictionCity);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public async Task<IReadOnlyList<(string Placeholder, string WhereToSet)>> MissingValuesAsync(LegalSiteInfo site, CancellationToken ct = default)
    {
        var values = await ValuesAsync(site, clock.Today, ct);
        var used = (await db.LegalPages.AsNoTracking().Where(p => p.IsPublished).Select(p => p.Body).ToListAsync(ct))
            .SelectMany(LegalMarkup.Placeholders).ToHashSet();
        return LegalMarkup.Known
            .Where(k => used.Contains(k.Key) && string.IsNullOrWhiteSpace(values.GetValueOrDefault(k.Key)))
            .Select(k => ($"{{{{{k.Key}}}}}", k.Value)).ToList();
    }

    private async Task<Dictionary<string, string?>> ValuesAsync(LegalSiteInfo site, DateOnly effectiveOn, CancellationToken ct)
    {
        var s = await db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new PlatformSetting();
        var address = string.Join(", ", new[] { s.AddressLine, s.City, string.Join(" ", new[] { s.State, s.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x))) }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        return new Dictionary<string, string?>
        {
            ["CompanyName"] = s.LegalName,
            ["ProductName"] = site.ProductName,
            ["Website"] = site.Website,
            ["SupportEmail"] = s.SupportEmail ?? s.BillingEmail ?? site.SupportEmail,
            ["BillingEmail"] = s.BillingEmail ?? s.SupportEmail ?? site.SupportEmail,
            ["Phone"] = s.Phone,
            ["Address"] = string.IsNullOrWhiteSpace(s.AddressLine) ? null : address + ", India",
            ["Gstin"] = s.Gstin,
            ["GrievanceOfficer"] = s.GrievanceOfficerName,
            ["GrievanceEmail"] = s.GrievanceOfficerEmail,
            ["Jurisdiction"] = s.LegalJurisdictionCity,
            ["EffectiveDate"] = effectiveOn.ToString("d MMMM yyyy"),
        };
    }
}

/// <summary>
/// A deliberately small, safe text format for policy pages. Everything is HTML-encoded first; only these are recognised:
/// "## Heading", "### Sub-heading", "- list item", blank line = new paragraph, **bold**, [text](https://… | mailto:… | /path).
/// </summary>
public static partial class LegalMarkup
{
    public static readonly IReadOnlyDictionary<string, string> Known = new Dictionary<string, string>
    {
        ["CompanyName"] = "Billing & email settings → Legal name",
        ["ProductName"] = "Settings file: Platform:ProductName",
        ["Website"] = "Settings file: Platform:CompanyWebsite",
        ["SupportEmail"] = "System & maintenance → Support email (else the billing email, else Platform:SupportEmail)",
        ["BillingEmail"] = "Billing & email settings → Billing email (else the support email)",
        ["Phone"] = "Billing & email settings → Phone",
        ["Address"] = "Billing & email settings → Address",
        ["Gstin"] = "Billing & email settings → GSTIN",
        ["GrievanceOfficer"] = "Legal pages → Grievance Officer name",
        ["GrievanceEmail"] = "Legal pages → Grievance Officer email",
        ["Jurisdiction"] = "Legal pages → Courts at (city)",
        ["EffectiveDate"] = "the page's 'Effective from' date",
    };

    [GeneratedRegex(@"\{\{\s*([A-Za-z]+)\s*\}\}")] private static partial Regex PlaceholderRegex();
    [GeneratedRegex(@"\*\*(.+?)\*\*")] private static partial Regex BoldRegex();
    [GeneratedRegex(@"\[([^\]]+)\]\(((?:https?://|mailto:|/)[^\s)""<>]*)\)")] private static partial Regex LinkRegex();
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]*)\)")] private static partial Regex AnyLinkRegex();

    public static IEnumerable<string> Placeholders(string body) =>
        PlaceholderRegex().Matches(body ?? string.Empty).Select(m => m.Groups[1].Value);

    public static IReadOnlyList<string> UnknownPlaceholders(string body) =>
        Placeholders(body).Where(p => !Known.ContainsKey(p)).Distinct().Select(p => $"{{{{{p}}}}}").ToList();

    public static string Fill(string body, IReadOnlyDictionary<string, string?> values) =>
        PlaceholderRegex().Replace(body ?? string.Empty, m =>
            values.TryGetValue(m.Groups[1].Value, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : LegalPageService.Missing);

    public static string ToHtml(string text)
    {
        var sb = new StringBuilder();
        var paragraph = new List<string>();
        var inList = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            sb.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>\n");
            paragraph.Clear();
        }
        void CloseList()
        {
            if (!inList) return;
            sb.Append("</ul>\n");
            inList = false;
        }

        foreach (var raw in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { FlushParagraph(); CloseList(); continue; }
            if (line.StartsWith("### ")) { FlushParagraph(); CloseList(); sb.Append("<h3>").Append(Inline(line[4..])).Append("</h3>\n"); continue; }
            if (line.StartsWith("## ")) { FlushParagraph(); CloseList(); sb.Append("<h2>").Append(Inline(line[3..])).Append("</h2>\n"); continue; }
            if (line.TrimStart().StartsWith("- "))
            {
                FlushParagraph();
                if (!inList) { sb.Append("<ul>\n"); inList = true; }
                sb.Append("<li>").Append(Inline(line.TrimStart()[2..])).Append("</li>\n");
                continue;
            }
            CloseList();
            paragraph.Add(line.Trim());
        }
        FlushParagraph();
        CloseList();
        return sb.ToString();
    }

    private static string Inline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = BoldRegex().Replace(encoded, "<strong>$1</strong>");
        encoded = LinkRegex().Replace(encoded, m =>
        {
            var url = m.Groups[2].Value;
            var external = url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
            return $"<a href=\"{url}\"{(external ? " rel=\"noopener\" target=\"_blank\"" : "")}>{m.Groups[1].Value}</a>";
        });
        // A link whose address isn't allowed (e.g. a missing email) is shown as plain text.
        return AnyLinkRegex().Replace(encoded, m => m.Value.Contains("<a ") ? m.Value : m.Groups[1].Value);
    }
}
