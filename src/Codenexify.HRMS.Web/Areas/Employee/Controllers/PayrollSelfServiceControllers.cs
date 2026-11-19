using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.Employee.Controllers;

[RequireFeature(FeatureCodes.Payroll)]
public sealed class PayslipsController(IEmployeeService employees, IPayrollRunService runs, ILoanService loans, IFeatureAccessService features, IAttendanceService attendance)
    : SelfServiceController(employees)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var enabled = await features.GetEnabledFeaturesAsync(User.GetOrganizationId()!.Value, ct);
        ViewBag.Loans = enabled.Contains(FeatureCodes.Loans) ? await loans.ForEmployeeAsync(me, ct) : null;
        ViewBag.Today = await attendance.TodayForOrganizationAsync(ct);
        return View(await runs.MyPayslipsAsync(me, ct));
    }

    [HttpGet("/me/payslips/{detailId:int}")]
    public async Task<IActionResult> Details(int detailId, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var slip = await runs.PayslipAsync(detailId, me, ct); // own, published payslips only
        return slip is null ? NotFound() : View("~/Views/Shared/Payslip.cshtml", slip);
    }

    [HttpGet("/me/payslips/{detailId:int}/pdf")]
    public async Task<IActionResult> Pdf(int detailId, [FromServices] Application.Documents.IPdfRenderer pdf,
        [FromServices] Application.Tenancy.ITenantAdminService admin, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var slip = await runs.PayslipAsync(detailId, me, ct);
        if (slip is null) return NotFound();
        var org = await admin.GetOrganizationAsync(ct);
        return File(pdf.Payslip(Application.Documents.PdfModels.FromPayslip(slip, org.BrandColor)), "application/pdf",
            $"Payslip-{slip.Employee.EmployeeCode}-{slip.Run.Year}-{slip.Run.Month:00}.pdf");
    }

    [HttpPost, RequireFeature(FeatureCodes.Loans)]
    public async Task<IActionResult> RequestLoan(LoanRequestInput loan, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/me/payslips");
        }
        Flash(await loans.RequestAsync(me, loan, byHr: false, ct), "Request submitted to payroll.");
        return Redirect("/me/payslips");
    }
}

[RequireFeature(FeatureCodes.EmployeeTax)]
public sealed class TaxController(IEmployeeService employees, ITaxDeclarationService declarations, ITaxReturnService returns) : SelfServiceController(employees)
{
    /// <summary>Own Form 16 Part B (with the employee's own full PAN).</summary>
    [HttpGet("/me/tax/form16/{year:int}")]
    public async Task<IActionResult> Form16(int year, [FromServices] Application.Documents.IPdfRenderer pdf, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        var model = await returns.Form16Async(me, year, revealPan: true, ct);
        return model is null ? NotFound() : File(pdf.Form16(model), "application/pdf", $"Form16-PartB-AY{model.AssessmentYear}.pdf");
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        ViewBag.Form16Years = await returns.YearsAsync(me, ct);
        var (decl, fy) = await declarations.MineAsync(me, ct);
        ViewBag.Fy = fy;
        ViewBag.Status = decl?.Status;
        ViewBag.Estimates = await declarations.CompareRegimesAsync(me, ct);
        return View(decl is null ? new DeclarationInput() : new DeclarationInput
        {
            Regime = decl.Regime, Section80C = decl.Section80C, Section80D = decl.Section80D, Section80CCD1B = decl.Section80CCD1B,
            HomeLoanInterest = decl.HomeLoanInterest, OtherDeductions = decl.OtherDeductions, MonthlyRentPaid = decl.MonthlyRentPaid,
            MetroCity = decl.MetroCity, PreviousEmployerIncome = decl.PreviousEmployerIncome, PreviousEmployerTds = decl.PreviousEmployerTds
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(DeclarationInput model, CancellationToken ct)
    {
        if (await MyEmployeeIdAsync(ct) is not int me) return NotLinked();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/me/tax");
        }
        Flash(await declarations.SaveAsync(me, model, ct), model.Submit ? "Declaration submitted. TDS will follow it from the next payroll." : "Draft saved.");
        return Redirect("/me/tax");
    }
}
