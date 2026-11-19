using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Application.Payroll;
using Codenexify.HRMS.Application.Time;
using Codenexify.HRMS.Domain.Benefits;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Benefits;

public sealed class BenefitPlanInput : IValidatableObject
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public BenefitCategory Category { get; set; } = BenefitCategory.HealthInsurance;
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(150), Display(Name = "Provider / insurer")] public string? Provider { get; set; }
    [StringLength(100), Display(Name = "Policy number")] public string? PolicyNumber { get; set; }
    [Range(0, 1_000_000_000), Display(Name = "Cover per employee (₹)")] public decimal? CoverageAmount { get; set; }
    [Display(Name = "Policy renews on")] public DateOnly? PolicyRenewsOn { get; set; }

    [Range(0, 10_000_000), Display(Name = "Company cost per employee / month (₹)")] public decimal EmployerCostPerMonth { get; set; }
    [Range(0, 10_000_000), Display(Name = "Employee contribution / month (₹)")] public decimal EmployeeContributionPerMonth { get; set; }
    [Range(0, 10_000_000), Display(Name = "Extra per dependent / month (₹)")] public decimal DependentContributionPerMonth { get; set; }
    [Range(0, 10_000_000), Display(Name = "Allowance paid / month (₹)")] public decimal AllowancePerMonth { get; set; }
    [Display(Name = "Allowance is taxable")] public bool AllowanceTaxable { get; set; } = true;

    [Display(Name = "Enrolment")] public BenefitEnrollmentMode EnrollmentMode { get; set; } = BenefitEnrollmentMode.Automatic;
    [Display(Name = "Covers family members")] public bool CoversDependents { get; set; }
    [Range(0, 10), Display(Name = "Maximum dependents")] public int MaxDependents { get; set; }
    [Display(Name = "Enrolment opens")] public DateOnly? EnrollmentOpensOn { get; set; }
    [Display(Name = "Enrolment closes")] public DateOnly? EnrollmentClosesOn { get; set; }

    [Display(Name = "Employment types (none ticked = all)")] public List<EmploymentType> EligibleEmploymentTypes { get; set; } = [];
    [Range(0, 3650), Display(Name = "Minimum service (days)")] public int MinServiceDays { get; set; }
    [Display(Name = "Branch")] public int? BranchId { get; set; }
    [Display(Name = "Department")] public int? DepartmentId { get; set; }
    [Display(Name = "Not during probation")] public bool ExcludeProbation { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EnrollmentOpensOn is { } o && EnrollmentClosesOn is { } c && c < o)
            yield return new ValidationResult("Enrolment closes before it opens.", [nameof(EnrollmentClosesOn)]);
        if (CoversDependents && MaxDependents < 1)
            yield return new ValidationResult("Set how many family members can be covered (at least 1).", [nameof(MaxDependents)]);
        if (!CoversDependents && DependentContributionPerMonth > 0)
            yield return new ValidationResult("A per-dependent amount needs 'Covers family members'.", [nameof(DependentContributionPerMonth)]);
        if (EnrollmentMode == BenefitEnrollmentMode.Automatic && (EnrollmentOpensOn is not null || EnrollmentClosesOn is not null))
            yield return new ValidationResult("An enrolment window applies only to opt-in plans.", [nameof(EnrollmentOpensOn)]);
    }
}

public sealed class DependentInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public DependentRelation Relation { get; set; } = DependentRelation.Spouse;
    [Display(Name = "Date of birth")] public DateOnly? DateOfBirth { get; set; }
}

public sealed record BenefitPlanRow(int Id, string Name, BenefitCategory Category, BenefitEnrollmentMode Mode, bool IsActive, int Members,
    decimal MonthlyEmployerCost, decimal MonthlyEmployeeContributions, decimal MonthlyAllowances);
public sealed record BenefitMemberRow(int EnrollmentId, int EmployeeId, string EmployeeName, string EmployeeCode, DateOnly StartsOn, DateOnly? EndsOn,
    BenefitEnrollmentStatus Status, string EnrolledBy, IReadOnlyList<BenefitDependent> Dependents, decimal MonthlyDeduction, string? EndReason);
