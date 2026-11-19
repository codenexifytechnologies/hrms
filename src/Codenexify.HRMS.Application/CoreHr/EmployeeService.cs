using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Catalog;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.CoreHr;

public sealed class EmploymentInput
{
    [Required, StringLength(100), Display(Name = "First name")] public string FirstName { get; set; } = string.Empty;
    [StringLength(100), Display(Name = "Middle name")] public string? MiddleName { get; set; }
    [Required, StringLength(100), Display(Name = "Last name")] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(200), Display(Name = "Work email")] public string WorkEmail { get; set; } = string.Empty;
    [Phone, StringLength(20), Display(Name = "Mobile")] public string? MobilePhone { get; set; }
    [Required, Display(Name = "Date of joining")] public DateOnly DateOfJoining { get; set; }
    [Display(Name = "Employment type")] public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    [Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Designation")] public int? DesignationId { get; set; }
    [Display(Name = "Reporting manager")] public int? ReportingManagerId { get; set; }
    [Range(0, 365), Display(Name = "Notice period (days)")] public int? NoticePeriodDays { get; set; }
    [Display(Name = "Probation ends on")] public DateOnly? ProbationEndDate { get; set; }
}

public sealed class EmployeeCreateInput
{
    public EmploymentInput Employment { get; set; } = new();
    [Display(Name = "Create a login for this employee")] public bool CreateLogin { get; set; } = true;
    [StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Initial password")] public string? InitialPassword { get; set; }
}

public sealed class PersonalInput
{
    public Gender? Gender { get; set; }
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
    [Display(Name = "Marital status")] public MaritalStatus? MaritalStatus { get; set; }
    [StringLength(5), Display(Name = "Blood group")] public string? BloodGroup { get; set; }
    [Required, StringLength(60)] public string Nationality { get; set; } = "Indian";
    [EmailAddress, StringLength(200), Display(Name = "Personal email")] public string? PersonalEmail { get; set; }
    [StringLength(500), Display(Name = "Current address")] public string? CurrentAddress { get; set; }
    [StringLength(500), Display(Name = "Permanent address")] public string? PermanentAddress { get; set; }
    [StringLength(150), Display(Name = "Emergency contact")] public string? EmergencyContactName { get; set; }
    [StringLength(50), Display(Name = "Relationship")] public string? EmergencyContactRelation { get; set; }
    [Phone, StringLength(20), Display(Name = "Emergency phone")] public string? EmergencyContactPhone { get; set; }
    [StringLength(12), RegularExpression("^[0-9]{12}$", ErrorMessage = "UAN is 12 digits."), Display(Name = "UAN")] public string? Uan { get; set; }
    /// <summary>Only applied when the caller has the sensitive-data permission; blank keeps the stored value.</summary>
    [StringLength(10), RegularExpression("^[A-Za-z]{5}[0-9]{4}[A-Za-z]$", ErrorMessage = "Enter a valid PAN."), Display(Name = "PAN")] public string? Pan { get; set; }
}

public sealed class BankInput
{
    [Required, StringLength(150), Display(Name = "Bank name")] public string BankName { get; set; } = string.Empty;
    [Required, StringLength(150), Display(Name = "Account holder name")] public string AccountHolderName { get; set; } = string.Empty;
    [Required, StringLength(18, MinimumLength = 6), RegularExpression("^[0-9]+$", ErrorMessage = "Digits only."), Display(Name = "Account number")] public string AccountNumber { get; set; } = string.Empty;
    [Required, StringLength(11), RegularExpression("^[A-Za-z]{4}0[A-Za-z0-9]{6}$", ErrorMessage = "Enter a valid IFSC."), Display(Name = "IFSC")] public string Ifsc { get; set; } = string.Empty;
    [StringLength(150), Display(Name = "Bank branch")] public string? BranchName { get; set; }
}

public sealed class EducationInput
{
    [Required, StringLength(150)] public string Qualification { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Institution { get; set; } = string.Empty;
    [StringLength(150)] public string? Specialization { get; set; }
    [Range(1950, 2100), Display(Name = "Year of passing")] public int? YearOfPassing { get; set; }
    [StringLength(30)] public string? Grade { get; set; }
}

public sealed class ExperienceInput
{
    [Required, StringLength(200)] public string Company { get; set; } = string.Empty;
    [StringLength(150)] public string? Designation { get; set; }
    [Required, Display(Name = "From")] public DateOnly FromDate { get; set; }
    [Display(Name = "To")] public DateOnly? ToDate { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class EmployeeQuery
{
    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public int? DesignationId { get; set; }
    public EmployeeStatus? Status { get; set; }
    /// <summary>When no status is chosen, exited employees are hidden unless this is true.</summary>
    public bool IncludeExited { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record EmployeeRow(
    int Id, string Code, string Name, string WorkEmail, string? Department, string? Designation, string? Branch,
    string? Manager, EmployeeStatus Status, EmploymentType Type, DateOnly DateOfJoining, bool HasLogin);

public sealed class EmployeeProfile
{
    public required Employee Employee { get; init; }
    public string? PanPlain { get; init; }
    public string? BankAccountPlain { get; init; }
    public required IReadOnlyList<EmployeeRow> DirectReports { get; init; }
    public ExitRequest? OpenExit { get; init; }
}

public sealed record OrgChartNode(int Id, string Name, string Code, string? Designation, string? Department, List<OrgChartNode> Reports);

public interface IEmployeeService
{
    Task<PagedResult<EmployeeRow>> ListAsync(EmployeeQuery query, CancellationToken ct = default);
    Task<EmployeeProfile?> GetProfileAsync(int id, bool includeSensitive, CancellationToken ct = default);
    Task<int?> GetMyEmployeeIdAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> ManagersAsync(int? excludeId = null, CancellationToken ct = default);
    Task<string> PreviewNextCodeAsync(CancellationToken ct = default);
    Task<EmploymentInput> NewEmploymentDefaultsAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(EmployeeCreateInput input, CancellationToken ct = default);
    Task<Result> UpdateEmploymentAsync(int id, EmploymentInput input, CancellationToken ct = default);
    Task<Result> UpdatePersonalAsync(int id, PersonalInput input, bool canEditSensitive, CancellationToken ct = default);
    Task<Result> SaveBankAsync(int id, BankInput input, CancellationToken ct = default);
    Task<Result> AddEducationAsync(int id, EducationInput input, CancellationToken ct = default);
    Task<Result> DeleteEducationAsync(int id, int educationId, CancellationToken ct = default);
    Task<Result> AddExperienceAsync(int id, ExperienceInput input, CancellationToken ct = default);
    Task<Result> DeleteExperienceAsync(int id, int experienceId, CancellationToken ct = default);
    Task<Result> ConfirmProbationAsync(int id, CancellationToken ct = default);
    Task<Result> CreateLoginAsync(int id, string password, CancellationToken ct = default);

    Task<IReadOnlyList<OrgChartNode>> OrgChartAsync(CancellationToken ct = default);
}

public sealed class EmployeeService(
    IAppDbContext db, ICurrentContext context, IClock clock, IPasswordService passwords,
    ISecretProtector protector, ISubscriptionService subscriptions) : IEmployeeService
{
    private int OrgId => context.OrganizationId ?? throw new TenantIsolationException("No organization in the current context.");

    public async Task<PagedResult<EmployeeRow>> ListAsync(EmployeeQuery q, CancellationToken ct = default)
    {
        const int size = 25;
        var page = Math.Max(1, q.Page);
        var query = Filter(db.Employees.AsNoTracking(), q);
        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedResult<EmployeeRow>(items, total, page, size);
    }

    internal static IQueryable<Employee> Filter(IQueryable<Employee> query, EmployeeQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(e => e.FirstName.Contains(s) || e.LastName.Contains(s) || e.EmployeeCode.Contains(s) || e.WorkEmail.Contains(s) || (e.MobilePhone != null && e.MobilePhone.Contains(s)));
        }
        if (q.DepartmentId is { } d) query = query.Where(e => e.DepartmentId == d);
        if (q.BranchId is { } b) query = query.Where(e => e.BranchId == b);
        if (q.DesignationId is { } g) query = query.Where(e => e.DesignationId == g);
        if (q.Status is { } st) query = query.Where(e => e.Status == st);
        else if (!q.IncludeExited) query = query.Where(e => e.Status != EmployeeStatus.Exited);
        return query;
    }

    internal static IQueryable<EmployeeRow> Project(IQueryable<Employee> query) =>
        query.Select(e => new EmployeeRow(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.WorkEmail,
            e.Department != null ? e.Department.Name : null, e.Designation != null ? e.Designation.Name : null,
            e.Branch != null ? e.Branch.Name : null,
            e.ReportingManager != null ? e.ReportingManager.FirstName + " " + e.ReportingManager.LastName : null,
            e.Status, e.EmploymentType, e.DateOfJoining, e.UserId != null));

    public async Task<EmployeeProfile?> GetProfileAsync(int id, bool includeSensitive, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking()
            .Include(x => x.Branch).Include(x => x.Department).Include(x => x.Designation).Include(x => x.ReportingManager)
            .Include(x => x.User).Include(x => x.BankDetail).Include(x => x.Education).Include(x => x.Experience)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;

        return new EmployeeProfile
        {
            Employee = e,
            PanPlain = includeSensitive && e.PanEncrypted is not null ? protector.Unprotect(e.PanEncrypted) : null,
            BankAccountPlain = includeSensitive && e.BankDetail is not null ? protector.Unprotect(e.BankDetail.AccountNumberEncrypted) : null,
            DirectReports = await Project(db.Employees.AsNoTracking().Where(r => r.ReportingManagerId == id && r.Status != EmployeeStatus.Exited)).ToListAsync(ct),
            OpenExit = await db.ExitRequests.AsNoTracking().Include(x => x.Checklist)
                .Where(x => x.EmployeeId == id && (x.Status == ExitStatus.Pending || x.Status == ExitStatus.Approved || x.Status == ExitStatus.Completed))
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct)
        };
    }

    public async Task<int?> GetMyEmployeeIdAsync(CancellationToken ct = default)
    {
        var uid = context.UserId;
        if (uid is null) return null;
        return await db.Employees.Where(e => e.UserId == uid).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LookupItem>> ManagersAsync(int? excludeId = null, CancellationToken ct = default) =>
        await db.Employees.AsNoTracking()
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && e.Id != excludeId)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName + " (" + e.EmployeeCode + ")"))
            .ToListAsync(ct);

