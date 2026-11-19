using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Platform;

/// <summary>
/// A public policy page (privacy, terms, refunds, contact) maintained in the Control Center. The body uses a small,
/// safe text format (headings, lists, bold, links) and placeholders such as {{CompanyName}} filled from settings.
/// </summary>
public class LegalPage : BaseEntity, IAuditable
{
    /// <summary>URL part, e.g. "privacy" → /privacy.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>One sentence for search engines and the page header.</summary>
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    /// <summary>Shown as "Effective from"; changing it starts a new version that new sign-ups accept.</summary>
    public DateOnly EffectiveOn { get; set; }
    public int Version { get; set; } = 1;
    /// <summary>False for the starter drafts until a Super Admin confirms a lawyer reviewed the text.</summary>
    public bool IsReviewed { get; set; }
    public int SortOrder { get; set; }
}
