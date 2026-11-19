using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Codenexify.HRMS.Application.Documents;

/// <summary>Renders business documents to PDF. Implemented in Infrastructure (PDFsharp/MigraDoc).</summary>
public interface IPdfRenderer
{
    byte[] Letter(LetterPdf letter);
    byte[] Payslip(PayslipPdf payslip);
    byte[] Invoice(InvoicePdf invoice);
    byte[] Form16(Form16Pdf form);
}

/// <summary>Form 16 Part B (salary and tax computation). Part A is issued from the TRACES portal.</summary>
public sealed record Form16Pdf(Letterhead Employer, string? EmployerPan, string? EmployerTan, string EmployeeName, string EmployeeCode, string? EmployeePan,
    string? Designation, string AssessmentYear, string FinancialYear, string Period, string Regime, bool Provisional,
    IReadOnlyList<(string Label, decimal Amount, bool Bold)> Computation,
    IReadOnlyList<(string Month, decimal Paid, decimal Tds)> Monthly, decimal TotalTds);

public sealed record Letterhead(string Name, string? AddressLines, string BrandColor, string? ContactLine);

public sealed record LetterPdf(Letterhead Head, string Number, DateOnly Date, string Subject, string Body, string? SignatoryName, string? SignatoryDesignation, string? Footer,
    SignatureCertificate? Acceptance = null);

/// <summary>Electronic acceptance recorded in HRMS, printed as a certificate block at the end of the signed letter.</summary>
public sealed record SignatureCertificate(string SignerName, string EmployeeCode, string TypedName, string SignedAt, string? IpAddress, string Method,
    string DocumentHash, string Reference);

public sealed record AmountLine(string Name, decimal Amount, decimal? FullAmount = null);

public sealed record PayslipPdf(Letterhead Head, string Period, bool IsDraft, IReadOnlyList<(string Label, string Value)> Details,
    IReadOnlyList<AmountLine> Earnings, IReadOnlyList<AmountLine> Deductions, IReadOnlyList<AmountLine> EmployerContributions,
    decimal Gross, decimal TotalDeductions, decimal NetPay, decimal ProjectedAnnualTax);

public sealed record InvoicePdfLine(string Description, string Sac, decimal Quantity, decimal Rate, decimal Amount);

public sealed record InvoicePdf(Letterhead Supplier, string? SupplierGstin, string? SupplierPan, string Number, DateOnly Date, DateOnly Due, string Status,
    string BillToName, string? BillToAddress, string? BillToGstin, string? PlaceOfSupply, string? ServicePeriod, IReadOnlyList<InvoicePdfLine> Lines,
    decimal Taxable, IReadOnlyList<(string Label, decimal Amount)> Taxes, decimal Total, decimal Credited, decimal Paid, decimal Balance,
    string? CreditNotes, string? Notes, string? BankDetails);

/// <summary>
/// The small, safe markup used by letter templates: blank line = paragraph, "**bold**", "- " bullet lines.
/// No HTML is ever accepted from templates.
/// </summary>
public static partial class LetterMarkup
{
    public sealed record Run(string Text, bool Bold);
    public sealed record Block(IReadOnlyList<Run> Runs, bool Bullet);

    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldRegex();

    public static IReadOnlyList<Block> Parse(string text)
    {
        var blocks = new List<Block>();
        var paragraph = new List<string>();
        void Flush()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new Block(Runs(string.Join("\n", paragraph)), false));
            paragraph.Clear();
        }
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { Flush(); continue; }
            if (line.StartsWith("- ")) { Flush(); blocks.Add(new Block(Runs(line[2..].Trim()), true)); continue; }
            paragraph.Add(line);
        }
        Flush();
        return blocks;
    }

    private static IReadOnlyList<Run> Runs(string text)
    {
        var runs = new List<Run>();
        var last = 0;
        foreach (Match m in BoldRegex().Matches(text))
        {
            if (m.Index > last) runs.Add(new Run(text[last..m.Index], false));
            runs.Add(new Run(m.Groups[1].Value, true));
            last = m.Index + m.Length;
        }
        if (last < text.Length) runs.Add(new Run(text[last..], false));
        return runs;
    }

    /// <summary>HTML for on-screen previews: everything encoded, only our own tags emitted.</summary>
    public static string ToHtml(string text)
    {
        var sb = new StringBuilder();
        var inList = false;
        foreach (var b in Parse(text))
        {
            if (b.Bullet && !inList) { sb.Append("<ul>"); inList = true; }
            if (!b.Bullet && inList) { sb.Append("</ul>"); inList = false; }
            sb.Append(b.Bullet ? "<li>" : "<p>");
            foreach (var r in b.Runs)
            {
                var enc = WebUtility.HtmlEncode(r.Text).Replace("\n", "<br>");
                sb.Append(r.Bold ? $"<strong>{enc}</strong>" : enc);
            }
            sb.Append(b.Bullet ? "</li>" : "</p>");
        }
        if (inList) sb.Append("</ul>");
        return sb.ToString();
    }
}

public static class Money
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    public static string Inr(decimal v) => "₹" + v.ToString("N2", India);
    public static string N(decimal v) => v.ToString("N2", India);
}