    public async Task<string> PreviewNextCodeAsync(CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        return s.FormatEmployeeCode(s.NextEmployeeNumber);
    }

    public async Task<EmploymentInput> NewEmploymentDefaultsAsync(CancellationToken ct = default)
    {
        var s = await SettingsAsync(ct);
        var today = clock.Today;
        return new EmploymentInput
        {
            DateOfJoining = today,
            NoticePeriodDays = s.DefaultNoticePeriodDays,
            ProbationEndDate = s.DefaultProbationMonths > 0 ? today.AddMonths(s.DefaultProbationMonths).AddDays(-1) : null
        };
    }

    private async Task<OrganizationSetting> SettingsAsync(CancellationToken ct)
    {
        var s = await db.OrganizationSettings.FirstOrDefaultAsync(ct);
        if (s is not null) return s;
        s = new OrganizationSetting();
        db.OrganizationSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<Result<int>> CreateAsync(EmployeeCreateInput input, CancellationToken ct = default)
    {
        var e = input.Employment;
        var email = e.WorkEmail.Trim().ToLowerInvariant();
        if (await db.Employees.AnyAsync(x => x.WorkEmail == email && x.Status != EmployeeStatus.Exited, ct))
            return Result<int>.Fail("An active employee already uses this work email.");

        var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
        if (await db.Employees.CountAsync(x => x.Status != EmployeeStatus.Exited && x.Status != EmployeeStatus.Inactive, ct) >= limits.MaxEmployees)
            return Result<int>.Fail($"Your plan allows {limits.MaxEmployees} employees. Upgrade your subscription to add more.");

        if (await ValidateReferencesAsync(e, null, ct) is { } refError) return Result<int>.Fail(refError);

        User? login = null;
        if (input.CreateLogin)
        {
            if (string.IsNullOrWhiteSpace(input.InitialPassword) || input.InitialPassword.Length < 8)
                return Result<int>.Fail("Enter an initial password of at least 8 characters, or untick 'Create a login'.");
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return Result<int>.Fail("A login with this email already exists. Untick 'Create a login' and link it later.");
            if (await db.Users.CountAsync(ct) >= limits.MaxEmployees)
                return Result<int>.Fail($"Your plan allows {limits.MaxEmployees} users.");
            var employeeRole = await db.Roles.FirstOrDefaultAsync(r => r.IsSystem && r.Name == "Employee", ct);
            if (employeeRole is null) return Result<int>.Fail("The system 'Employee' role is missing.");
            login = new User
            {
                Email = email, FullName = $"{e.FirstName.Trim()} {e.LastName.Trim()}", Phone = e.MobilePhone?.Trim(),
                PasswordHash = passwords.Hash(input.InitialPassword), MustChangePassword = true,
                UserRoles = [new UserRole { RoleId = employeeRole.Id }]
            };
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var settings = await SettingsAsync(ct);
        var number = settings.NextEmployeeNumber;
        var code = settings.FormatEmployeeCode(number);
        // Skip numbers already taken (e.g. after manual imports) — the unique index is the final guard.
        while (await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == OrgId && x.EmployeeCode == code, ct))
            code = settings.FormatEmployeeCode(++number);
        settings.NextEmployeeNumber = number + 1;

        if (login is not null) db.Users.Add(login);
        var employee = new Employee
        {
            EmployeeCode = code,
            Status = e.ProbationEndDate is { } pe && pe >= e.DateOfJoining ? EmployeeStatus.Probation : EmployeeStatus.Active,
            NoticePeriodDays = e.NoticePeriodDays ?? settings.DefaultNoticePeriodDays,
            User = login
        };
        ApplyEmployment(employee, e);
        db.Employees.Add(employee);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The settings row (employee number sequence) changed underneath us: another employee was added concurrently.
            return Result<int>.Fail("Another employee was added at the same moment. Please submit again.");
        }
        await tx.CommitAsync(ct);
        return Result<int>.Ok(employee.Id);
    }

