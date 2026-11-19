using Codenexify.HRMS.Application.Organizations;
using Codenexify.HRMS.Application.Support;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class LegalPageTests : IAsyncLifetime
{
    private static readonly LegalSiteInfo Site = new("Codenexify HRMS", "https://codenexify.test");
    private TestHost _host = null!;

    public async Task InitializeAsync() => _host = await TestHost.CreateAsync();
    public async Task DisposeAsync() => await _host.DisposeAsync();

    private Task<T> Legal<T>(Func<ILegalPageService, Task<T>> action, bool platform = false)
    {
        if (platform) _host.Context.AsPlatform(); else _host.Context.AsAnonymous();
        return _host.RunAsync(sp => action(sp.GetRequiredService<ILegalPageService>()));
    }

    [Fact]
    public async Task Starter_pages_are_seeded_once_published_and_marked_unreviewed()
    {
        var rows = await Legal(s => s.ListAsync(), platform: true);
        Assert.Equal(["privacy", "terms", "refund-policy", "contact"], rows.Select(r => r.Slug));
        Assert.All(rows, r => { Assert.True(r.IsPublished); Assert.False(r.IsReviewed); Assert.Equal(1, r.Version); });

        // Seeding again never duplicates or overwrites.
        await _host.RunAsync(sp => LegalPageDefaults.EnsureAsync(sp.GetRequiredService<AppDbContext>(), _host.Clock.Today));
        Assert.Equal(4, (await Legal(s => s.ListAsync(), platform: true)).Count);
        Assert.Equal(4, (await Legal(s => s.LinksAsync())).Count);
    }

    [Fact]
    public async Task Public_page_fills_settings_and_flags_missing_values()
    {
        var before = await Legal(s => s.GetPublishedAsync("privacy", Site));
        Assert.NotNull(before);
        Assert.Contains("Codenexify Technologies Private Limited", before!.Html);
        Assert.Contains(LegalPageService.Missing, before.Html); // grievance officer not set yet
        Assert.DoesNotContain("{{", before.Html);
        Assert.DoesNotContain("mailto:not yet", before.Html); // broken mailto shown as plain text

        var missing = await Legal(s => s.MissingValuesAsync(Site), platform: true);
        Assert.Contains(missing, m => m.Placeholder == "{{GrievanceOfficer}}");

        Assert.True((await Legal(s => s.SaveSettingsAsync(new LegalSettingsInput { GrievanceOfficerName = "Asha Rao", GrievanceOfficerEmail = "Grievance@Codenexify.test", LegalJurisdictionCity = "Pune" }), platform: true)).Succeeded);
        var after = await Legal(s => s.GetPublishedAsync("privacy", Site));
        Assert.Contains("Asha Rao", after!.Html);
        Assert.Contains("href=\"mailto:grievance@codenexify.test\"", after.Html);
        Assert.Contains("Pune", (await Legal(s => s.GetPublishedAsync("terms", Site)))!.Html);
        Assert.DoesNotContain(await Legal(s => s.MissingValuesAsync(Site), platform: true), m => m.Placeholder == "{{GrievanceOfficer}}");
        Assert.Null(await Legal(s => s.GetPublishedAsync("nope", Site)));
    }

    [Fact]
    public void Markup_is_html_safe_and_supports_the_small_format()
    {
        var html = LegalMarkup.ToHtml("## Title <script>\n\nHello **world** & [docs](https://x.test/a?b=1&c=2) [bad](javascript:alert(1))\n\n- one\n- two <img src=x onerror=1>\n### Sub");
        Assert.Contains("<h2>Title &lt;script&gt;</h2>", html);
        Assert.Contains("<strong>world</strong> &amp;", html);
        Assert.Contains("<a href=\"https://x.test/a?b=1&amp;c=2\" rel=\"noopener\" target=\"_blank\">docs</a>", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.Contains("<ul>\n<li>one</li>\n<li>two &lt;img src=x onerror=1&gt;</li>\n</ul>", html);
        Assert.Contains("<h3>Sub</h3>", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public async Task Editing_validates_placeholders_bumps_versions_and_can_hide_a_page()
    {
        var terms = (await Legal(s => s.ListAsync(), platform: true)).Single(r => r.Slug == "terms");
        var edit = (await Legal(s => s.GetForEditAsync(terms.Id), platform: true))!.Value.Input;

        edit.Body += "\n\nSee {{Typo}}.";
        Assert.Contains("{{Typo}}", (await Legal(s => s.SaveAsync(terms.Id, edit), platform: true)).Error);

        edit.Body = edit.Body.Replace("\n\nSee {{Typo}}.", "\n\n## 17. Extra\n\nNew clause.");
        edit.NewVersion = true;
        edit.EffectiveOn = _host.Clock.Today.AddDays(-1);
        Assert.Contains("past", (await Legal(s => s.SaveAsync(terms.Id, edit), platform: true)).Error);
        edit.EffectiveOn = _host.Clock.Today.AddDays(30);
        edit.IsReviewed = true;
        Assert.True((await Legal(s => s.SaveAsync(terms.Id, edit), platform: true)).Succeeded);

        var row = (await Legal(s => s.ListAsync(), platform: true)).Single(r => r.Slug == "terms");
        Assert.Equal(2, row.Version);
        Assert.True(row.IsReviewed);
        Assert.Contains("terms v2", await Legal(s => s.AcceptanceVersionsAsync()));
        Assert.Contains("New clause.", (await Legal(s => s.GetPublishedAsync("terms", Site)))!.Html);

        edit.NewVersion = false;
        edit.IsPublished = false;
        Assert.True((await Legal(s => s.SaveAsync(terms.Id, edit), platform: true)).Succeeded);
        Assert.Null(await Legal(s => s.GetPublishedAsync("terms", Site)));
        Assert.DoesNotContain(await Legal(s => s.LinksAsync()), l => l.Slug == "terms");
    }

    [Fact]
    public async Task Self_registration_requires_and_records_terms_acceptance()
    {
        var plan = await _host.PlanIdAsync("FOUNDATION");
        OrganizationRegistrationInput Input(bool accept) => new()
        {
            CompanyName = "Consent Co", Code = "consentco", CompanyEmail = "hr@consent.test", PlanId = plan,
            AdminName = "Admin", AdminEmail = "admin@consent.test", AdminPassword = "Passw0rd!x", AcceptTerms = accept
        };

        _host.Context.AsAnonymous();
        var refused = await _host.RunAsync(sp => sp.GetRequiredService<IProvisioningService>().ProvisionAsync(Input(false), true));
        Assert.False(refused.Succeeded);
        Assert.Contains("Terms", refused.Error);

        _host.Context.AsAnonymous();
        var ok = await _host.RunAsync(sp => sp.GetRequiredService<IProvisioningService>().ProvisionAsync(Input(true), true));
        Assert.True(ok.Succeeded, ok.Error);
        var org = await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Organizations.IgnoreQueryFilters().SingleAsync(o => o.Id == ok.Value));
        Assert.Equal(_host.Clock.UtcNow, org.TermsAcceptedAtUtc);
        Assert.Equal("terms v1, privacy v1", org.TermsAcceptedVersions);
        Assert.Equal("127.0.0.1", org.TermsAcceptedIp);

        // Created by Codenexify staff: no online acceptance recorded.
        var staff = await _host.ProvisionAsync("staffmade");
        Assert.Null(await _host.RunAsync(sp => sp.GetRequiredService<AppDbContext>().Organizations.IgnoreQueryFilters().Where(o => o.Id == staff).Select(o => o.TermsAcceptedAtUtc).SingleAsync()));
    }
}
