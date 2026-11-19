using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.Subscriptions;
using Codenexify.HRMS.Domain.CoreHr;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.CoreHr;

/// <summary>Starting HR configuration for a new organization; every value is editable afterwards.</summary>
public static class HrDefaults
{
    public static void AddTo(IAppDbContext db, int organizationId)
    {
        db.OrganizationSettings.Add(new OrganizationSetting { OrganizationId = organizationId });
        db.ExitChecklistTemplateItems.AddRange(ChecklistFor(organizationId));
    }

    private static IEnumerable<ExitChecklistTemplateItem> ChecklistFor(int organizationId)
    {
        var items = new (string Title, string Owner)[]
        {
            ("Knowledge transfer & handover", "Manager"),
            ("Return company assets (laptop, ID card, access card)", "IT"),
            ("Revoke system & email access", "IT"),
            ("Clear advances, loans & pending expenses", "Finance"),
            ("Exit interview", "HR"),
            ("Full & final settlement", "Finance"),
            ("Issue relieving & experience letters", "HR"),
        };
        for (var i = 0; i < items.Length; i++)
            yield return new ExitChecklistTemplateItem { OrganizationId = organizationId, Title = items[i].Title, Owner = items[i].Owner, SortOrder = (i + 1) * 10 };
    }
}