    private static void ApplyEmployment(Employee x, EmploymentInput e)
    {
        x.FirstName = e.FirstName.Trim();
        x.MiddleName = string.IsNullOrWhiteSpace(e.MiddleName) ? null : e.MiddleName.Trim();
        x.LastName = e.LastName.Trim();
        x.WorkEmail = e.WorkEmail.Trim().ToLowerInvariant();
        x.MobilePhone = e.MobilePhone?.Trim();
        x.DateOfJoining = e.DateOfJoining;
        x.EmploymentType = e.EmploymentType;
        x.BranchId = e.BranchId;
        x.DepartmentId = e.DepartmentId;
        x.DesignationId = e.DesignationId;
        x.ReportingManagerId = e.ReportingManagerId;
        x.ProbationEndDate = e.ProbationEndDate;
        if (e.NoticePeriodDays is { } n) x.NoticePeriodDays = n;
    }

    /// <summary>All referenced ids are looked up through the tenant filter: another organization's ids never resolve.</summary>
    private async Task<string?> ValidateReferencesAsync(EmploymentInput e, int? employeeId, CancellationToken ct)
    {
        if (e.BranchId is int b && !await db.Branches.AnyAsync(x => x.Id == b, ct)) return "Branch not found.";
        if (e.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return "Department not found.";
        if (e.DesignationId is int g && !await db.Designations.AnyAsync(x => x.Id == g, ct)) return "Designation not found.";
        if (e.ProbationEndDate is { } pe && pe < e.DateOfJoining) return "Probation cannot end before the joining date.";
        if (e.ReportingManagerId is int m)
        {
            if (m == employeeId) return "An employee cannot report to themselves.";
            if (!await db.Employees.AnyAsync(x => x.Id == m && x.Status != EmployeeStatus.Exited, ct)) return "Reporting manager not found.";
            if (employeeId is int id)
            {
                // Walk up the chain from the new manager; reaching this employee would create a loop.
                var managers = await db.Employees.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ReportingManagerId, ct);
                int? cursor = m;
                for (var guard = 0; cursor is int c && guard < 500; guard++)
                {
                    if (c == id) return "This reporting line would create a loop (the manager reports to this employee).";
                    cursor = managers.GetValueOrDefault(c);
                }
            }
        }
        return null;
    }

