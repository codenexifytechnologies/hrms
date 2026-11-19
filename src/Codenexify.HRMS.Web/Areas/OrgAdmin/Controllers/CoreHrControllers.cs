using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Codenexify.HRMS.Web.Areas.OrgAdmin.Controllers;

[RequireFeature(FeatureCodes.CoreHr), RequirePermission(TenantPermissions.StructureManage)]
public sealed class StructureController(IStructureService structure, IEmployeeService employees) : OrgController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Branches = await structure.BranchesAsync(ct);
        ViewBag.Departments = await structure.DepartmentsAsync(ct);
        ViewBag.Designations = await structure.DesignationsAsync(ct);
        ViewBag.Checklist = await structure.ChecklistTemplateAsync(ct);
        ViewBag.Managers = await employees.ManagersAsync(null, ct);
        return View(await structure.GetSettingsAsync(ct));
    }

    private IActionResult Back(string tab) => Redirect($"/org/structure#{tab}");

    private IActionResult Invalid(string tab)
    {
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return Back(tab);
    }

    [HttpGet("/org/structure/branch/{id:int}")]
    public async Task<IActionResult> Branch(int id, CancellationToken ct)
    {
        var model = await structure.GetBranchAsync(id, ct);
        return model is null ? NotFound() : View("EditBranch", model);
    }

    [HttpGet("/org/structure/department/{id:int}")]
    public async Task<IActionResult> Department(int id, CancellationToken ct)
    {
        var model = await structure.GetDepartmentAsync(id, ct);
        if (model is null) return NotFound();
        await LoadDepartmentLookupsAsync(id, ct);
        return View("EditDepartment", model);
    }

    [HttpGet("/org/structure/designation/{id:int}")]
    public async Task<IActionResult> Designation(int id, CancellationToken ct)
    {
        var model = await structure.GetDesignationAsync(id, ct);
        return model is null ? NotFound() : View("EditDesignation", model);
    }

    private async Task LoadDepartmentLookupsAsync(int? id, CancellationToken ct)
    {
        ViewBag.Id = id;
        ViewBag.Departments = (await structure.DepartmentsAsync(ct)).Where(d => d.Id != id).ToList();
        ViewBag.Managers = await employees.ManagersAsync(null, ct);
    }

    /// <summary>Create (from the modal) or update (from the edit page, which re-renders on errors).</summary>
    private async Task<IActionResult> SaveAsync<T>(int? id, T model, string tab, string view, Func<Task<Application.Common.Result>> save, string message)
    {
        if (ModelState.IsValid)
        {
            var result = await save();
            if (result.Succeeded) { TempData["Success"] = message; return Back(tab); }
            if (id is null) { TempData["Error"] = result.Error; return Back(tab); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        else if (id is null) return Invalid(tab);
        ViewBag.Id = id;
        return View(view, model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveBranch(int? id, BranchInput branch, CancellationToken ct) =>
        await SaveAsync(id, branch, "branches", "EditBranch", () => structure.SaveBranchAsync(id, branch, ct), "Branch saved.");

    [HttpPost]
    public async Task<IActionResult> SaveDepartment(int? id, DepartmentInput department, CancellationToken ct)
    {
        if (id is not null) await LoadDepartmentLookupsAsync(id, ct);
        return await SaveAsync(id, department, "departments", "EditDepartment", () => structure.SaveDepartmentAsync(id, department, ct), "Department saved.");
    }

    [HttpPost]
    public async Task<IActionResult> SaveDesignation(int? id, DesignationInput designation, CancellationToken ct) =>
        await SaveAsync(id, designation, "designations", "EditDesignation", () => structure.SaveDesignationAsync(id, designation, ct), "Designation saved.");

    [HttpPost]
    public async Task<IActionResult> DeleteBranch(int id, CancellationToken ct)
    {
        Flash(await structure.DeleteBranchAsync(id, ct), "Branch deleted.");
        return Back("branches");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDepartment(int id, CancellationToken ct)
    {
        Flash(await structure.DeleteDepartmentAsync(id, ct), "Department deleted.");
        return Back("departments");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDesignation(int id, CancellationToken ct)
    {
        Flash(await structure.DeleteDesignationAsync(id, ct), "Designation deleted.");
        return Back("designations");
    }

    [HttpPost]
    public async Task<IActionResult> Settings(HrSettingsInput settings, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid("settings");
        Flash(await structure.SaveSettingsAsync(settings, ct), "HR settings saved.");
        return Back("settings");
    }

    [HttpPost]
    public async Task<IActionResult> AddChecklistItem(string title, string owner, CancellationToken ct)
    {
        Flash(await structure.AddChecklistTemplateItemAsync(title, owner, ct), "Checklist item added.");
        return Back("settings");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteChecklistItem(int id, CancellationToken ct)
    {
        Flash(await structure.DeleteChecklistTemplateItemAsync(id, ct), "Checklist item removed.");
        return Back("settings");
    }
}

[RequireFeature(FeatureCodes.CoreHr), RequirePermission(TenantPermissions.EmployeesView)]
public sealed class EmployeesController(
    IEmployeeService employees, IStructureService structure, IDocumentService documents, IExitService exits, IHrReportService reports)
    : OrgController
{
    private bool CanSensitive => User.HasPermission(TenantPermissions.EmployeesSensitive);

    public async Task<IActionResult> Index([FromQuery] EmployeeQuery query, CancellationToken ct)
    {
        ViewBag.Query = query;
        await LoadLookupsAsync(null, ct);
        return View(await employees.ListAsync(query, ct));
    }

    [RequirePermission(TenantPermissions.ReportsView), RequireFeature(FeatureCodes.ReportsBasic)]
    public async Task<IActionResult> Export([FromQuery] EmployeeQuery query, CancellationToken ct)
    {
        var file = await reports.ExportEmployeesAsync(query, ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    public async Task<IActionResult> OrgChart(CancellationToken ct) => View(await employees.OrgChartAsync(ct));

    [HttpGet, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadLookupsAsync(null, ct);
        ViewBag.NextCode = await employees.PreviewNextCodeAsync(ct);
        return View(new EmployeeCreateInput { Employment = await employees.NewEmploymentDefaultsAsync(ct) });
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> Create(EmployeeCreateInput model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await employees.CreateAsync(model, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Employee added.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await LoadLookupsAsync(null, ct);
        ViewBag.NextCode = await employees.PreviewNextCodeAsync(ct);
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var profile = await employees.GetProfileAsync(id, CanSensitive, ct);
        if (profile is null) return NotFound();
        await LoadLookupsAsync(id, ct);
        ViewBag.CanSensitive = CanSensitive;
        ViewBag.Documents = await documents.ForEmployeeAsync(id, visibleOnly: false, ct);
        return View(profile);
    }

    private IActionResult Back(int id, string tab) => Redirect($"/org/employees/details/{id}#{tab}");

    private IActionResult Invalid(int id, string tab)
    {
        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
        return Back(id, tab);
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> Employment(int id, EmploymentInput employment, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "employment");
        Flash(await employees.UpdateEmploymentAsync(id, employment, ct), "Employment details saved.");
        return Back(id, "employment");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> Personal(int id, PersonalInput personal, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "personal");
        Flash(await employees.UpdatePersonalAsync(id, personal, CanSensitive, ct), "Personal details saved.");
        return Back(id, "personal");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesSensitive)]
    public async Task<IActionResult> Bank(int id, BankInput bank, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "bank");
        Flash(await employees.SaveBankAsync(id, bank, ct), "Bank details saved.");
        return Back(id, "bank");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> AddEducation(int id, EducationInput education, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "background");
        Flash(await employees.AddEducationAsync(id, education, ct), "Education added.");
        return Back(id, "background");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> DeleteEducation(int id, int recordId, CancellationToken ct)
    {
        Flash(await employees.DeleteEducationAsync(id, recordId, ct), "Education removed.");
        return Back(id, "background");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> AddExperience(int id, ExperienceInput experience, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "background");
        Flash(await employees.AddExperienceAsync(id, experience, ct), "Experience added.");
        return Back(id, "background");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> DeleteExperience(int id, int recordId, CancellationToken ct)
    {
        Flash(await employees.DeleteExperienceAsync(id, recordId, ct), "Experience removed.");
        return Back(id, "background");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage)]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        Flash(await employees.ConfirmProbationAsync(id, ct), "Employee confirmed.");
        return Back(id, "employment");
    }

    [HttpPost, RequirePermission(TenantPermissions.EmployeesManage), RequirePermission(TenantPermissions.UsersManage)]
    public async Task<IActionResult> CreateLogin(int id, string password, CancellationToken ct)
    {
        Flash(await employees.CreateLoginAsync(id, password, ct), "Login created. The employee must change the password at first sign-in.");
        return Back(id, "employment");
    }

    [HttpPost, RequireFeature(FeatureCodes.DocumentsLetters), RequirePermission(TenantPermissions.DocumentsManage)]
    [RequestSizeLimit(DocumentService.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(int id, DocumentUploadInput document, IFormFile? file, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "documents");
        if (file is null) { TempData["Error"] = "Choose a file to upload."; return Back(id, "documents"); }
        await using var stream = file.OpenReadStream();
        Flash(await documents.UploadAsync(id, document, new UploadedFile(stream, file.FileName, file.Length), ct), "Document uploaded.");
        return Back(id, "documents");
    }

    [RequireFeature(FeatureCodes.DocumentsLetters)]
    public async Task<IActionResult> Document(int docId, CancellationToken ct)
    {
        var doc = await documents.OpenAsync(docId, null, ct);
        return doc is null ? NotFound() : File(doc.Content, doc.ContentType, doc.FileName);
    }

    [HttpPost, RequireFeature(FeatureCodes.DocumentsLetters), RequirePermission(TenantPermissions.DocumentsManage)]
    public async Task<IActionResult> VerifyDocument(int id, int docId, CancellationToken ct)
    {
        Flash(await documents.VerifyAsync(docId, ct), "Document marked as verified.");
        return Back(id, "documents");
    }

    [HttpPost, RequireFeature(FeatureCodes.DocumentsLetters), RequirePermission(TenantPermissions.DocumentsManage)]
    public async Task<IActionResult> DeleteDocument(int id, int docId, CancellationToken ct)
    {
        Flash(await documents.DeleteAsync(docId, ct), "Document deleted.");
        return Back(id, "documents");
    }

    [HttpPost, RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.ExitManage)]
    public async Task<IActionResult> InitiateExit(int id, ExitInitiateInput exit, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(id, "exit");
        var result = await exits.InitiateAsync(id, exit, byEmployee: false, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(id, "exit"); }
        TempData["Success"] = "Exit initiated. Review and approve it to start the checklist.";
        return Redirect($"/org/exits/details/{result.Value}");
    }

    private async Task LoadLookupsAsync(int? employeeId, CancellationToken ct)
    {
        var (branches, departments, designations) = await structure.LookupsAsync(ct);
        ViewBag.Branches = branches;
        ViewBag.Departments = departments;
        ViewBag.Designations = designations;
        ViewBag.Managers = await employees.ManagersAsync(employeeId, ct);
    }
}

[RequireFeature(FeatureCodes.EmployeeExit), RequirePermission(TenantPermissions.ExitManage)]
public sealed class ExitsController(IExitService exits) : OrgController
{
    public async Task<IActionResult> Index(ExitStatus? status, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Status = status;
        return View(await exits.ListAsync(status, page, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var exit = await exits.GetAsync(id, ct);
        return exit is null ? NotFound() : View(exit);
    }

    private IActionResult Back(int id) => Redirect($"/org/exits/details/{id}");

    [HttpPost]
    public async Task<IActionResult> Approve(int id, DateOnly lastWorkingDay, string? notes, CancellationToken ct)
    {
        Flash(await exits.ApproveAsync(id, lastWorkingDay, notes, ct), "Exit approved. The employee is now serving notice.");
        return Back(id);
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string notes, CancellationToken ct)
    {
        Flash(await exits.RejectAsync(id, notes, ct), "Exit request rejected.");
        return Back(id);
    }

    [HttpPost]
    public async Task<IActionResult> Checklist(int id, int itemId, bool completed, CancellationToken ct)
    {
        Flash(await exits.SetChecklistItemAsync(id, itemId, completed, ct), completed ? "Item completed." : "Item reopened.");
        return Back(id);
    }

    [HttpPost]
    public async Task<IActionResult> Interview(int id, string? notes, CancellationToken ct)
    {
        Flash(await exits.SaveInterviewNotesAsync(id, notes, ct), "Exit interview notes saved.");
        return Back(id);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        Flash(await exits.CompleteAsync(id, ct), "Exit completed. The employee's login has been disabled.");
        return Back(id);
    }
}

[RequireFeature(FeatureCodes.ReportsBasic), RequirePermission(TenantPermissions.ReportsView)]
public sealed class ReportsController(IHrReportService reports, Application.Abstractions.IClock clock,
    Application.Subscriptions.IFeatureAccessService features, IStructureService structure) : OrgController
{
    /// <summary>Filters apply only when the plan includes Advanced Reports.</summary>
    private async Task<ReportFilter> FilterAsync(int? departmentId, int? branchId, Domain.CoreHr.EmploymentType? type, CancellationToken ct)
    {
        var advanced = await features.IsEnabledAsync(User.GetOrganizationId()!.Value, FeatureCodes.ReportsAdvanced, ct);
        ViewBag.Advanced = advanced;
        return advanced ? new ReportFilter(departmentId, branchId, type) : ReportFilter.None;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? departmentId, int? branchId, Domain.CoreHr.EmploymentType? type, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        var filter = await FilterAsync(departmentId, branchId, type, ct);
        ViewBag.From = f;
        ViewBag.To = t;
        ViewBag.Filter = filter;
        if (ViewBag.Advanced is true)
        {
            var lookups = await structure.LookupsAsync(ct);
            ViewBag.Departments = lookups.Departments;
            ViewBag.Branches = lookups.Branches;
        }
        ViewBag.Joiners = await reports.JoinersAsync(f, t, filter, ct);
        ViewBag.Exits = await reports.ExitsAsync(f, t, filter, ct);
        return View(await reports.HeadcountAsync(ct));
    }

    public async Task<IActionResult> ExportJoiners(DateOnly? from, DateOnly? to, int? departmentId, int? branchId, Domain.CoreHr.EmploymentType? type, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        var file = await reports.ExportJoinersAsync(f, t, await FilterAsync(departmentId, branchId, type, ct), ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    public async Task<IActionResult> ExportExits(DateOnly? from, DateOnly? to, int? departmentId, int? branchId, Domain.CoreHr.EmploymentType? type, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        var file = await reports.ExportExitsAsync(f, t, await FilterAsync(departmentId, branchId, type, ct), ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    private (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var today = clock.Today;
        var t = to ?? today;
        var f = from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-2);
        return f > t ? (t, f) : (f, t);
    }
}
