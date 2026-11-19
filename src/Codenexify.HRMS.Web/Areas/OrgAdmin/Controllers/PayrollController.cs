using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Payroll;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

[RequireFeature(FeatureCodes.Payroll), RequirePermission(TenantPermissions.PayrollView)]
[Route("org/payroll")]
public sealed class PayrollController(
    IPayrollRunService runs, IPayrollSetupService setup, ILoanService loans, ITaxDeclarationService declarations,
    ISettlementService settlements, IEmployeeService employees, IAttendanceService attendance) : OrgController
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private IActionResult Invalid(string url)
    {
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return Redirect(url);
    }

    // ------------------------------------------------------------------ runs
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Today = await attendance.TodayForOrganizationAsync(ct);
        ViewBag.Missing = (await setup.RosterAsync(null, true, 1, ct)).TotalCount;
        return View(await runs.RunsAsync(ct));
    }

    [HttpPost("create"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Create(int year, int month, CancellationToken ct)
    {
        var r = await runs.CreateAsync(year, month, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/org/payroll"); }
        var p = await runs.ProcessAsync(r.Value, ct);
        if (p.Succeeded) TempData["Success"] = "Payroll processed. Review it, then finalize."; else TempData["Error"] = p.Error;
        return Redirect($"/org/payroll/run/{r.Value}");
    }

    [HttpGet("run/{id:int}")]
    public async Task<IActionResult> Run(int id, CancellationToken ct)
    {
        var run = await runs.GetAsync(id, ct);
        if (run is null) return NotFound();
        ViewBag.Run = run;
        return View(await runs.DetailsAsync(id, ct));
    }

    [HttpPost("run/{id:int}/process"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Process(int id, CancellationToken ct)
    {
        Flash(await runs.ProcessAsync(id, ct), "Payroll re-processed with the latest attendance, leave and salary data.");
        return Redirect($"/org/payroll/run/{id}");
    }

    [HttpPost("run/{id:int}/lop"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Lop(int id, int detailId, decimal? lopDays, CancellationToken ct)
    {
        Flash(await runs.SetLopOverrideAsync(detailId, lopDays, ct), lopDays is null ? "LOP override removed." : "LOP updated.");
        return Redirect($"/org/payroll/run/{id}");
    }

    [HttpPost("run/{id:int}/hold"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Hold(int id, int detailId, bool hold, string? reason, CancellationToken ct)
    {
        Flash(await runs.SetHoldAsync(detailId, hold, reason, ct), hold ? "Salary put on hold." : "Salary released.");
        return Redirect($"/org/payroll/run/{id}");
    }

    [HttpPost("run/{id:int}/finalize"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Finalize(int id, CancellationToken ct)
    {
        Flash(await runs.FinalizeAsync(id, ct), "Payroll finalized. Payslips are published to employees.");
        return Redirect($"/org/payroll/run/{id}");
    }

    [HttpPost("run/{id:int}/paid"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Paid(int id, DateOnly paidOn, CancellationToken ct)
    {
        Flash(await runs.MarkPaidAsync(id, paidOn, ct), "Payroll marked as paid.");
        return Redirect($"/org/payroll/run/{id}");
    }

    [HttpPost("run/{id:int}/delete"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Flash(await runs.DeleteAsync(id, ct), "Draft payroll deleted.");
        return Redirect("/org/payroll");
    }

    [HttpGet("run/{id:int}/export/{kind}")]
    public async Task<IActionResult> Export(int id, string kind, CancellationToken ct)
    {
        if (kind is not ("register" or "bank" or "pf" or "ecr" or "esi" or "journal")) return NotFound();
        if (kind == "bank" && !(User.HasPermission(TenantPermissions.EmployeesSensitive) && User.HasPermission(TenantPermissions.PayrollManage))) return Forbid();
        try
        {
            var file = await runs.ExportAsync(id, kind, ct);
            return File(file.Content, file.FileName.EndsWith(".txt") ? "text/plain" : Xlsx, file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect($"/org/payroll/run/{id}");
        }
    }

    [HttpGet("payslip/{detailId:int}")]
    public async Task<IActionResult> Payslip(int detailId, CancellationToken ct)
    {
        var slip = await runs.PayslipAsync(detailId, null, ct);
        return slip is null ? NotFound() : View("~/Views/Shared/Payslip.cshtml", slip);
    }

    [HttpGet("payslip/{detailId:int}/pdf")]
    public async Task<IActionResult> PayslipPdf(int detailId, [FromServices] Application.Documents.IPdfRenderer pdf,
        [FromServices] Application.Tenancy.ITenantAdminService admin, CancellationToken ct)
    {
        var slip = await runs.PayslipAsync(detailId, null, ct);
        if (slip is null) return NotFound();
        var org = await admin.GetOrganizationAsync(ct);
        return File(pdf.Payslip(Application.Documents.PdfModels.FromPayslip(slip, org.BrandColor)), "application/pdf",
            $"Payslip-{slip.Employee.EmployeeCode}-{slip.Run.Year}-{slip.Run.Month:00}.pdf");
    }

    // ------------------------------------------------------------------ one-time items
    [HttpGet("adjustments")]
    public async Task<IActionResult> Adjustments(bool all, [FromServices] IPayrollAdjustmentService adjustments, CancellationToken ct)
    {
        ViewBag.All = all;
        var open = await adjustments.NextOpenMonthAsync(ct);
        ViewBag.Open = open;
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        ViewBag.Input = new AdjustmentInput { Year = open.Year, Month = open.Month };
        return View(await adjustments.ListAsync(!all, ct));
    }

    [HttpPost("adjustments"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> AddAdjustment(AdjustmentInput item, [FromServices] IPayrollAdjustmentService adjustments, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("/org/payroll/adjustments");
        Flash(await adjustments.AddAsync(item, ct), "Added. It will be paid with that month's payroll (re-process the run if it's already processed).");
        return Redirect("/org/payroll/adjustments");
    }

    [HttpPost("adjustments/{id:int}/delete"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> DeleteAdjustment(int id, [FromServices] IPayrollAdjustmentService adjustments, CancellationToken ct)
    {
        Flash(await adjustments.DeleteAsync(id, ct), "Removed.");
        return Redirect("/org/payroll/adjustments");
    }

    // ------------------------------------------------------------------ Form 16 & 24Q
    [HttpGet("returns"), RequireFeature(FeatureCodes.EmployeeTax)]
    public async Task<IActionResult> Returns([FromServices] ITaxReturnService returns, CancellationToken ct)
    {
        ViewBag.Years = await returns.YearsAsync(null, ct);
        ViewBag.Employees = await returns.EmployeesAsync(ct);
        return View();
    }

    [HttpGet("returns/form16"), RequireFeature(FeatureCodes.EmployeeTax)]
    public async Task<IActionResult> Form16(int employeeId, int year, [FromServices] ITaxReturnService returns,
        [FromServices] Application.Documents.IPdfRenderer pdf, CancellationToken ct)
    {
        // Full PAN only for users who may see sensitive employee data.
        var model = await returns.Form16Async(employeeId, year, User.HasPermission(TenantPermissions.EmployeesSensitive), ct);
        if (model is null) { TempData["Error"] = "No finalized payroll for that employee in that financial year."; return Redirect("/org/payroll/returns"); }
        return File(pdf.Form16(model), "application/pdf", $"Form16-PartB-{model.EmployeeCode}-AY{model.AssessmentYear}.pdf");
    }

    [HttpGet("returns/24q"), RequireFeature(FeatureCodes.EmployeeTax), RequirePermission(TenantPermissions.PayrollManage), RequirePermission(TenantPermissions.EmployeesSensitive)]
    public async Task<IActionResult> Form24Q(int year, int quarter, [FromServices] ITaxReturnService returns, CancellationToken ct)
    {
        if (quarter is < 1 or > 4) return BadRequest();
        var file = await returns.Form24QAnnexureAsync(year, quarter, ct);
        return File(file.Content, Xlsx, file.FileName);
    }

    // ------------------------------------------------------------------ salaries
    [HttpGet("salaries")]
    public async Task<IActionResult> Salaries(string? search, bool missing = false, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Search = search;
        ViewBag.Missing = missing;
        ViewBag.Structures = (await setup.StructuresAsync(ct)).Where(s => s.IsActive).ToList();
        ViewBag.Today = await attendance.TodayForOrganizationAsync(ct);
        return View(await setup.RosterAsync(search, missing, page, ct));
    }

    [HttpGet("salaries/{employeeId:int}")]
    public async Task<IActionResult> SalaryHistory(int employeeId, CancellationToken ct) =>
        Json((await setup.HistoryAsync(employeeId, ct)).Select(s => new { s.EffectiveFrom, s.AnnualGross, structure = s.Structure.Name, s.PfApplicable, s.Remarks }));

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int structureId, decimal annualGross, bool pfApplicable = true, CancellationToken ct = default)
    {
        var b = await setup.PreviewAsync(structureId, annualGross, pfApplicable, ct);
        return Json(new { b.MonthlyGross, b.EmployerPf, b.EmployerEsi, b.MonthlyCtc, annualCtc = b.MonthlyCtc * 12, b.Error, components = b.Components.Select(c => new { c.Name, type = c.Type.ToString(), c.Monthly }) });
    }

    [HttpPost("salaries/{employeeId:int}"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Assign(int employeeId, SalaryAssignInput salary, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("/org/payroll/salaries");
        var saved = await setup.AssignAsync(employeeId, salary, ct);
        Flash(saved, saved.Value switch
        {
            > 0 => $"Salary saved. Arrears of ₹{saved.Value:N0} will be paid in the next payroll (see One-time items).",
            < 0 => $"Salary saved. ₹{-saved.Value:N0} will be recovered in the next payroll (see One-time items).",
            _ => "Salary saved."
        });
        return Redirect("/org/payroll/salaries");
    }

    // ------------------------------------------------------------------ structures & components
    [HttpGet("structures")]
    public async Task<IActionResult> Structures(CancellationToken ct)
    {
        ViewBag.Components = await setup.ComponentsAsync(ct);
        return View(await setup.StructuresAsync(ct));
    }

    [HttpGet("component/{id:int?}"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Component(int? id, CancellationToken ct)
    {
        var model = id is null ? new ComponentInput() : await setup.GetComponentAsync(id.Value, ct);
        if (model is null) return NotFound();
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost("component/{id:int?}"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Component(int? id, ComponentInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await setup.SaveComponentAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Component saved."; return Redirect("/org/payroll/structures"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        return View(model);
    }

    [HttpGet("structure/{id:int?}"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Structure(int? id, CancellationToken ct)
    {
        ViewBag.Id = id;
        ViewBag.Components = await setup.ComponentsAsync(ct);
        return View(await setup.GetStructureAsync(id, ct));
    }

    [HttpPost("structure/{id:int?}"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Structure(int? id, StructureInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await setup.SaveStructureAsync(id, model, ct);
            if (r.Succeeded) { TempData["Success"] = "Structure saved."; return Redirect("/org/payroll/structures"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        ViewBag.Id = id;
        ViewBag.Components = await setup.ComponentsAsync(ct);
        return View(model);
    }

    // ------------------------------------------------------------------ settings
    [HttpGet("settings"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Settings(CancellationToken ct) => View(await setup.GetSettingsAsync(ct));

    [HttpPost("settings"), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Settings(PayrollSettingsInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await setup.SaveSettingsAsync(model, ct);
            if (r.Succeeded) { TempData["Success"] = "Payroll settings saved."; return Redirect("/org/payroll/settings"); }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        return View(model);
    }

    // ------------------------------------------------------------------ loans
    [HttpGet("loans"), RequireFeature(FeatureCodes.Loans)]
    public async Task<IActionResult> Loans(LoanStatus? status, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Status = status;
        ViewBag.Employees = await employees.ManagersAsync(null, ct);
        ViewBag.Today = await attendance.TodayForOrganizationAsync(ct);
        return View(await loans.ListAsync(status, page, ct));
    }

    [HttpPost("loans/new"), RequireFeature(FeatureCodes.Loans), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> NewLoan(int employeeId, LoanRequestInput loan, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("/org/payroll/loans");
        var r = await loans.RequestAsync(employeeId, loan, byHr: true, ct);
        Flash(r, "Loan recorded. Approve it to start recovery.");
        return Redirect("/org/payroll/loans");
    }

    [HttpPost("loans/{id:int}/decide"), RequireFeature(FeatureCodes.Loans), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> DecideLoan(int id, bool approve, string? notes, CancellationToken ct)
    {
        Flash(await loans.DecideAsync(id, approve, notes, ct), approve ? "Loan approved; recovery starts in payroll." : "Request rejected.");
        return Redirect("/org/payroll/loans");
    }

    [HttpPost("loans/{id:int}/repay"), RequireFeature(FeatureCodes.Loans), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> Repay(int id, decimal amount, DateOnly date, string? note, CancellationToken ct)
    {
        Flash(await loans.RecordRepaymentAsync(id, amount, date, note, ct), "Repayment recorded.");
        return Redirect("/org/payroll/loans");
    }

    // ------------------------------------------------------------------ tax declarations
    [HttpGet("declarations"), RequireFeature(FeatureCodes.EmployeeTax)]
    public async Task<IActionResult> Declarations(int? fy, DeclarationStatus? status, int page = 1, CancellationToken ct = default)
    {
        var year = fy ?? await declarations.CurrentFinancialYearAsync(ct);
        ViewBag.Fy = year;
        ViewBag.Status = status;
        return View(await declarations.ListAsync(year, status, page, ct));
    }

    [HttpPost("declarations/{id:int}/approve"), RequireFeature(FeatureCodes.EmployeeTax), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> ApproveDeclaration(int id, CancellationToken ct)
    {
        Flash(await declarations.ApproveAsync(id, ct), "Declaration approved.");
        return Redirect("/org/payroll/declarations");
    }

    [HttpPost("declarations/{id:int}/reopen"), RequireFeature(FeatureCodes.EmployeeTax), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> ReopenDeclaration(int id, CancellationToken ct)
    {
        Flash(await declarations.ReopenAsync(id, ct), "Declaration reopened for the employee.");
        return Redirect("/org/payroll/declarations");
    }

    // ------------------------------------------------------------------ gratuity & F&F
    [HttpGet("gratuity"), RequireFeature(FeatureCodes.Gratuity)]
    public async Task<IActionResult> Gratuity(CancellationToken ct) => View(await settlements.GratuityLiabilityAsync(ct));

    [HttpGet("settlements"), RequireFeature(FeatureCodes.EmployeeExit)]
    public async Task<IActionResult> Settlements(CancellationToken ct) => View(await settlements.CandidatesAsync(ct));

    [HttpPost("settlements/prepare/{employeeId:int}"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> PrepareSettlement(int employeeId, CancellationToken ct)
    {
        var r = await settlements.PrepareAsync(employeeId, ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect("/org/payroll/settlements"); }
        TempData["Success"] = "Settlement calculated. Add any other items, then approve.";
        return Redirect($"/org/payroll/settlement/{r.Value}");
    }

    [HttpGet("settlement/{id:int}"), RequireFeature(FeatureCodes.EmployeeExit)]
    public async Task<IActionResult> Settlement(int id, CancellationToken ct)
    {
        var s = await settlements.GetAsync(id, ct);
        return s is null ? NotFound() : View(s);
    }

    [HttpPost("settlement/{id:int}/line"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> AddSettlementLine(int id, string description, decimal amount, CancellationToken ct)
    {
        Flash(await settlements.AddLineAsync(id, description, amount, ct), "Item added.");
        return Redirect($"/org/payroll/settlement/{id}");
    }

    [HttpPost("settlement/{id:int}/line/{lineId:int}/remove"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> RemoveSettlementLine(int id, int lineId, CancellationToken ct)
    {
        Flash(await settlements.RemoveLineAsync(id, lineId, ct), "Item removed.");
        return Redirect($"/org/payroll/settlement/{id}");
    }

    [HttpPost("settlement/{id:int}/recalculate"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> RecalculateSettlement(int id, CancellationToken ct)
    {
        var s = await settlements.GetAsync(id, ct);
        if (s is null) return NotFound();
        Flash(await settlements.PrepareAsync(s.EmployeeId, ct), "Settlement recalculated.");
        return Redirect($"/org/payroll/settlement/{id}");
    }

    [HttpPost("settlement/{id:int}/approve"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> ApproveSettlement(int id, string? notes, CancellationToken ct)
    {
        Flash(await settlements.ApproveAsync(id, notes, ct), "Settlement approved.");
        return Redirect($"/org/payroll/settlement/{id}");
    }

    [HttpPost("settlement/{id:int}/paid"), RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.PayrollManage)]
    public async Task<IActionResult> SettlementPaid(int id, DateOnly paidOn, CancellationToken ct)
    {
        Flash(await settlements.MarkPaidAsync(id, paidOn, ct), "Settlement marked as paid.");
        return Redirect($"/org/payroll/settlement/{id}");
    }
}
