using System.Text;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Documents;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Infrastructure.Persistence;
using Codenexify.HRMS.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Codenexify.HRMS.Tests;

public sealed class LetterTests : IAsyncLifetime
{
    private TestHost _host = null!;
    private int _org, _other, _admin;

    public async Task InitializeAsync()
    {
        _host = await TestHost.CreateAsync();
        _org = await _host.ProvisionAsync("acme", "GROWTH");
        _other = await _host.ProvisionAsync("globex", "GROWTH");
        _host.Context.AsPlatform();
        _admin = await _host.RunAsync(sp => S<AppDbContext>(sp).Users.IgnoreQueryFilters().Where(u => u.OrganizationId == _org && u.IsOrganizationOwner).Select(u => u.Id).SingleAsync());
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private static T S<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

    private Task<T> Letters<T>(int org, int user, Func<ILetterService, Task<T>> a)
    {
        _host.Context.AsTenant(org, user);
        return _host.RunAsync(sp => a(S<ILetterService>(sp)));
    }

    private async Task<(int EmployeeId, int UserId)> EmployeeAsync(int org, string name)
    {
        _host.Context.AsTenant(org, _admin);
        var r = await _host.RunAsync(sp => S<IEmployeeService>(sp).CreateAsync(new EmployeeCreateInput
        {
            Employment = new EmploymentInput { FirstName = name, LastName = "Rao", WorkEmail = $"{name.ToLowerInvariant()}@{org}.test", DateOfJoining = new DateOnly(2023, 4, 3) },
            CreateLogin = true, InitialPassword = "Welcome@123"
        }));
        Assert.True(r.Succeeded, r.Error);
        var uid = await _host.RunAsync(sp => S<AppDbContext>(sp).Employees.Where(e => e.Id == r.Value).Select(e => e.UserId!.Value).SingleAsync());
        return (r.Value, uid);
    }

    private Task<int> TemplateAsync(int org, LetterKind kind) =>
        Letters(org, _admin, async l => (await l.TemplatesAsync(true)).First(t => t.Kind == kind).Id);

    [Fact]
    public void Markup_supports_paragraphs_bold_and_bullets_and_never_passes_html_through()
    {
        var html = LetterMarkup.ToHtml("Dear <b>Asha</b>,\n\nYou are **confirmed**.\n- First\n- Second <script>x</script>");
        Assert.Equal("<p>Dear &lt;b&gt;Asha&lt;/b&gt;,</p><p>You are <strong>confirmed</strong>.</p><ul><li>First</li><li>Second &lt;script&gt;x&lt;/script&gt;</li></ul>", html);
        var blocks = LetterMarkup.Parse("a\nb\n\nc");
        Assert.Equal(2, blocks.Count);
        Assert.Equal("a\nb", blocks[0].Runs.Single().Text);
    }

    [Fact]
    public async Task Organizations_start_with_templates_and_placeholders_are_validated()
    {
        var templates = await Letters(_org, _admin, l => l.TemplatesAsync(false));
        Assert.Equal(new[] { LetterKind.Offer, LetterKind.Appointment, LetterKind.Confirmation, LetterKind.Experience, LetterKind.Relieving }.Order(), templates.Select(t => t.Kind).Order());

        var bad = await Letters(_org, _admin, l => l.SaveTemplateAsync(null, new LetterTemplateInput { Name = "Bad", Subject = "Hi {{Employee.Salary}}", Body = "Dear {{Employee.FirstName}}, welcome aboard to the company." }));
        Assert.False(bad.Succeeded);
        Assert.Contains("{{Employee.Salary}}", bad.Error);
        var dup = await Letters(_org, _admin, l => l.SaveTemplateAsync(null, new LetterTemplateInput { Name = "Offer letter", Subject = "x", Body = "Dear {{Employee.FirstName}}, welcome aboard to the company." }));
        Assert.False(dup.Succeeded);
        Assert.True((await Letters(_org, _admin, l => l.SaveTemplateAsync(null, new LetterTemplateInput { Name = "Address proof", Kind = LetterKind.Other, Subject = "Address proof", Body = "This certifies that {{Employee.FullName}} works with {{Company.Name}}." }))).Succeeded);
    }

    [Fact]
    public async Task Issuing_needs_complete_data_numbers_letters_and_files_the_pdf()
    {
        var (emp, user) = await EmployeeAsync(_org, "Asha");
        var relieving = await TemplateAsync(_org, LetterKind.Relieving);
        var input = new LetterIssueInput { TemplateId = relieving, SignatoryName = "Priya HR", SignatoryDesignation = "HR Manager" };

        // No exit yet: the last working day is missing, so the letter can't be issued.
        var preview = await Letters(_org, _admin, l => l.PreviewAsync(emp, input));
        Assert.Contains("Last working day (exit)", preview.Value!.Missing);
        Assert.Contains("{{Letter.Number}}", preview.Value.Body + preview.Value.Subject + "{{Letter.Number}}");
        var refused = await Letters(_org, _admin, l => l.IssueAsync(emp, input));
        Assert.False(refused.Succeeded);
        Assert.Contains("Last working day", refused.Error);

        _host.Context.AsTenant(_org, _admin);
        await _host.RunAsync(async sp =>
        {
            var db = S<AppDbContext>(sp);
            var e = await db.Employees.SingleAsync(x => x.Id == emp);
            e.DateOfExit = new DateOnly(2026, 6, 30);
            e.Designation = new Designation { Name = "Accountant" };
            await db.SaveChangesAsync();
        });

        var first = await Letters(_org, _admin, l => l.IssueAsync(emp, input));
        Assert.True(first.Succeeded, first.Error);
        var second = await Letters(_org, _admin, l => l.IssueAsync(emp, input));
        var rows = (await Letters(_org, _admin, l => l.ListAsync(emp, null, 1))).Items;
        Assert.Equal(new[] { "ACME/HR/26-27/0002", "ACME/HR/26-27/0001" }, rows.Select(r => r.Number));
        Assert.Contains("30 June 2026", (await _host.RunAsync(sp => S<AppDbContext>(sp).GeneratedLetters.SingleAsync(l => l.Id == first.Value))).Body);

        // The PDF is stored in the employee's documents and the employee can open it.
        _host.Context.AsTenant(_org, user);
        var doc = await _host.RunAsync(sp => S<IDocumentService>(sp).ForEmployeeAsync(emp, visibleOnly: true));
        Assert.Equal(2, doc.Count(d => d.Category == DocumentCategory.Letter));
        var file = await _host.RunAsync(sp => S<IDocumentService>(sp).OpenAsync(rows[1].DocumentId, emp));
        using (var ms = new MemoryStream()) { await file!.Content.CopyToAsync(ms); Assert.Equal("%PDF", Encoding.ASCII.GetString(ms.ToArray(), 0, 4)); }

        // Revoking withdraws it from the employee but keeps HR's record; the number is never reused.
        Assert.True((await Letters(_org, _admin, l => l.RevokeAsync(second.Value, "Wrong date"))).Succeeded);
        _host.Context.AsTenant(_org, user);
        Assert.Single(await _host.RunAsync(sp => S<IDocumentService>(sp).ForEmployeeAsync(emp, visibleOnly: true)), d => d.Category == DocumentCategory.Letter);
        var third = await Letters(_org, _admin, l => l.IssueAsync(emp, input));
        Assert.Equal("ACME/HR/26-27/0003", (await Letters(_org, _admin, l => l.ListAsync(emp, null, 1))).Items[0].Number);
        Assert.True(third.Succeeded);
    }

    [Fact]
    public async Task Edited_text_is_issued_but_unknown_placeholders_are_rejected()
    {
        var (emp, _) = await EmployeeAsync(_org, "Ravi");
        var other = await TemplateAsync(_org, LetterKind.Confirmation);
        var input = new LetterIssueInput { TemplateId = other, SignatoryName = "Priya HR", Subject = "Note for {{Employee.FirstName}}", Body = "Dear {{Employee.FirstName}}, your ref is {{Letter.Number}}." };
        Assert.False((await Letters(_org, _admin, l => l.IssueAsync(emp, new LetterIssueInput { TemplateId = other, SignatoryName = "x", Body = "Hi {{Employee.Shoe}}" }))).Succeeded);
        var id = await Letters(_org, _admin, l => l.IssueAsync(emp, input));
        Assert.True(id.Succeeded, id.Error);
        _host.Context.AsTenant(_org, _admin);
        var letter = await _host.RunAsync(sp => S<AppDbContext>(sp).GeneratedLetters.SingleAsync(l => l.Id == id.Value));
        Assert.Equal(("Note for Ravi", "Dear Ravi, your ref is ACME/HR/26-27/0001."), (letter.Subject, letter.Body));
    }

    [Fact]
    public async Task Letters_and_templates_never_cross_organizations()
    {
        var (emp, _) = await EmployeeAsync(_org, "Asha");
        var otherTemplate = await TemplateAsync(_other, LetterKind.Confirmation);
        var ownTemplate = await TemplateAsync(_org, LetterKind.Confirmation);
        Assert.False((await Letters(_org, _admin, l => l.PreviewAsync(emp, new LetterIssueInput { TemplateId = otherTemplate, SignatoryName = "x" }))).Succeeded);

        _host.Context.AsTenant(_org, _admin);
        await _host.RunAsync(async sp => { var db = S<AppDbContext>(sp); (await db.Employees.SingleAsync(e => e.Id == emp)).ConfirmationDate = new DateOnly(2023, 10, 3); await db.SaveChangesAsync(); });
        var id = (await Letters(_org, _admin, l => l.IssueAsync(emp, new LetterIssueInput { TemplateId = ownTemplate, SignatoryName = "Priya" }))).Value;

        Assert.Empty((await Letters(_other, 0, l => l.ListAsync(null, null, 1))).Items);
        Assert.Null(await Letters(_other, 0, l => l.DocumentIdAsync(id)));
        Assert.False((await Letters(_other, 0, l => l.RevokeAsync(id, "no"))).Succeeded);
        Assert.False((await Letters(_other, 0, l => l.PreviewAsync(emp, new LetterIssueInput { TemplateId = otherTemplate, SignatoryName = "x" }))).Succeeded);
    }

    [Fact]
    public void Payslip_and_invoice_pdfs_render_with_rupee_amounts()
    {
        var pdf = new Codenexify.HRMS.Infrastructure.Services.MigraDocPdfRenderer();
        var head = new Letterhead("Acme Pvt Ltd", "Pune, Maharashtra", "#0f766e", "hr@acme.test");
        var slip = pdf.Payslip(new PayslipPdf(head, "June 2026", false, [("Employee", "Asha Rao"), ("Employee ID", "EMP0001")],
            [new AmountLine("Basic", 25000), new AmountLine("HRA", 10000)], [new AmountLine("PF", 1800)], [new AmountLine("PF (employer)", 1800)],
            35000, 1800, 33200, 0));
        var invoice = pdf.Invoice(new InvoicePdf(head, "27AAACC1234D1Z5", null, "CNX/26-27/00001", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8), "PAID",
            "Globex", "Bengaluru", "29ABCDE1234F1Z5", "Karnataka (29)", null, [new InvoicePdfLine("Foundation plan", "998314", 10, 99, 990)],
            990, [("IGST @ 18%", 178.20m)], 1168.20m, 0, 1168.20m, 0, null, null, null));
        // Set CNX_PDF_DUMP to a folder to inspect the generated samples.
        if (Environment.GetEnvironmentVariable("CNX_PDF_DUMP") is { Length: > 0 } dump)
        {
            File.WriteAllBytes(Path.Combine(dump, "payslip.pdf"), slip);
            File.WriteAllBytes(Path.Combine(dump, "invoice.pdf"), invoice);
        }
        foreach (var bytes in new[] { slip, invoice })
        {
            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
    }
}
