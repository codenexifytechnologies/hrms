using Codenexify.HRMS.Domain.Billing;
using Codenexify.HRMS.Domain.Payroll;

namespace Codenexify.HRMS.Application.Documents;

/// <summary>Maps domain objects to renderer-neutral PDF models (same content as the on-screen printable pages).</summary>
public static class PdfModels
{
    public static PayslipPdf FromPayslip(Payroll.Payslip slip, string brandColor)
    {
        var d = slip.Detail;
        var e = slip.Employee;
        var month = new DateOnly(slip.Run.Year, slip.Run.Month, 1);
        var final = slip.Run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Paid;
        List<AmountLine> Lines(PayrollLineKind k) => d.Lines.Where(l => l.Kind == k).OrderBy(l => l.SortOrder).Select(l => new AmountLine(l.Name, l.Amount, l.FullAmount)).ToList();
        return new PayslipPdf(new Letterhead(slip.OrganizationName, slip.OrganizationAddress, brandColor, null), month.ToString("MMMM yyyy"), !final,
            [
                ("Employee", e.FullName), ("Employee ID", e.EmployeeCode),
                ("Designation", e.Designation?.Name ?? "—"), ("Department", e.Department?.Name ?? "—"),
                ("Date of joining", e.DateOfJoining.ToString("dd MMM yyyy")), ("PAN", e.PanMasked ?? "—"),
                ("UAN", e.Uan ?? "—"), ("Bank", slip.BankName is null ? "—" : $"{slip.BankName} ·· {slip.BankLast4}"),
                ("Paid / LOP days", $"{d.PaidDays:0.#} / {d.LopDays:0.#} of {d.DaysInMonth}"), ("Tax regime", d.Regime.ToString()),
            ],
            Lines(PayrollLineKind.Earning), Lines(PayrollLineKind.Deduction), Lines(PayrollLineKind.EmployerContribution),
            d.GrossEarnings, d.TotalDeductions, d.NetPay, d.ProjectedAnnualTax);
    }

    public static InvoicePdf FromInvoice(Invoice inv, PlatformSetting supplier, IReadOnlyList<CreditNote> credits)
    {
        var address = string.Join(", ", new[] { supplier.AddressLine, supplier.City, supplier.State, supplier.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var contact = string.Join(" · ", new[] { supplier.BillingEmail, supplier.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
        List<(string, decimal)> taxes = inv.IgstRate == 0
            ? [($"CGST @ {inv.CgstRate:0.##}%", inv.CgstAmount), ($"SGST @ {inv.SgstRate:0.##}%", inv.SgstAmount)]
            : [($"IGST @ {inv.IgstRate:0.##}%", inv.IgstAmount)];
        var status = inv.Status switch { InvoiceStatus.Cancelled => "CANCELLED", InvoiceStatus.Paid => "PAID", _ => "" };
        return new InvoicePdf(new Letterhead(supplier.LegalName, address.Length > 0 ? address : null, "#1F5EFF", contact.Length > 0 ? contact : null),
            supplier.Gstin, supplier.Pan, inv.Number, inv.InvoiceDate, inv.DueDate, status,
            inv.BillToName, string.Join(", ", new[] { inv.BillToAddress, inv.BillToState }.Where(x => !string.IsNullOrWhiteSpace(x))), inv.BillToGstin, inv.PlaceOfSupply,
            inv.PeriodStart is { } ps && inv.PeriodEnd is { } pe ? $"{ps:dd MMM yyyy} – {pe:dd MMM yyyy}" : null,
            inv.Lines.Select(l => new InvoicePdfLine(l.Description, l.SacCode, l.Quantity, l.UnitPrice, l.Amount)).ToList(),
            inv.Subtotal, taxes, inv.Total, inv.AmountCredited, inv.AmountPaid, inv.Status == InvoiceStatus.Cancelled ? 0 : inv.Balance,
            credits.Count == 0 ? null : string.Join(" · ", credits.Select(c => $"{c.Number} dated {c.Date:dd MMM yyyy} for {Money.Inr(c.Total)}")),
            string.IsNullOrWhiteSpace(inv.Notes) ? null : inv.Notes,
            inv.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid ? supplier.BankDetails : null);
    }
}