public sealed record EligibleEmployee(int EmployeeId, string Name, string Code);
public sealed record BenefitPlanView(BenefitPlan Plan, string? BranchName, string? DepartmentName, IReadOnlyList<BenefitMemberRow> Members,
    IReadOnlyList<EligibleEmployee> EligibleNotEnrolled, IReadOnlyList<EligibleEmployee> IneligibleNotEnrolled);
public sealed record MyBenefit(BenefitPlan Plan, BenefitEnrollment? Enrollment, bool CanJoin, bool CanLeave, bool CanEditDependents, string? Note, decimal MonthlyDeduction);
public sealed record BenefitDocument(Stream Content, string FileName);

public interface IBenefitService
{
    Task<IReadOnlyList<BenefitPlanRow>> ListAsync(CancellationToken ct = default);
    Task<BenefitPlanView?> GetAsync(int id, CancellationToken ct = default);
    Task<BenefitPlanInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, BenefitPlanInput input, CancellationToken ct = default);
    Task<Result> UploadDocumentAsync(int planId, UploadedFile file, CancellationToken ct = default);
    /// <param name="employeeId">Null for HR; otherwise the employee must be enrolled in or eligible for the plan.</param>
    Task<BenefitDocument?> DocumentAsync(int planId, int? employeeId, CancellationToken ct = default);

    /// <summary>HR enrols one employee (eligibility is not required: HR may make exceptions).</summary>
    Task<Result> EnrollAsync(int planId, int employeeId, DateOnly startsOn, CancellationToken ct = default);
    Task<Result> EndAsync(int enrollmentId, DateOnly lastDay, string reason, CancellationToken ct = default);
    /// <summary>Automatic plans: enrol eligible current employees, end automatic memberships that lost eligibility; every plan: end memberships of employees who left.</summary>
    Task<int> SyncAsync(int? planId, CancellationToken ct = default);

    Task<IReadOnlyList<MyBenefit>> MineAsync(int employeeId, CancellationToken ct = default);
    Task<Result> JoinAsync(int planId, int employeeId, IReadOnlyList<DependentInput> dependents, CancellationToken ct = default);
    Task<Result> LeaveAsync(int planId, int employeeId, CancellationToken ct = default);
    /// <param name="employeeId">Null for HR.</param>
    Task<Result> SaveDependentsAsync(int enrollmentId, int? employeeId, IReadOnlyList<DependentInput> dependents, CancellationToken ct = default);

    /// <summary>Payroll lines for a month: employee contributions (deductions) and allowances (earnings) of every membership active on any day of it.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<OneTimeItem>>> PayrollItemsAsync(IReadOnlyCollection<int> employeeIds, DateOnly start, DateOnly end, CancellationToken ct = default);
    /// <summary>Insurer roster (employees and covered dependents) as Excel.</summary>
    Task<byte[]?> ExportMembersAsync(int planId, CancellationToken ct = default);
}

