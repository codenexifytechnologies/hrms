using System.ComponentModel.DataAnnotations;
using Codenexify.HRMS.Application.Abstractions;
using Codenexify.HRMS.Application.Common;
using Codenexify.HRMS.Application.CoreHr;
using Codenexify.HRMS.Domain.CoreHr;
using Codenexify.HRMS.Domain.Performance;
using Microsoft.EntityFrameworkCore;

namespace Codenexify.HRMS.Application.Performance;

public sealed class SkillInput
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(60)] public string Category { get; set; } = "General";
    [StringLength(500)] public string? Description { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}

public sealed record SkillRow(int Id, string Name, string Category, string? Description, bool IsActive, int People);

/// <summary>One line of a skill profile. Effective level = the manager's assessment when there is one, else the self-rating.</summary>
public sealed record SkillLine(int SkillId, string Skill, string Category, int? SelfLevel, int? ManagerLevel, string? ManagerNote, string? AssessedBy, int? Required)
{
    public int? Effective => ManagerLevel ?? SelfLevel;
    public int Gap => Required is int r ? Math.Max(0, r - (Effective ?? 0)) : 0;
}

public sealed record SkillProfile(int EmployeeId, string EmployeeName, string? Designation, IReadOnlyList<SkillLine> Lines, bool CanAssess, bool IsSelf);
public sealed record SkillMatch(int EmployeeId, string EmployeeName, string EmployeeCode, string? Department, string? Designation, int Level, bool Assessed);
public sealed record RequirementRow(int SkillId, string Skill, string Category, int Level);

public interface ISkillService
{
    Task<IReadOnlyList<SkillRow>> CatalogAsync(bool includeInactive, CancellationToken ct = default);
    Task<SkillInput?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(int? id, SkillInput input, CancellationToken ct = default);
    Task<IReadOnlyList<RequirementRow>> RequirementsAsync(int designationId, CancellationToken ct = default);
    /// <param name="level">1–5; 0 removes the requirement.</param>
    Task<Result> SetRequirementAsync(int designationId, int skillId, int level, CancellationToken ct = default);
    /// <summary>Null unless the viewer is the employee, a manager above them, or HR.</summary>
    Task<SkillProfile?> ProfileAsync(int employeeId, int? viewer, bool isHr, CancellationToken ct = default);
    /// <param name="level">1–5; 0 removes the self-rating.</param>
    Task<Result> SetSelfLevelAsync(int me, int skillId, int level, CancellationToken ct = default);
    /// <param name="level">1–5; 0 clears the assessment.</param>
    Task<Result> AssessAsync(int? me, bool isHr, int employeeId, int skillId, int level, string? note, CancellationToken ct = default);
    /// <summary>Current employees with at least the given effective level in a skill, highest first.</summary>
    Task<IReadOnlyList<SkillMatch>> FindAsync(int skillId, int minLevel, int? departmentId, CancellationToken ct = default);
    Task<IReadOnlyList<LookupItem>> TeamAsync(int me, CancellationToken ct = default);
    Task<byte[]> ExportMatrixAsync(int? departmentId, CancellationToken ct = default);
}

public sealed class SkillService(IAppDbContext db, ICurrentContext context, IClock clock, IExcelExporter excel) : ISkillService
{
    public static readonly IReadOnlyList<string> Levels = ["Beginner", "Elementary", "Intermediate", "Advanced", "Expert"];
    public static string LevelName(int? level) => level is >= 1 and <= 5 ? Levels[level.Value - 1] : "—";