public sealed class BranchInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(20), RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "Letters, digits, - and _ only.")] public string Code { get; set; } = string.Empty;
    [StringLength(300), Display(Name = "Address")] public string? AddressLine { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [Required, StringLength(100)] public string Country { get; set; } = "India";
    [StringLength(10), Display(Name = "PIN code")] public string? PostalCode { get; set; }
    [Phone, StringLength(20)] public string? Phone { get; set; }
    [Display(Name = "Head office")] public bool IsHeadOffice { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed class DepartmentInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(20)] public string? Code { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Parent department")] public int? ParentDepartmentId { get; set; }
    [Display(Name = "Department head")] public int? HeadEmployeeId { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed class DesignationInput
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Range(1, 50), Display(Name = "Grade / level")] public int? Level { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed class HrSettingsInput
{
    [Required, StringLength(10), RegularExpression("^[A-Za-z0-9-]*$", ErrorMessage = "Letters, digits and hyphens only."), Display(Name = "Employee ID prefix")]
    public string EmployeeIdPrefix { get; set; } = "EMP";
    [Range(1, 8), Display(Name = "Number of digits")] public int EmployeeIdPadding { get; set; } = 4;
    [Range(1, 9_999_999), Display(Name = "Next employee number")] public int NextEmployeeNumber { get; set; } = 1;
    [Range(0, 365), Display(Name = "Default notice period (days)")] public int DefaultNoticePeriodDays { get; set; } = 30;
    [Range(0, 24), Display(Name = "Default probation (months)")] public int DefaultProbationMonths { get; set; } = 6;
    [Range(0, 100), Display(Name = "Points per on-time day")] public int OnTimePoints { get; set; } = 10;
    [Range(0, 100), Display(Name = "Points per late day")] public int LatePoints { get; set; } = 3;
}

public sealed record BranchRow(int Id, string Name, string Code, string? City, string? State, bool IsHeadOffice, bool IsActive, int EmployeeCount);
public sealed record DepartmentRow(int Id, string Name, string? Code, string? ParentName, string? HeadName, bool IsActive, int EmployeeCount, int? ParentId);
public sealed record DesignationRow(int Id, string Name, int? Level, string? Description, bool IsActive, int EmployeeCount);
public sealed record LookupItem(int Id, string Name);

public interface IStructureService
{
    Task<IReadOnlyList<BranchRow>> BranchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentRow>> DepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DesignationRow>> DesignationsAsync(CancellationToken ct = default);
    Task<BranchInput?> GetBranchAsync(int id, CancellationToken ct = default);
    Task<DepartmentInput?> GetDepartmentAsync(int id, CancellationToken ct = default);
    Task<DesignationInput?> GetDesignationAsync(int id, CancellationToken ct = default);
    Task<Result> SaveBranchAsync(int? id, BranchInput input, CancellationToken ct = default);
    Task<Result> SaveDepartmentAsync(int? id, DepartmentInput input, CancellationToken ct = default);
    Task<Result> SaveDesignationAsync(int? id, DesignationInput input, CancellationToken ct = default);
    Task<Result> DeleteBranchAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteDepartmentAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteDesignationAsync(int id, CancellationToken ct = default);

    Task<HrSettingsInput> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> SaveSettingsAsync(HrSettingsInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ExitChecklistTemplateItem>> ChecklistTemplateAsync(CancellationToken ct = default);
    Task<Result> AddChecklistTemplateItemAsync(string title, string owner, CancellationToken ct = default);
    Task<Result> DeleteChecklistTemplateItemAsync(int id, CancellationToken ct = default);

    /// <summary>Active lookups for dropdowns (tenant-scoped).</summary>
    Task<(IReadOnlyList<LookupItem> Branches, IReadOnlyList<LookupItem> Departments, IReadOnlyList<LookupItem> Designations)> LookupsAsync(CancellationToken ct = default);
}

public sealed class StructureService(IAppDbContext db, ICurrentContext context, ISubscriptionService subscriptions) : IStructureService
{
    private int OrgId => context.OrganizationId ?? throw new TenantIsolationException("No organization in the current context.");

    public async Task<IReadOnlyList<BranchRow>> BranchesAsync(CancellationToken ct = default) =>
        await db.Branches.AsNoTracking().OrderByDescending(b => b.IsHeadOffice).ThenBy(b => b.Name)
            .Select(b => new BranchRow(b.Id, b.Name, b.Code, b.City, b.State, b.IsHeadOffice, b.IsActive,
                db.Employees.Count(e => e.BranchId == b.Id && e.Status != EmployeeStatus.Exited)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DepartmentRow>> DepartmentsAsync(CancellationToken ct = default) =>
        await db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentRow(d.Id, d.Name, d.Code,
                d.ParentDepartment != null ? d.ParentDepartment.Name : null,
                d.HeadEmployee != null ? d.HeadEmployee.FirstName + " " + d.HeadEmployee.LastName : null,
                d.IsActive, db.Employees.Count(e => e.DepartmentId == d.Id && e.Status != EmployeeStatus.Exited), d.ParentDepartmentId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DesignationRow>> DesignationsAsync(CancellationToken ct = default) =>
        await db.Designations.AsNoTracking().OrderBy(d => d.Level).ThenBy(d => d.Name)
            .Select(d => new DesignationRow(d.Id, d.Name, d.Level, d.Description, d.IsActive,
                db.Employees.Count(e => e.DesignationId == d.Id && e.Status != EmployeeStatus.Exited)))
            .ToListAsync(ct);

    public async Task<BranchInput?> GetBranchAsync(int id, CancellationToken ct = default) =>
        await db.Branches.AsNoTracking().Where(b => b.Id == id).Select(b => new BranchInput
        {
            Name = b.Name, Code = b.Code, AddressLine = b.AddressLine, City = b.City, State = b.State, Country = b.Country,
            PostalCode = b.PostalCode, Phone = b.Phone, IsHeadOffice = b.IsHeadOffice, IsActive = b.IsActive
        }).FirstOrDefaultAsync(ct);

    public async Task<DepartmentInput?> GetDepartmentAsync(int id, CancellationToken ct = default) =>
        await db.Departments.AsNoTracking().Where(d => d.Id == id).Select(d => new DepartmentInput
        {
            Name = d.Name, Code = d.Code, Description = d.Description, ParentDepartmentId = d.ParentDepartmentId,
            HeadEmployeeId = d.HeadEmployeeId, IsActive = d.IsActive
        }).FirstOrDefaultAsync(ct);

    public async Task<DesignationInput?> GetDesignationAsync(int id, CancellationToken ct = default) =>
        await db.Designations.AsNoTracking().Where(d => d.Id == id).Select(d => new DesignationInput
        {
            Name = d.Name, Level = d.Level, Description = d.Description, IsActive = d.IsActive
        }).FirstOrDefaultAsync(ct);

    public async Task<Result> SaveBranchAsync(int? id, BranchInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Branches.AnyAsync(b => b.Code == code && b.Id != id, ct)) return Result.Fail($"Branch code {code} is already used.");

        Branch? branch;
        if (id is null)
        {
            var limits = await subscriptions.GetLimitsAsync(OrgId, ct);
            if (await db.Branches.CountAsync(ct) >= limits.MaxBranches)
                return Result.Fail($"Your plan allows {limits.MaxBranches} branch(es). Upgrade your subscription to add more.");
            branch = db.Branches.Add(new Branch()).Entity;
        }
        else
        {
            branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (branch is null) return Result.Fail("Branch not found.");
        }

        branch.Name = input.Name.Trim();
        branch.Code = code;
        branch.AddressLine = input.AddressLine?.Trim();
        branch.City = input.City?.Trim();
        branch.State = input.State?.Trim();
        branch.Country = input.Country.Trim();
        branch.PostalCode = input.PostalCode?.Trim();
        branch.Phone = input.Phone?.Trim();
        branch.IsActive = input.IsActive;
        branch.IsHeadOffice = input.IsHeadOffice;
        if (input.IsHeadOffice)
        {
            // Only one head office per organization.
            foreach (var other in await db.Branches.Where(b => b.IsHeadOffice && b.Id != branch.Id).ToListAsync(ct)) other.IsHeadOffice = false;
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SaveDepartmentAsync(int? id, DepartmentInput input, CancellationToken ct = default)
    {
        var name = input.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct)) return Result.Fail($"Department {name} already exists.");
        // Referenced ids are checked through the tenant filter, so another organization's rows are never accepted.
        if (input.ParentDepartmentId is int pid && !await db.Departments.AnyAsync(d => d.Id == pid, ct)) return Result.Fail("Parent department not found.");
        if (input.HeadEmployeeId is int hid && !await db.Employees.AnyAsync(e => e.Id == hid, ct)) return Result.Fail("Department head not found.");

        Department? dept;
        if (id is null) dept = db.Departments.Add(new Department()).Entity;
        else
        {
            dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (dept is null) return Result.Fail("Department not found.");
            if (input.ParentDepartmentId is int p && await IsDescendantOrSelfAsync(p, id.Value, ct))
                return Result.Fail("A department cannot sit under itself or one of its sub-departments.");
        }
        dept.Name = name;
        dept.Code = input.Code?.Trim();
        dept.Description = input.Description?.Trim();
        dept.ParentDepartmentId = input.ParentDepartmentId;
        dept.HeadEmployeeId = input.HeadEmployeeId;
        dept.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<bool> IsDescendantOrSelfAsync(int candidateId, int departmentId, CancellationToken ct)
    {
        var parents = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.ParentDepartmentId, ct);
        int? cursor = candidateId;
        for (var guard = 0; cursor is int c && guard < 200; guard++)
        {
            if (c == departmentId) return true;
            cursor = parents.GetValueOrDefault(c);
        }
        return false;
    }

    public async Task<Result> SaveDesignationAsync(int? id, DesignationInput input, CancellationToken ct = default)
    {
        var name = input.Name.Trim();
        if (await db.Designations.AnyAsync(d => d.Name == name && d.Id != id, ct)) return Result.Fail($"Designation {name} already exists.");
        Designation? des;
        if (id is null) des = db.Designations.Add(new Designation()).Entity;
        else
        {
            des = await db.Designations.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (des is null) return Result.Fail("Designation not found.");
        }
        des.Name = name;
        des.Level = input.Level;
        des.Description = input.Description?.Trim();
        des.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteBranchAsync(int id, CancellationToken ct = default)
    {
        var b = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return Result.Fail("Branch not found.");
        if (await db.Employees.AnyAsync(e => e.BranchId == id, ct)) return Result.Fail("Employees are assigned to this branch. Move them or mark the branch inactive.");
        db.Branches.Remove(b);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteDepartmentAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Departments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return Result.Fail("Department not found.");
        if (await db.Employees.AnyAsync(e => e.DepartmentId == id, ct)) return Result.Fail("Employees belong to this department. Move them or mark it inactive.");
        if (await db.Departments.AnyAsync(x => x.ParentDepartmentId == id, ct)) return Result.Fail("This department has sub-departments.");
        db.Departments.Remove(d);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteDesignationAsync(int id, CancellationToken ct = default)
    {
        var d = await db.Designations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return Result.Fail("Designation not found.");
        if (await db.Employees.AnyAsync(e => e.DesignationId == id, ct)) return Result.Fail("Employees hold this designation. Change them or mark it inactive.");
        db.Designations.Remove(d);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<HrSettingsInput> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await GetOrCreateSettingsAsync(ct);
        return new HrSettingsInput
        {
            EmployeeIdPrefix = s.EmployeeIdPrefix, EmployeeIdPadding = s.EmployeeIdPadding, NextEmployeeNumber = s.NextEmployeeNumber,
            DefaultNoticePeriodDays = s.DefaultNoticePeriodDays, DefaultProbationMonths = s.DefaultProbationMonths,
            OnTimePoints = s.OnTimePoints, LatePoints = s.LatePoints
        };
    }

    public async Task<Result> SaveSettingsAsync(HrSettingsInput input, CancellationToken ct = default)
    {
        var s = await GetOrCreateSettingsAsync(ct);
        var prefix = input.EmployeeIdPrefix.Trim().ToUpperInvariant();
        var candidate = new OrganizationSetting { EmployeeIdPrefix = prefix, EmployeeIdPadding = input.EmployeeIdPadding }.FormatEmployeeCode(input.NextEmployeeNumber);
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.OrganizationId == OrgId && e.EmployeeCode == candidate, ct))
            return Result.Fail($"Employee ID {candidate} is already in use. Choose a higher next number or a different prefix.");
        s.EmployeeIdPrefix = prefix;
        s.EmployeeIdPadding = input.EmployeeIdPadding;
        s.NextEmployeeNumber = input.NextEmployeeNumber;
        s.DefaultNoticePeriodDays = input.DefaultNoticePeriodDays;
        s.DefaultProbationMonths = input.DefaultProbationMonths;
        s.OnTimePoints = input.OnTimePoints;
        s.LatePoints = input.LatePoints;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<OrganizationSetting> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var s = await db.OrganizationSettings.FirstOrDefaultAsync(ct);
        if (s is not null) return s;
        s = new OrganizationSetting();
        db.OrganizationSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<IReadOnlyList<ExitChecklistTemplateItem>> ChecklistTemplateAsync(CancellationToken ct = default) =>
        await db.ExitChecklistTemplateItems.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync(ct);

    public async Task<Result> AddChecklistTemplateItemAsync(string title, string owner, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200) return Result.Fail("Enter a checklist item (max 200 characters).");
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 50) return Result.Fail("Enter who owns this item (max 50 characters).");
        var max = await db.ExitChecklistTemplateItems.Select(i => (int?)i.SortOrder).MaxAsync(ct) ?? 0;
        db.ExitChecklistTemplateItems.Add(new ExitChecklistTemplateItem { Title = title.Trim(), Owner = owner.Trim(), SortOrder = max + 10 });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteChecklistTemplateItemAsync(int id, CancellationToken ct = default)
    {
        var item = await db.ExitChecklistTemplateItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return Result.Fail("Item not found.");
        db.ExitChecklistTemplateItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<(IReadOnlyList<LookupItem>, IReadOnlyList<LookupItem>, IReadOnlyList<LookupItem>)> LookupsAsync(CancellationToken ct = default)
    {
        // One round trip (UNION ALL) instead of three: these dropdowns are on many pages.
        var rows = await db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => new { Kind = 1, b.Id, b.Name })
            .Concat(db.Departments.AsNoTracking().Where(d => d.IsActive).Select(d => new { Kind = 2, d.Id, d.Name }))
            .Concat(db.Designations.AsNoTracking().Where(d => d.IsActive).Select(d => new { Kind = 3, d.Id, d.Name }))
            .ToListAsync(ct);
        IReadOnlyList<LookupItem> Of(int kind) => rows.Where(r => r.Kind == kind).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).Select(r => new LookupItem(r.Id, r.Name)).ToList();
        return (Of(1), Of(2), Of(3));
    }
}