public sealed class BenefitService(IAppDbContext db, ICurrentContext context, IClock clock, IFileStorage storage, IExcelExporter excel,
    INotificationService notifications, IAuditWriter audit) : IBenefitService
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;
    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];

    private async Task<DateOnly> TodayAsync(CancellationToken ct) => (await db.OrgClockAsync(context.OrgId(), clock, ct)).Today;
    private async Task<string> MyNameAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";

    public static decimal MonthlyDeduction(BenefitPlan p, int dependents) =>
        p.EmployeeContributionPerMonth + (p.CoversDependents ? p.DependentContributionPerMonth * dependents : 0);

    /// <summary>Why an employee is not eligible (null = eligible).</summary>
    public static string? IneligibleReason(BenefitPlan p, Employee e, DateOnly today)
    {
        if (!e.IsCurrent) return "Not a current employee.";
        var types = ParseTypes(p.EligibleEmploymentTypes);
        if (types.Count > 0 && !types.Contains(e.EmploymentType)) return $"Only for {string.Join(", ", types.Select(Label))} employees.";
        if (p.MinServiceDays > 0 && today.DayNumber - e.DateOfJoining.DayNumber < p.MinServiceDays)
            return $"Available after {p.MinServiceDays} days of service (from {e.DateOfJoining.AddDays(p.MinServiceDays):dd MMM yyyy}).";
        if (p.BranchId is int b && e.BranchId != b) return "Only for another branch.";
        if (p.DepartmentId is int d && e.DepartmentId != d) return "Only for another department.";
        if (p.ExcludeProbation && e.Status == EmployeeStatus.Probation) return "Available after probation.";
        return null;
    }

    private static string Label(EmploymentType t) => t switch { EmploymentType.FullTime => "full-time", EmploymentType.PartTime => "part-time", _ => t.ToString().ToLowerInvariant() };

    public static List<EmploymentType> ParseTypes(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<EmploymentType>(s, out var t) ? t : (EmploymentType?)null).OfType<EmploymentType>().Distinct().ToList();

    private static bool WindowOpen(BenefitPlan p, DateOnly today) =>
        (p.EnrollmentOpensOn is null || today >= p.EnrollmentOpensOn) && (p.EnrollmentClosesOn is null || today <= p.EnrollmentClosesOn);

    // ------------------------------------------------------------------ plans (HR)

    public async Task<IReadOnlyList<BenefitPlanRow>> ListAsync(CancellationToken ct = default)
    {
        var plans = await db.BenefitPlans.AsNoTracking().OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToListAsync(ct);
        var active = await db.BenefitEnrollments.AsNoTracking().Where(e => e.Status == BenefitEnrollmentStatus.Active)
            .Select(e => new { e.BenefitPlanId, Dependents = e.Dependents.Count }).ToListAsync(ct);
        return plans.Select(p =>
        {
            var m = active.Where(a => a.BenefitPlanId == p.Id).ToList();
            return new BenefitPlanRow(p.Id, p.Name, p.Category, p.EnrollmentMode, p.IsActive, m.Count,
                m.Count * p.EmployerCostPerMonth, m.Sum(x => MonthlyDeduction(p, x.Dependents)), m.Count * p.AllowancePerMonth);
        }).ToList();
    }

    public async Task<BenefitPlanView?> GetAsync(int id, CancellationToken ct = default)
    {
        var plan = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return null;
        var branch = plan.BranchId is int b ? await db.Branches.Where(x => x.Id == b).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var dept = plan.DepartmentId is int d ? await db.Departments.Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var enrollments = await db.BenefitEnrollments.AsNoTracking().Include(e => e.Dependents).Where(e => e.BenefitPlanId == id)
            .OrderByDescending(e => e.Status == BenefitEnrollmentStatus.Active).ThenByDescending(e => e.StartsOn).Take(1000).ToListAsync(ct);
        var empIds = enrollments.Select(e => e.EmployeeId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName, e.EmployeeCode }).ToListAsync(ct);
        var members = enrollments.Select(e =>
        {
            var n = names.First(x => x.Id == e.EmployeeId);
            var name = string.Join(' ', new[] { n.FirstName, n.MiddleName, n.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new BenefitMemberRow(e.Id, e.EmployeeId, name, n.EmployeeCode, e.StartsOn, e.EndsOn, e.Status, e.EnrolledBy,
                e.Dependents.Where(x => !x.IsDeleted).ToList(), MonthlyDeduction(plan, e.Dependents.Count(x => !x.IsDeleted)), e.EndReason);
        }).ToList();

        var today = await TodayAsync(ct);
        var activeIds = enrollments.Where(e => e.Status == BenefitEnrollmentStatus.Active).Select(e => e.EmployeeId).ToHashSet();
        var current = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .OrderBy(e => e.FirstName).ToListAsync(ct);
        var notEnrolled = current.Where(e => !activeIds.Contains(e.Id)).ToList();
        EligibleEmployee Row(Employee e) => new(e.Id, e.FullName, e.EmployeeCode);
        return new BenefitPlanView(plan, branch, dept, members,
            notEnrolled.Where(e => IneligibleReason(plan, e, today) is null).Select(Row).ToList(),
            notEnrolled.Where(e => IneligibleReason(plan, e, today) is not null).Select(Row).ToList());
    }

    public async Task<BenefitPlanInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : new BenefitPlanInput
        {
            Name = p.Name, Category = p.Category, Description = p.Description, Provider = p.Provider, PolicyNumber = p.PolicyNumber,
            CoverageAmount = p.CoverageAmount, PolicyRenewsOn = p.PolicyRenewsOn, EmployerCostPerMonth = p.EmployerCostPerMonth,
            EmployeeContributionPerMonth = p.EmployeeContributionPerMonth, DependentContributionPerMonth = p.DependentContributionPerMonth,
            AllowancePerMonth = p.AllowancePerMonth, AllowanceTaxable = p.AllowanceTaxable, EnrollmentMode = p.EnrollmentMode,
            CoversDependents = p.CoversDependents, MaxDependents = p.MaxDependents, EnrollmentOpensOn = p.EnrollmentOpensOn, EnrollmentClosesOn = p.EnrollmentClosesOn,
            EligibleEmploymentTypes = ParseTypes(p.EligibleEmploymentTypes), MinServiceDays = p.MinServiceDays, BranchId = p.BranchId,
            DepartmentId = p.DepartmentId, ExcludeProbation = p.ExcludeProbation, IsActive = p.IsActive
        };
    }

    public async Task<Result<int>> SaveAsync(int? id, BenefitPlanInput i, CancellationToken ct = default)
    {
        var name = i.Name.Trim();
        if (await db.BenefitPlans.AnyAsync(p => p.Name == name && p.Id != (id ?? 0), ct)) return Result<int>.Fail("Another plan already has this name.");
        if (i.BranchId is int b && !await db.Branches.AnyAsync(x => x.Id == b, ct)) return Result<int>.Fail("Branch not found.");
        if (i.DepartmentId is int d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) return Result<int>.Fail("Department not found.");

        BenefitPlan plan;
        var wasActive = true;
        if (id is int existing)
        {
            plan = await db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == existing, ct) ?? throw new InvalidOperationException();
            wasActive = plan.IsActive;
            if (!i.CoversDependents && plan.CoversDependents
                && await db.BenefitDependents.AnyAsync(x => db.BenefitEnrollments.Any(e => e.Id == x.BenefitEnrollmentId && e.BenefitPlanId == plan.Id && e.Status == BenefitEnrollmentStatus.Active), ct))
                return Result<int>.Fail("Members have dependents on this plan. Remove them before turning off family cover.");
            if (i.CoversDependents && plan.CoversDependents && i.MaxDependents < plan.MaxDependents
                && await db.BenefitEnrollments.AnyAsync(e => e.BenefitPlanId == plan.Id && e.Status == BenefitEnrollmentStatus.Active && e.Dependents.Count(x => !x.IsDeleted) > i.MaxDependents, ct))
                return Result<int>.Fail($"Some members already cover more than {i.MaxDependents} dependents.");
        }
        else
        {
            plan = new BenefitPlan();
            db.BenefitPlans.Add(plan);
        }

        plan.Name = name;
        plan.Category = i.Category;
        plan.Description = Clean(i.Description);
        plan.Provider = Clean(i.Provider);
        plan.PolicyNumber = Clean(i.PolicyNumber);
        plan.CoverageAmount = i.CoverageAmount;
        plan.PolicyRenewsOn = i.PolicyRenewsOn;
        plan.EmployerCostPerMonth = i.EmployerCostPerMonth;
        plan.EmployeeContributionPerMonth = i.EmployeeContributionPerMonth;
        plan.DependentContributionPerMonth = i.CoversDependents ? i.DependentContributionPerMonth : 0;
        plan.AllowancePerMonth = i.AllowancePerMonth;
        plan.AllowanceTaxable = i.AllowanceTaxable;
        plan.EnrollmentMode = i.EnrollmentMode;
        plan.CoversDependents = i.CoversDependents;
        plan.MaxDependents = i.CoversDependents ? i.MaxDependents : 0;
        plan.EnrollmentOpensOn = i.EnrollmentMode == BenefitEnrollmentMode.OptIn ? i.EnrollmentOpensOn : null;
        plan.EnrollmentClosesOn = i.EnrollmentMode == BenefitEnrollmentMode.OptIn ? i.EnrollmentClosesOn : null;
        plan.EligibleEmploymentTypes = i.EligibleEmploymentTypes.Count == 0 ? null : string.Join(",", i.EligibleEmploymentTypes.Distinct());
        plan.MinServiceDays = i.MinServiceDays;
        plan.BranchId = i.BranchId;
        plan.DepartmentId = i.DepartmentId;
        plan.ExcludeProbation = i.ExcludeProbation;
        plan.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);

        if (wasActive && !plan.IsActive)
        {
            // Deactivating a plan ends every membership today; contributions stop from next month.
            var today = await TodayAsync(ct);
            foreach (var e in await db.BenefitEnrollments.Where(e => e.BenefitPlanId == plan.Id && e.Status == BenefitEnrollmentStatus.Active).ToListAsync(ct))
                End(e, today, "Plan discontinued");
            audit.Add("Benefit.PlanDiscontinued", nameof(BenefitPlan), plan.Id.ToString(), plan.Name);
            await db.SaveChangesAsync(ct);
        }
        if (plan.IsActive) await SyncAsync(plan.Id, ct);
        return Result<int>.Ok(plan.Id);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<Result> UploadDocumentAsync(int planId, UploadedFile file, CancellationToken ct = default)
    {
        var plan = await db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result.Fail("Plan not found.");
        if (file.Length == 0 || file.Length > MaxDocumentBytes) return Result.Fail("Upload a PDF of up to 10 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)) return Result.Fail("Only PDF files are accepted.");
        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        if (buffer.Length < 4 || !buffer.GetBuffer().AsSpan(0, 4).SequenceEqual(PdfSignature)) return Result.Fail("The file is not a valid PDF.");
        buffer.Position = 0;
        var key = await storage.SaveAsync(context.OrgId(), buffer, ".pdf", ct);
        var old = plan.DocumentStorageKey;
        plan.DocumentStorageKey = key;
        plan.DocumentFileName = Path.GetFileName(file.FileName) is { Length: > 200 } n ? n[^200..] : Path.GetFileName(file.FileName);
        await db.SaveChangesAsync(ct);
        if (old is not null) await storage.DeleteAsync(old, ct);
        return Result.Ok();
    }

    public async Task<BenefitDocument?> DocumentAsync(int planId, int? employeeId, CancellationToken ct = default)
    {
        var plan = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan?.DocumentStorageKey is null) return null;
        if (employeeId is int me)
        {
            var mine = (await MineAsync(me, ct)).Any(b => b.Plan.Id == planId);
            if (!mine) return null;
        }
        var stream = await storage.OpenReadAsync(plan.DocumentStorageKey, ct);
        return stream is null ? null : new BenefitDocument(stream, plan.DocumentFileName ?? "policy.pdf");
    }

    // ------------------------------------------------------------------ enrolments (HR)

    public async Task<Result> EnrollAsync(int planId, int employeeId, DateOnly startsOn, CancellationToken ct = default)
    {
        var plan = await db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null || !plan.IsActive) return Result.Fail("The plan is not active.");
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null || !emp.IsCurrent) return Result.Fail("Only current employees can be enrolled.");
        if (await db.BenefitEnrollments.AnyAsync(e => e.BenefitPlanId == planId && e.EmployeeId == employeeId && e.Status == BenefitEnrollmentStatus.Active, ct))
            return Result.Fail($"{emp.FullName} is already enrolled.");
        if (startsOn < emp.DateOfJoining) return Result.Fail("The start date is before the employee joined.");
        var today = await TodayAsync(ct);
        if (startsOn < today.AddMonths(-3)) return Result.Fail("The start date can be at most three months back.");
        db.BenefitEnrollments.Add(new BenefitEnrollment { BenefitPlanId = planId, EmployeeId = employeeId, StartsOn = startsOn, EnrolledBy = await MyNameAsync(ct) });
        audit.Add("Benefit.Enrolled", nameof(BenefitPlan), planId.ToString(), $"{emp.FullName} from {startsOn:dd MMM yyyy}{(IneligibleReason(plan, emp, today) is { } why ? $" (exception: {why})" : "")}");
        await db.SaveChangesAsync(ct);
        if (emp.UserId is int u) notifications.Queue([u], "You're enrolled in a benefit", $"{plan.Name} from {startsOn:dd MMM yyyy}.", "/me/benefits");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> EndAsync(int enrollmentId, DateOnly lastDay, string reason, CancellationToken ct = default)
    {
        var e = await db.BenefitEnrollments.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == enrollmentId, ct);
        if (e is null || e.Status != BenefitEnrollmentStatus.Active) return Result.Fail("This membership has already ended.");
        if (lastDay < e.StartsOn) return Result.Fail("The last day is before the membership started.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason.");
        End(e, lastDay, reason.Trim());
        audit.Add("Benefit.Ended", nameof(BenefitPlan), e.BenefitPlanId.ToString(), $"Enrolment {e.Id} ends {lastDay:dd MMM yyyy}: {reason.Trim()}");
        await db.SaveChangesAsync(ct);
        var userId = await db.Employees.Where(x => x.Id == e.EmployeeId).Select(x => x.UserId).FirstOrDefaultAsync(ct);
        if (userId is int u) notifications.Queue([u], "Benefit membership ending", $"{e.Plan.Name}: last day {lastDay:dd MMM yyyy}.", "/me/benefits");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static void End(BenefitEnrollment e, DateOnly lastDay, string reason)
    {
        e.Status = BenefitEnrollmentStatus.Ended;
        e.EndsOn = e.EndsOn is { } existing && existing < lastDay ? existing : lastDay;
        if (e.EndsOn < e.StartsOn) e.EndsOn = e.StartsOn;
        e.EndReason = reason.Length > 300 ? reason[..300] : reason;
    }

    public async Task<int> SyncAsync(int? planId, CancellationToken ct = default)
    {
        var today = await TodayAsync(ct);
        var changes = 0;

        // Anyone who left: membership ends on the exit date (or today if none was recorded).
        var leavers = await db.BenefitEnrollments
            .Where(e => e.Status == BenefitEnrollmentStatus.Active && (planId == null || e.BenefitPlanId == planId))
            .Join(db.Employees.Where(x => x.Status == EmployeeStatus.Exited || x.Status == EmployeeStatus.Inactive), e => e.EmployeeId, x => x.Id, (e, x) => new { e, x.DateOfExit })
            .ToListAsync(ct);
        foreach (var l in leavers) { End(l.e, l.DateOfExit ?? today, "Left the company"); changes++; }

        var plans = await db.BenefitPlans.Where(p => p.IsActive && p.EnrollmentMode == BenefitEnrollmentMode.Automatic && (planId == null || p.Id == planId)).ToListAsync(ct);
        if (plans.Count > 0)
        {
            var current = await db.Employees.AsNoTracking().Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive).ToListAsync(ct);
            var planIds = plans.Select(p => p.Id).ToList();
            var active = await db.BenefitEnrollments.Where(e => planIds.Contains(e.BenefitPlanId) && e.Status == BenefitEnrollmentStatus.Active).ToListAsync(ct);
            foreach (var plan in plans)
            {
                var mine = active.Where(a => a.BenefitPlanId == plan.Id).ToDictionary(a => a.EmployeeId);
                foreach (var emp in current)
                {
                    var eligible = IneligibleReason(plan, emp, today) is null;
                    if (eligible && !mine.ContainsKey(emp.Id))
                    {
                        db.BenefitEnrollments.Add(new BenefitEnrollment { BenefitPlanId = plan.Id, EmployeeId = emp.Id, StartsOn = today, EnrolledBy = "Automatic" });
                        if (emp.UserId is int u) notifications.Queue([u], "New benefit", $"You're now covered by {plan.Name}.", "/me/benefits");
                        changes++;
                    }
                    // Only automatic memberships are withdrawn; HR exceptions stay until HR ends them.
                    else if (!eligible && mine.TryGetValue(emp.Id, out var en) && en.EnrolledBy == "Automatic")
                    {
                        End(en, today, "No longer eligible");
                        changes++;
                    }
                }
            }
        }
        if (changes > 0) await db.SaveChangesAsync(ct);
        return changes;
    }

    // ------------------------------------------------------------------ self-service

    public async Task<IReadOnlyList<MyBenefit>> MineAsync(int employeeId, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null) return [];
        var today = await TodayAsync(ct);
        var plans = await db.BenefitPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync(ct);
        var enrollments = await db.BenefitEnrollments.AsNoTracking().Include(e => e.Dependents)
            .Where(e => e.EmployeeId == employeeId && (e.Status == BenefitEnrollmentStatus.Active || e.EndsOn >= today)).ToListAsync(ct);
        var result = new List<MyBenefit>();
        foreach (var p in plans)
        {
            var en = enrollments.Where(e => e.BenefitPlanId == p.Id).OrderByDescending(e => e.Status == BenefitEnrollmentStatus.Active).ThenByDescending(e => e.Id).FirstOrDefault();
            var why = IneligibleReason(p, emp, today);
            if (en is null && why is not null && p.EnrollmentMode == BenefitEnrollmentMode.Automatic) continue; // nothing to show
            var activeEn = en?.Status == BenefitEnrollmentStatus.Active ? en : null;
            var optIn = p.EnrollmentMode == BenefitEnrollmentMode.OptIn;
            var open = WindowOpen(p, today);
            string? note = activeEn is not null ? null
                : en is not null ? $"Ends on {en.EndsOn:dd MMM yyyy}."
                : why ?? (!open ? (p.EnrollmentOpensOn > today ? $"Enrolment opens on {p.EnrollmentOpensOn:dd MMM yyyy}." : "Enrolment is closed.") : null);
            result.Add(new MyBenefit(p, en, CanJoin: optIn && activeEn is null && en is null && why is null && open,
                CanLeave: optIn && activeEn is not null, CanEditDependents: activeEn is not null && p.CoversDependents,
                note, MonthlyDeduction(p, en?.Dependents.Count(d => !d.IsDeleted) ?? 0)));
        }
        return result;
    }

    public async Task<Result> JoinAsync(int planId, int employeeId, IReadOnlyList<DependentInput> dependents, CancellationToken ct = default)
    {
        var plan = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (plan is null || emp is null) return Result.Fail("Plan not found.");
        if (plan.EnrollmentMode != BenefitEnrollmentMode.OptIn) return Result.Fail("You're enrolled in this plan automatically when eligible.");
        var today = await TodayAsync(ct);
        if (IneligibleReason(plan, emp, today) is { } why) return Result.Fail(why);
        if (!WindowOpen(plan, today)) return Result.Fail("Enrolment for this plan is closed.");
        if (await db.BenefitEnrollments.AnyAsync(e => e.BenefitPlanId == planId && e.EmployeeId == employeeId && (e.Status == BenefitEnrollmentStatus.Active || e.EndsOn >= today), ct))
            return Result.Fail("You're already in this plan (or your membership is ending this month).");
        if (ValidateDependents(plan, dependents, today) is { } bad) return Result.Fail(bad);

        var en = new BenefitEnrollment { BenefitPlanId = planId, EmployeeId = employeeId, StartsOn = today, EnrolledBy = "Employee" };
        foreach (var d in dependents) en.Dependents.Add(new BenefitDependent { Name = d.Name.Trim(), Relation = d.Relation, DateOfBirth = d.DateOfBirth });
        db.BenefitEnrollments.Add(en);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> LeaveAsync(int planId, int employeeId, CancellationToken ct = default)
    {
        var en = await db.BenefitEnrollments.Include(e => e.Plan)
            .FirstOrDefaultAsync(e => e.BenefitPlanId == planId && e.EmployeeId == employeeId && e.Status == BenefitEnrollmentStatus.Active, ct);
        if (en is null) return Result.Fail("You're not enrolled in this plan.");
        if (en.Plan.EnrollmentMode != BenefitEnrollmentMode.OptIn) return Result.Fail("This benefit is provided by the company; ask HR if you need to leave it.");
        var today = await TodayAsync(ct);
        // Cover continues to the end of the month already paid for.
        var monthEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        End(en, monthEnd, "Opted out by employee");
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SaveDependentsAsync(int enrollmentId, int? employeeId, IReadOnlyList<DependentInput> dependents, CancellationToken ct = default)
    {
        var en = await db.BenefitEnrollments.Include(e => e.Plan).Include(e => e.Dependents)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && (employeeId == null || e.EmployeeId == employeeId), ct);
        if (en is null || en.Status != BenefitEnrollmentStatus.Active) return Result.Fail("Membership not found.");
        if (!en.Plan.CoversDependents) return Result.Fail("This plan doesn't cover family members.");
        if (ValidateDependents(en.Plan, dependents, await TodayAsync(ct)) is { } bad) return Result.Fail(bad);
        foreach (var old in en.Dependents.Where(d => !d.IsDeleted).ToList()) db.BenefitDependents.Remove(old);
        foreach (var d in dependents) en.Dependents.Add(new BenefitDependent { Name = d.Name.Trim(), Relation = d.Relation, DateOfBirth = d.DateOfBirth });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static string? ValidateDependents(BenefitPlan plan, IReadOnlyList<DependentInput> dependents, DateOnly today)
    {
        if (dependents.Count == 0) return null;
        if (!plan.CoversDependents) return "This plan doesn't cover family members.";
        if (dependents.Count > plan.MaxDependents) return $"This plan covers at most {plan.MaxDependents} family member(s).";
        if (dependents.Any(d => string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > 150)) return "Enter each family member's name.";
        if (dependents.Any(d => d.DateOfBirth > today)) return "A date of birth is in the future.";
        if (dependents.Count(d => d.Relation == DependentRelation.Spouse) > 1) return "Only one spouse can be covered.";
        return null;
    }

    // ------------------------------------------------------------------ payroll & reports

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<OneTimeItem>>> PayrollItemsAsync(IReadOnlyCollection<int> employeeIds, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        var rows = await db.BenefitEnrollments.AsNoTracking().Include(e => e.Plan)
            .Where(e => employeeIds.Contains(e.EmployeeId) && e.StartsOn <= end && (e.EndsOn == null || e.EndsOn >= start))
            .Select(e => new { e.EmployeeId, e.Plan, Dependents = e.Dependents.Count(d => !d.IsDeleted) })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g =>
        {
            var items = new List<OneTimeItem>();
            foreach (var r in g.GroupBy(x => x.Plan.Id).Select(x => x.First()))
            {
                if (r.Plan.AllowancePerMonth > 0) items.Add(new OneTimeItem($"BENA{r.Plan.Id}", r.Plan.Name, false, r.Plan.AllowancePerMonth, r.Plan.AllowanceTaxable));
                var deduction = MonthlyDeduction(r.Plan, r.Dependents);
                if (deduction > 0) items.Add(new OneTimeItem($"BEN{r.Plan.Id}", $"{r.Plan.Name} (your share)", true, deduction, false));
            }
            return (IReadOnlyList<OneTimeItem>)items;
        });
    }

    public async Task<byte[]?> ExportMembersAsync(int planId, CancellationToken ct = default)
    {
        var view = await GetAsync(planId, ct);
        if (view is null) return null;
        var ids = view.Members.Select(m => m.EmployeeId).Distinct().ToList();
        var people = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Gender, e.DateOfBirth, e.DateOfJoining, e.WorkEmail }).ToDictionaryAsync(e => e.Id, ct);
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var m in view.Members)
        {
            var p = people[m.EmployeeId];
            rows.Add([m.EmployeeCode, m.EmployeeName, "Self", p.Gender?.ToString(), p.DateOfBirth?.ToString("dd-MM-yyyy"), p.DateOfJoining.ToString("dd-MM-yyyy"),
                p.WorkEmail, m.StartsOn.ToString("dd-MM-yyyy"), m.EndsOn?.ToString("dd-MM-yyyy"), m.Status.ToString(), m.MonthlyDeduction]);
            foreach (var d in m.Dependents)
                rows.Add([m.EmployeeCode, d.Name, d.Relation.ToString(), null, d.DateOfBirth?.ToString("dd-MM-yyyy"), null, null,
                    m.StartsOn.ToString("dd-MM-yyyy"), m.EndsOn?.ToString("dd-MM-yyyy"), m.Status.ToString(), null]);
        }
        return excel.Export("Members", ["Employee code", "Name", "Relation", "Gender", "Date of birth", "Date of joining", "Work email",
            "Covered from", "Covered until", "Status", "Employee contribution / month"], rows);
    }
}