    public async Task<IReadOnlyList<SkillRow>> CatalogAsync(bool includeInactive, CancellationToken ct = default)
    {
        var people = await db.EmployeeSkills.AsNoTracking().Where(s => s.SelfLevel != null || s.ManagerLevel != null)
            .GroupBy(s => s.SkillId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N, ct);
        return (await db.Skills.AsNoTracking().Where(s => includeInactive || s.IsActive).OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct))
            .Select(s => new SkillRow(s.Id, s.Name, s.Category, s.Description, s.IsActive, people.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SkillInput?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new SkillInput { Name = s.Name, Category = s.Category, Description = s.Description, IsActive = s.IsActive };
    }

    public async Task<Result<int>> SaveAsync(int? id, SkillInput i, CancellationToken ct = default)
    {
        var name = i.Name.Trim();
        if (await db.Skills.AnyAsync(s => s.Name == name && s.Id != (id ?? 0), ct)) return Result<int>.Fail("This skill is already in the catalog.");
        if (id is null && await db.Skills.CountAsync(ct) >= 500) return Result<int>.Fail("The catalog can hold up to 500 skills.");
        Skill s;
        if (id is int existing) s = await db.Skills.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new InvalidOperationException();
        else { s = new Skill(); db.Skills.Add(s); }
        s.Name = name;
        s.Category = i.Category.Trim();
        s.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
        s.IsActive = i.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(s.Id);
    }

    public async Task<IReadOnlyList<RequirementRow>> RequirementsAsync(int designationId, CancellationToken ct = default) =>
        await (from r in db.SkillRequirements.AsNoTracking().Where(r => r.DesignationId == designationId)
               join s in db.Skills on r.SkillId equals s.Id
               orderby s.Category, s.Name
               select new RequirementRow(s.Id, s.Name, s.Category, r.RequiredLevel)).ToListAsync(ct);

    public async Task<Result> SetRequirementAsync(int designationId, int skillId, int level, CancellationToken ct = default)
    {
        if (level is < 0 or > 5) return Result.Fail("Choose a level from 1 to 5.");
        if (!await db.Designations.AnyAsync(d => d.Id == designationId, ct)) return Result.Fail("Job title not found.");
        if (!await db.Skills.AnyAsync(s => s.Id == skillId && s.IsActive, ct)) return Result.Fail("Skill not found.");
        var row = await db.SkillRequirements.FirstOrDefaultAsync(r => r.DesignationId == designationId && r.SkillId == skillId, ct);
        if (level == 0) { if (row is not null) db.SkillRequirements.Remove(row); }
        else if (row is null) db.SkillRequirements.Add(new SkillRequirement { DesignationId = designationId, SkillId = skillId, RequiredLevel = level });
        else row.RequiredLevel = level;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<bool> CanAssessAsync(int? viewer, bool isHr, int employeeId, CancellationToken ct) =>
        isHr || (viewer is int v && v != employeeId && (await ReportingLine.LoadAsync(db, ct)).IsAbove(v, employeeId));

    public async Task<SkillProfile?> ProfileAsync(int employeeId, int? viewer, bool isHr, CancellationToken ct = default)
    {
        var emp = await db.Employees.AsNoTracking().Include(e => e.Designation).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null) return null;
        var isSelf = viewer == employeeId;
        var canAssess = await CanAssessAsync(viewer, isHr, employeeId, ct);
        if (!isSelf && !canAssess) return null;

        var have = await (from es in db.EmployeeSkills.AsNoTracking().Where(x => x.EmployeeId == employeeId && (x.SelfLevel != null || x.ManagerLevel != null))
                          join s in db.Skills on es.SkillId equals s.Id
                          select new { s.Id, s.Name, s.Category, es.SelfLevel, es.ManagerLevel, es.ManagerNote, es.AssessedByName }).ToListAsync(ct);
        var required = emp.DesignationId is int d
            ? (await RequirementsAsync(d, ct)).ToDictionary(r => r.SkillId)
            : new Dictionary<int, RequirementRow>();
        var lines = have.Select(h => new SkillLine(h.Id, h.Name, h.Category, h.SelfLevel, h.ManagerLevel, h.ManagerNote, h.AssessedByName, required.TryGetValue(h.Id, out var r) ? r.Level : null)).ToList();
        lines.AddRange(required.Values.Where(r => have.All(h => h.Id != r.SkillId)).Select(r => new SkillLine(r.SkillId, r.Skill, r.Category, null, null, null, null, r.Level)));
        return new SkillProfile(emp.Id, emp.FullName, emp.Designation?.Name,
            lines.OrderByDescending(l => l.Gap).ThenBy(l => l.Category).ThenBy(l => l.Skill).ToList(), canAssess, isSelf);
    }

    public async Task<Result> SetSelfLevelAsync(int me, int skillId, int level, CancellationToken ct = default)
    {
        if (level is < 0 or > 5) return Result.Fail("Choose a level from 1 to 5.");
        if (!await db.Skills.AnyAsync(s => s.Id == skillId && s.IsActive, ct)) return Result.Fail("Choose a skill from the catalog.");
        var row = await db.EmployeeSkills.FirstOrDefaultAsync(s => s.EmployeeId == me && s.SkillId == skillId, ct);
        if (row is null)
        {
            if (level == 0) return Result.Ok();
            if (await db.EmployeeSkills.CountAsync(s => s.EmployeeId == me, ct) >= 100) return Result.Fail("You can list up to 100 skills.");
            db.EmployeeSkills.Add(row = new EmployeeSkill { EmployeeId = me, SkillId = skillId });
        }
        row.SelfLevel = level == 0 ? null : level;
        row.SelfUpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AssessAsync(int? me, bool isHr, int employeeId, int skillId, int level, string? note, CancellationToken ct = default)
    {
        if (level is < 0 or > 5) return Result.Fail("Choose a level from 1 to 5.");
        if (!await CanAssessAsync(me, isHr, employeeId, ct)) return Result.Fail("Only managers in the reporting line (or HR) can assess skills.");
        if (!await db.Skills.AnyAsync(s => s.Id == skillId, ct)) return Result.Fail("Skill not found.");
        var row = await db.EmployeeSkills.FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.SkillId == skillId, ct);
        if (row is null)
        {
            if (level == 0) return Result.Ok();
            db.EmployeeSkills.Add(row = new EmployeeSkill { EmployeeId = employeeId, SkillId = skillId });
        }
        row.ManagerLevel = level == 0 ? null : level;
        row.ManagerNote = level == 0 || string.IsNullOrWhiteSpace(note) ? null : note.Trim() is { Length: > 500 } n ? n[..500] : note.Trim();
        row.AssessedByName = level == 0 ? null : await db.Users.Where(u => u.Id == context.UserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "HR";
        row.AssessedAtUtc = level == 0 ? null : clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<SkillMatch>> FindAsync(int skillId, int minLevel, int? departmentId, CancellationToken ct = default)
    {
        var rows = await (from es in db.EmployeeSkills.AsNoTracking().Where(s => s.SkillId == skillId)
                          join e in db.Employees.Include(x => x.Department).Include(x => x.Designation) on es.EmployeeId equals e.Id
                          where e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && (departmentId == null || e.DepartmentId == departmentId)
                          select new { e.Id, e.FirstName, e.LastName, e.EmployeeCode, Dept = e.Department == null ? null : e.Department.Name, Desig = e.Designation == null ? null : e.Designation.Name, es.SelfLevel, es.ManagerLevel })
            .ToListAsync(ct);
        return rows.Select(r => new SkillMatch(r.Id, r.FirstName + " " + r.LastName, r.EmployeeCode, r.Dept, r.Desig, r.ManagerLevel ?? r.SelfLevel ?? 0, r.ManagerLevel is not null))
            .Where(m => m.Level >= Math.Max(1, minLevel)).OrderByDescending(m => m.Level).ThenByDescending(m => m.Assessed).ThenBy(m => m.EmployeeName).ToList();
    }

    public async Task<IReadOnlyList<LookupItem>> TeamAsync(int me, CancellationToken ct = default)
    {
        var team = (await ReportingLine.LoadAsync(db, ct)).ReportsOf(me).ToList();
        return team.Count == 0 ? [] : await db.Employees.AsNoTracking()
            .Where(e => team.Contains(e.Id) && e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive)
            .OrderBy(e => e.FirstName).Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName)).ToListAsync(ct);
    }

    public async Task<byte[]> ExportMatrixAsync(int? departmentId, CancellationToken ct = default)
    {
        var skills = await db.Skills.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);
        var people = await db.Employees.AsNoTracking().Include(e => e.Department).Include(e => e.Designation)
            .Where(e => e.Status != EmployeeStatus.Exited && e.Status != EmployeeStatus.Inactive && (departmentId == null || e.DepartmentId == departmentId))
            .OrderBy(e => e.FirstName).ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();
        var levels = await db.EmployeeSkills.AsNoTracking().Where(s => ids.Contains(s.EmployeeId))
            .Select(s => new { s.EmployeeId, s.SkillId, Level = s.ManagerLevel ?? s.SelfLevel }).ToListAsync(ct);
        var map = levels.Where(l => l.Level != null).ToDictionary(l => (l.EmployeeId, l.SkillId), l => l.Level!.Value);
        var headers = new List<string> { "Employee code", "Employee", "Department", "Job title" };
        headers.AddRange(skills.Select(s => s.Name));
        return excel.Export("Skills", headers, people.Select(p =>
        {
            var row = new List<object?> { p.EmployeeCode, p.FullName, p.Department?.Name, p.Designation?.Name };
            row.AddRange(skills.Select(s => map.TryGetValue((p.Id, s.Id), out var l) ? (object)l : null));
            return (IReadOnlyList<object?>)row;
        }));
    }
}