    public async Task<Result> UpdateEmploymentAsync(int id, EmploymentInput input, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (employee is null) return Result.Fail("Employee not found.");
        var email = input.WorkEmail.Trim().ToLowerInvariant();
        if (await db.Employees.AnyAsync(x => x.WorkEmail == email && x.Id != id && x.Status != EmployeeStatus.Exited, ct))
            return Result.Fail("Another active employee uses this work email.");
        if (await ValidateReferencesAsync(input, id, ct) is { } err) return Result.Fail(err);
        ApplyEmployment(employee, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdatePersonalAsync(int id, PersonalInput input, bool canEditSensitive, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result.Fail("Employee not found.");
        if (input.DateOfBirth is { } dob)
        {
            if (dob >= clock.Today) return Result.Fail("Date of birth must be in the past.");
            if (dob.AddYears(14) > e.DateOfJoining) return Result.Fail("Employee must be at least 14 years old on the joining date.");
        }
        e.Gender = input.Gender;
        e.DateOfBirth = input.DateOfBirth;
        e.MaritalStatus = input.MaritalStatus;
        e.BloodGroup = input.BloodGroup?.Trim().ToUpperInvariant();
        e.Nationality = input.Nationality.Trim();
        e.PersonalEmail = input.PersonalEmail?.Trim().ToLowerInvariant();
        e.CurrentAddress = input.CurrentAddress?.Trim();
        e.PermanentAddress = input.PermanentAddress?.Trim();
        e.EmergencyContactName = input.EmergencyContactName?.Trim();
        e.EmergencyContactRelation = input.EmergencyContactRelation?.Trim();
        e.EmergencyContactPhone = input.EmergencyContactPhone?.Trim();
        e.Uan = input.Uan?.Trim();
        if (canEditSensitive && !string.IsNullOrWhiteSpace(input.Pan))
        {
            var pan = input.Pan.Trim().ToUpperInvariant();
            e.PanEncrypted = protector.Protect(pan);
            e.PanMasked = "XXXXXX" + pan[^4..];
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SaveBankAsync(int id, BankInput input, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == id, ct)) return Result.Fail("Employee not found.");
        var bank = await db.EmployeeBankDetails.FirstOrDefaultAsync(x => x.EmployeeId == id, ct)
                   ?? db.EmployeeBankDetails.Add(new EmployeeBankDetail { EmployeeId = id }).Entity;
        var account = input.AccountNumber.Trim();
        bank.BankName = input.BankName.Trim();
        bank.AccountHolderName = input.AccountHolderName.Trim();
        bank.AccountNumberEncrypted = protector.Protect(account);
        bank.AccountNumberLast4 = account[^4..];
        bank.Ifsc = input.Ifsc.Trim().ToUpperInvariant();
        bank.BranchName = input.BranchName?.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddEducationAsync(int id, EducationInput input, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == id, ct)) return Result.Fail("Employee not found.");
        db.EmployeeEducation.Add(new EmployeeEducation
        {
            EmployeeId = id, Qualification = input.Qualification.Trim(), Institution = input.Institution.Trim(),
            Specialization = input.Specialization?.Trim(), YearOfPassing = input.YearOfPassing, Grade = input.Grade?.Trim()
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteEducationAsync(int id, int educationId, CancellationToken ct = default)
    {
        var row = await db.EmployeeEducation.FirstOrDefaultAsync(x => x.Id == educationId && x.EmployeeId == id, ct);
        if (row is null) return Result.Fail("Record not found.");
        db.EmployeeEducation.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddExperienceAsync(int id, ExperienceInput input, CancellationToken ct = default)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == id, ct)) return Result.Fail("Employee not found.");
        if (input.ToDate is { } to && to < input.FromDate) return Result.Fail("'To' date cannot be before 'From' date.");
        db.EmployeeExperience.Add(new EmployeeExperience
        {
            EmployeeId = id, Company = input.Company.Trim(), Designation = input.Designation?.Trim(),
            FromDate = input.FromDate, ToDate = input.ToDate, Notes = input.Notes?.Trim()
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteExperienceAsync(int id, int experienceId, CancellationToken ct = default)
    {
        var row = await db.EmployeeExperience.FirstOrDefaultAsync(x => x.Id == experienceId && x.EmployeeId == id, ct);
        if (row is null) return Result.Fail("Record not found.");
        db.EmployeeExperience.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ConfirmProbationAsync(int id, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result.Fail("Employee not found.");
        if (e.Status != EmployeeStatus.Probation) return Result.Fail("Only employees on probation can be confirmed.");
        e.Status = EmployeeStatus.Active;
        e.ConfirmationDate = clock.Today;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CreateLoginAsync(int id, string password, CancellationToken ct = default)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result.Fail("Employee not found.");
        if (e.UserId is not null) return Result.Fail("This employee already has a login.");
        if (!e.IsCurrent) return Result.Fail("Logins can only be created for current employees.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return Result.Fail("Password must be at least 8 characters.");

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == e.WorkEmail, ct);
        if (existing is not null)
        {
            if (await db.Employees.AnyAsync(x => x.UserId == existing.Id, ct)) return Result.Fail("A login with this email is already linked to another employee.");
            e.UserId = existing.Id; // link the existing account (e.g. the organization owner)
        }
        else
        {
            var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
            if (await db.Users.CountAsync(ct) >= limits.MaxEmployees) return Result.Fail($"Your plan allows {limits.MaxEmployees} users.");
            var role = await db.Roles.FirstAsync(r => r.IsSystem && r.Name == "Employee", ct);
            e.User = new User
            {
                Email = e.WorkEmail, FullName = e.FullName, Phone = e.MobilePhone, PasswordHash = passwords.Hash(password),
                MustChangePassword = true, UserRoles = [new UserRole { RoleId = role.Id }]
            };
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<OrgChartNode>> OrgChartAsync(CancellationToken ct = default)
    {
        var rows = await db.Employees.AsNoTracking()
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .Select(e => new
            {
                e.Id, e.ReportingManagerId, Name = e.FirstName + " " + e.LastName, e.EmployeeCode,
                Designation = e.Designation != null ? e.Designation.Name : null,
                Department = e.Department != null ? e.Department.Name : null
            })
            .ToListAsync(ct);
        var nodes = rows.ToDictionary(r => r.Id, r => new OrgChartNode(r.Id, r.Name, r.EmployeeCode, r.Designation, r.Department, []));
        var roots = new List<OrgChartNode>();
        foreach (var r in rows.OrderBy(r => r.Name))
        {
            if (r.ReportingManagerId is int m && nodes.TryGetValue(m, out var parent)) parent.Reports.Add(nodes[r.Id]);
            else roots.Add(nodes[r.Id]);
        }
        return roots;
    }
}
