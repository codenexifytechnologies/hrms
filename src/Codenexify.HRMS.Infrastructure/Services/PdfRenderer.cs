using System.Globalization;
using Codenexify.HRMS.Application.Documents;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Codenexify.HRMS.Infrastructure.Services;

/// <summary>
/// PDF output with PDFsharp/MigraDoc. A4, Unicode text (₹ and Indian names render correctly).
/// Licence: keep the package pinned to 6.1.x (MIT). Releases from 6.5 are AGPL-3.0, which is not acceptable for this commercial SaaS.
/// </summary>
public sealed class MigraDocPdfRenderer : IPdfRenderer
{
    private const string Font = "Arial";
    private static readonly object FontLock = new();
    private static bool _fontsReady;

    public MigraDocPdfRenderer() => EnsureFonts();

    private static void EnsureFonts()
    {
        lock (FontLock)
        {
            if (_fontsReady) return;
            GlobalFontSettings.FontResolver ??= new SystemFontResolver();
            _fontsReady = true;
        }
    }

    // ------------------------------------------------------------------ shared layout

    private static Color Brand(string hex)
    {
        hex = (hex ?? "").TrimStart('#');
        return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, null, out var v)
            ? new Color((byte)(v >> 16), (byte)((v >> 8) & 0xFF), (byte)(v & 0xFF))
            : new Color(31, 94, 255); // Codenexify blue
    }

    private static Document NewDocument(string title)
    {
        var doc = new Document();
        doc.Info.Title = title;
        doc.Info.Author = "Codenexify HRMS";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = Font;
        normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        var section = doc.AddSection();
        section.PageSetup = doc.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        return doc;
    }

    private static void Head(Section s, Letterhead h, string? rightTitle = null, IEnumerable<string>? rightLines = null)
    {
        var t = s.AddTable();
        t.AddColumn(Unit.FromCentimeter(10.6));
        t.AddColumn(Unit.FromCentimeter(6));
        var row = t.AddRow();
        var left = row.Cells[0];
        var name = left.AddParagraph(h.Name);
        name.Format.Font.Size = 16;
        name.Format.Font.Bold = true;
        name.Format.Font.Color = Brand(h.BrandColor);
        if (!string.IsNullOrWhiteSpace(h.AddressLines)) Small(left.AddParagraph(h.AddressLines));
        if (!string.IsNullOrWhiteSpace(h.ContactLine)) Small(left.AddParagraph(h.ContactLine));
        var right = row.Cells[1];
        right.Format.Alignment = ParagraphAlignment.Right;
        if (rightTitle is not null)
        {
            var p = right.AddParagraph(rightTitle);
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 13;
        }
        foreach (var l in rightLines ?? []) Small(right.AddParagraph(l));
        var rule = s.AddParagraph();
        rule.Format.Borders.Bottom.Width = 1.2;
        rule.Format.Borders.Bottom.Color = Brand(h.BrandColor);
        rule.Format.SpaceAfter = Unit.FromPoint(12);
    }

    private static Paragraph Small(Paragraph p)
    {
        p.Format.Font.Size = 8.5;
        p.Format.Font.Color = new Color(71, 85, 105);
        p.Format.SpaceAfter = 0;
        return p;
    }

    private static void Footer(Section s, string text)
    {
        var p = s.Footers.Primary.AddParagraph(text);
        p.Format.Font.Size = 7.5;
        p.Format.Font.Color = new Color(100, 116, 139);
        p.Format.Alignment = ParagraphAlignment.Center;
        p.AddText("   ·   Page ");
        p.AddPageField();
        p.AddText(" of ");
        p.AddNumPagesField();
    }

    private static byte[] Render(Document doc)
    {
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static Table AmountTable(Section s, params double[] widthsCm)
    {
        var t = s.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(203, 213, 225);
        t.Format.Font.Size = 9;
        foreach (var w in widthsCm) t.AddColumn(Unit.FromCentimeter(w));
        return t;
    }

    private static Row HeaderRow(Table t, params string[] titles)
    {
        var r = t.AddRow();
        r.HeadingFormat = true;
        r.Shading.Color = new Color(241, 245, 249);
        r.Format.Font.Bold = true;
        for (var i = 0; i < titles.Length; i++) r.Cells[i].AddParagraph(titles[i]);
        return r;
    }

    private static void Cell(Row r, int i, string text, bool right = false, bool bold = false)
    {
        var p = r.Cells[i].AddParagraph(text);
        if (right) p.Format.Alignment = ParagraphAlignment.Right;
        if (bold) p.Format.Font.Bold = true;
    }

    // ------------------------------------------------------------------ letter

    public byte[] Letter(LetterPdf l)
    {
        var doc = NewDocument(l.Subject);
        var s = doc.LastSection!;
        Head(s, l.Head);
        var meta = s.AddTable();
        meta.AddColumn(Unit.FromCentimeter(8.3));
        meta.AddColumn(Unit.FromCentimeter(8.3));
        var mr = meta.AddRow();
        mr.Cells[0].AddParagraph($"Ref: {l.Number}");
        var date = mr.Cells[1].AddParagraph($"Date: {l.Date.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture)}");
        date.Format.Alignment = ParagraphAlignment.Right;
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(6);
        var subject = s.AddParagraph(l.Subject);
        subject.Format.Font.Bold = true;
        subject.Format.Font.Size = 11.5;
        subject.Format.SpaceAfter = Unit.FromPoint(10);

        foreach (var block in LetterMarkup.Parse(l.Body))
        {
            var p = s.AddParagraph();
            p.Format.SpaceAfter = Unit.FromPoint(block.Bullet ? 3 : 8);
            p.Format.Alignment = block.Bullet ? ParagraphAlignment.Left : ParagraphAlignment.Justify;
            p.Format.LineSpacingRule = LineSpacingRule.OnePtFive;
            if (block.Bullet)
            {
                p.Format.LeftIndent = Unit.FromCentimeter(0.8);
                p.Format.FirstLineIndent = Unit.FromCentimeter(-0.4);
                p.AddText("•  ");
            }
            foreach (var run in block.Runs)
            {
                var parts = run.Text.Split('\n');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0) p.AddLineBreak();
                    if (run.Bold) p.AddFormattedText(parts[i], TextFormat.Bold); else p.AddText(parts[i]);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(l.SignatoryName))
        {
            var sig = s.AddParagraph();
            sig.Format.SpaceBefore = Unit.FromPoint(28);
            sig.AddText($"For {l.Head.Name}");
            sig.AddLineBreak(); sig.AddLineBreak(); sig.AddLineBreak();
            sig.AddFormattedText(l.SignatoryName, TextFormat.Bold);
            if (!string.IsNullOrWhiteSpace(l.SignatoryDesignation)) { sig.AddLineBreak(); sig.AddText(l.SignatoryDesignation); }
        }
        if (l.Acceptance is { } a)
        {
            var accepted = s.AddParagraph();
            accepted.Format.SpaceBefore = Unit.FromPoint(22);
            accepted.AddText("Accepted electronically by");
            accepted.AddLineBreak();
            accepted.AddFormattedText(a.TypedName, TextFormat.Bold);
            accepted.AddLineBreak();
            accepted.AddText($"{a.SignerName} ({a.EmployeeCode}) · {a.SignedAt}");

            var cert = s.AddTable();
            cert.Format.Font.Size = 8;
            cert.Borders.Width = 0.5;
            cert.Borders.Color = Colors.LightGray;
            cert.TopPadding = cert.BottomPadding = Unit.FromPoint(2);
            cert.AddColumn(Unit.FromCentimeter(4));
            cert.AddColumn(Unit.FromCentimeter(12.6));
            var title = cert.AddRow();
            title.Cells[0].MergeRight = 1;
            title.Cells[0].Shading.Color = Colors.WhiteSmoke;
            title.Cells[0].AddParagraph("Electronic acceptance certificate").Format.Font.Bold = true;
            foreach (var (k, v) in new[]
            {
                ("Signer", $"{a.SignerName} ({a.EmployeeCode})"), ("Name typed", a.TypedName), ("Signed at", a.SignedAt), ("IP address", a.IpAddress ?? "—"),
                ("Verification", a.Method), ("Document SHA-256", a.DocumentHash), ("Reference", a.Reference)
            })
            {
                var r = cert.AddRow();
                r.Cells[0].AddParagraph(k);
                r.Cells[1].AddParagraph(v);
            }
            s.AddParagraph().Format.SpaceBefore = Unit.FromPoint(4);
            var note = s.AddParagraph("The SHA-256 fingerprint is of the letter's subject and text as issued. Any change to the text produces a different fingerprint.");
            note.Format.Font.Size = 7.5;
            note.Format.Font.Color = Colors.Gray;
        }
        Footer(s, l.Footer ?? $"{l.Head.Name} · {l.Number}");
        return Render(doc);
    }

    // ------------------------------------------------------------------ payslip

    public byte[] Payslip(PayslipPdf p)
    {
        var doc = NewDocument($"Payslip {p.Period}");
        var s = doc.LastSection!;
        Head(s, p.Head, "PAYSLIP", [p.Period, p.IsDraft ? "DRAFT — not final" : ""]);

        var info = s.AddTable();
        info.Format.Font.Size = 9;
        info.AddColumn(Unit.FromCentimeter(3.3)); info.AddColumn(Unit.FromCentimeter(5));
        info.AddColumn(Unit.FromCentimeter(3.3)); info.AddColumn(Unit.FromCentimeter(5));
        for (var i = 0; i < p.Details.Count; i += 2)
        {
            var r = info.AddRow();
            Cell(r, 0, p.Details[i].Label, bold: true); Cell(r, 1, p.Details[i].Value);
            if (i + 1 < p.Details.Count) { Cell(r, 2, p.Details[i + 1].Label, bold: true); Cell(r, 3, p.Details[i + 1].Value); }
        }
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);

        var t = AmountTable(s, 5.2, 2.6, 0.4, 5.6, 2.8);
        t.Borders.Visible = false;
        var hr = HeaderRow(t, "Earnings", "Amount", "", "Deductions", "Amount");
        hr.Cells[1].Format.Alignment = hr.Cells[4].Format.Alignment = ParagraphAlignment.Right;
        var rows = Math.Max(p.Earnings.Count, p.Deductions.Count);
        for (var i = 0; i < rows; i++)
        {
            var r = t.AddRow();
            r.Borders.Bottom.Width = 0.3;
            r.Borders.Bottom.Color = new Color(226, 232, 240);
            if (i < p.Earnings.Count) { Cell(r, 0, p.Earnings[i].Name); Cell(r, 1, Money.N(p.Earnings[i].Amount), right: true); }
            if (i < p.Deductions.Count) { Cell(r, 3, p.Deductions[i].Name); Cell(r, 4, Money.N(p.Deductions[i].Amount), right: true); }
        }
        var tot = t.AddRow();
        tot.Shading.Color = new Color(241, 245, 249);
        Cell(tot, 0, "Gross earnings", bold: true); Cell(tot, 1, Money.N(p.Gross), true, true);
        Cell(tot, 3, "Total deductions", bold: true); Cell(tot, 4, Money.N(p.TotalDeductions), true, true);

        var net = s.AddParagraph();
        net.Format.SpaceBefore = Unit.FromPoint(14);
        net.Format.Borders.Width = 1;
        net.Format.Borders.Color = Brand(p.Head.BrandColor);
        net.Format.Borders.Distance = Unit.FromPoint(6);
        net.Format.Font.Size = 13;
        net.AddFormattedText("Net pay:  ", TextFormat.Bold);
        net.AddFormattedText(Money.Inr(p.NetPay), TextFormat.Bold);

        if (p.EmployerContributions.Count > 0)
        {
            var e = s.AddParagraph("Employer contributions (not deducted from pay): " + string.Join(" · ", p.EmployerContributions.Select(c => $"{c.Name} {Money.Inr(c.Amount)}")));
            e.Format.SpaceBefore = Unit.FromPoint(12);
            Small(e);
        }
        if (p.ProjectedAnnualTax > 0) Small(s.AddParagraph($"Projected income tax for the year: {Money.Inr(p.ProjectedAnnualTax)}"));
        Footer(s, "This is a system-generated payslip and does not require a signature.");
        return Render(doc);
    }

    // ------------------------------------------------------------------ invoice

    public byte[] Invoice(InvoicePdf i)
    {
        var doc = NewDocument($"Invoice {i.Number}");
        var s = doc.LastSection!;
        var supplierIds = string.Join("   ", new[] { i.SupplierGstin is null ? null : $"GSTIN: {i.SupplierGstin}", i.SupplierPan is null ? null : $"PAN: {i.SupplierPan}" }.Where(x => x is not null));
        Head(s, i.Supplier with { ContactLine = string.Join(" · ", new[] { supplierIds, i.Supplier.ContactLine }.Where(x => !string.IsNullOrWhiteSpace(x))) },
            "TAX INVOICE", [$"No. {i.Number}", $"Date: {i.Date:dd MMM yyyy}", $"Due: {i.Due:dd MMM yyyy}", i.Status]);

        var parties = s.AddTable();
        parties.Format.Font.Size = 9;
        parties.AddColumn(Unit.FromCentimeter(9.6)); parties.AddColumn(Unit.FromCentimeter(7));
        var pr = parties.AddRow();
        var bill = pr.Cells[0];
        Small(bill.AddParagraph("BILL TO"));
        bill.AddParagraph(i.BillToName).Format.Font.Bold = true;
        if (!string.IsNullOrWhiteSpace(i.BillToAddress)) bill.AddParagraph(i.BillToAddress);
        bill.AddParagraph($"GSTIN: {i.BillToGstin ?? "Unregistered"}");
        var pos = pr.Cells[1];
        Small(pos.AddParagraph("PLACE OF SUPPLY"));
        pos.AddParagraph(i.PlaceOfSupply ?? "—");
        if (i.ServicePeriod is not null) { Small(pos.AddParagraph("SERVICE PERIOD")); pos.AddParagraph(i.ServicePeriod); }
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);

        var t = AmountTable(s, 0.8, 7.4, 1.6, 1.4, 2.6, 2.8);
        var hr = HeaderRow(t, "#", "Description", "SAC", "Qty", "Rate (₹)", "Taxable (₹)");
        for (var c = 3; c <= 5; c++) hr.Cells[c].Format.Alignment = ParagraphAlignment.Right;
        var n = 0;
        foreach (var l in i.Lines)
        {
            var r = t.AddRow();
            Cell(r, 0, (++n).ToString()); Cell(r, 1, l.Description); Cell(r, 2, l.Sac);
            Cell(r, 3, l.Quantity.ToString("0.##", CultureInfo.InvariantCulture), true); Cell(r, 4, Money.N(l.Rate), true); Cell(r, 5, Money.N(l.Amount), true);
        }
        void Total(string label, string value, bool bold = false)
        {
            var r = t.AddRow();
            r.Cells[0].MergeRight = 4;
            Cell(r, 0, label, right: true, bold: bold);
            Cell(r, 5, value, right: true, bold: bold);
        }
        Total("Taxable value", Money.N(i.Taxable));
        foreach (var (label, amount) in i.Taxes) Total(label, Money.N(amount));
        Total("Invoice total", Money.Inr(i.Total), bold: true);
        if (i.Credited > 0) Total("Less: credit notes", "− " + Money.N(i.Credited));
        if (i.Paid > 0) Total("Less: received", "− " + Money.N(i.Paid));
        if (i.Status != "CANCELLED") Total("Balance due", Money.Inr(i.Balance), bold: true);

        if (i.CreditNotes is not null) { var p = s.AddParagraph("Credit notes: " + i.CreditNotes); p.Format.SpaceBefore = Unit.FromPoint(8); p.Format.Font.Size = 9; }
        if (i.Notes is not null) { var p = s.AddParagraph("Notes: " + i.Notes); p.Format.SpaceBefore = Unit.FromPoint(6); p.Format.Font.Size = 9; }
        if (i.BankDetails is not null && i.Balance > 0)
        {
            var p = s.AddParagraph($"Pay by bank transfer: {i.BankDetails}. Quote invoice number {i.Number} as reference.");
            p.Format.SpaceBefore = Unit.FromPoint(8);
            p.Format.Font.Size = 9;
            p.Format.Borders.Width = 0.5;
            p.Format.Borders.Distance = Unit.FromPoint(4);
        }
        Footer(s, "Tax is not payable on reverse charge basis. This is a computer-generated invoice and does not require a signature.");
        return Render(doc);
    }

    // ------------------------------------------------------------------ Form 16 Part B

    public byte[] Form16(Form16Pdf f)
    {
        var doc = NewDocument($"Form 16 Part B — {f.EmployeeName} — AY {f.AssessmentYear}");
        var s = doc.LastSection!;
        Head(s, f.Employer, "FORM 16 — PART B", [$"Assessment year {f.AssessmentYear}", $"Financial year {f.FinancialYear}", f.Provisional ? "PROVISIONAL" : ""]);

        var info = s.AddTable();
        info.Format.Font.Size = 9;
        info.AddColumn(Unit.FromCentimeter(3.6)); info.AddColumn(Unit.FromCentimeter(4.7));
        info.AddColumn(Unit.FromCentimeter(3.6)); info.AddColumn(Unit.FromCentimeter(4.7));
        void Pair(string a, string? av, string b, string? bv)
        {
            var r = info.AddRow();
            Cell(r, 0, a, bold: true); Cell(r, 1, av ?? "—"); Cell(r, 2, b, bold: true); Cell(r, 3, bv ?? "—");
        }
        Pair("Employer TAN", f.EmployerTan, "Employer PAN", f.EmployerPan);
        Pair("Employee", f.EmployeeName, "Employee PAN", f.EmployeePan);
        Pair("Employee ID", f.EmployeeCode, "Designation", f.Designation);
        Pair("Period of employment", f.Period, "Tax regime", f.Regime);
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);

        var t = AmountTable(s, 12.6, 4);
        HeaderRow(t, "Details of salary paid and tax deducted", "Amount (₹)").Cells[1].Format.Alignment = ParagraphAlignment.Right;
        foreach (var (label, amount, bold) in f.Computation)
        {
            var r = t.AddRow();
            if (bold) r.Shading.Color = new Color(248, 250, 252);
            Cell(r, 0, label, bold: bold); Cell(r, 1, Money.N(amount), right: true, bold: bold);
        }
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(10);

        var head = s.AddParagraph("Month-wise salary and tax deducted at source");
        head.Format.Font.Bold = true;
        var m = AmountTable(s, 6.2, 5.2, 5.2);
        var mh = HeaderRow(m, "Month", "Amount paid (₹)", "TDS (₹)");
        mh.Cells[1].Format.Alignment = mh.Cells[2].Format.Alignment = ParagraphAlignment.Right;
        foreach (var (month, paid, tds) in f.Monthly)
        {
            var r = m.AddRow();
            Cell(r, 0, month); Cell(r, 1, Money.N(paid), true); Cell(r, 2, Money.N(tds), true);
        }
        var tr = m.AddRow();
        Cell(tr, 0, "Total", bold: true); Cell(tr, 1, Money.N(f.Monthly.Sum(x => x.Paid)), true, true); Cell(tr, 2, Money.N(f.TotalTds), true, true);

        var note = s.AddParagraph("Part A of Form 16 (TDS deposited and the TRACES certificate number) must be downloaded from the TRACES portal and issued together with this Part B." +
            (f.Provisional ? " This statement is provisional: the financial year is not complete or some months are not finalized." : ""));
        note.Format.SpaceBefore = Unit.FromPoint(12);
        Small(note);
        Footer(s, $"{f.Employer.Name} · Form 16 Part B · AY {f.AssessmentYear} · {f.EmployeeCode}");
        return Render(doc);
    }

    /// <summary>Loads a sans-serif TrueType font from the server: Arial on Windows, DejaVu Sans on Linux.</summary>
    private sealed class SystemFontResolver : IFontResolver
    {
        private static readonly string WinFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        private static readonly string[] Regular = [Path.Combine(WinFonts, "arial.ttf"), "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"];
        private static readonly string[] Bold = [Path.Combine(WinFonts, "arialbd.ttf"), "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"];
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> Cache = new();

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(isBold ? "sans-bold" : "sans");

        public byte[] GetFont(string faceName) => Cache.GetOrAdd(faceName, name =>
        {
            var path = (name == "sans-bold" ? Bold : Regular).FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                ?? throw new InvalidOperationException("No TrueType font found for PDF output. On Windows Arial is used; on Linux install DejaVu Sans (fonts-dejavu-core).");
            return File.ReadAllBytes(path);
        });
    }
}
