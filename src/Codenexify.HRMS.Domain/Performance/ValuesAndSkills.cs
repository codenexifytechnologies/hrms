using Codenexify.HRMS.Domain.Common;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== core values

/// <summary>A company value employees recognise each other for (e.g. "Customer first").</summary>
public class CoreValue : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Bootstrap icon name from a fixed list (e.g. "heart").</summary>
    public string Icon { get; set; } = "star";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A public thank-you that ties someone's action to a core value. Visible to everyone in the organization.</summary>
public class ValueShoutout : TenantEntity
{
    public int CoreValueId { get; set; }
    public int FromEmployeeId { get; set; }
    public int ToEmployeeId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}

// ====================================================================== skills

/// <summary>An entry in the organization's skill catalog.</summary>
public class Skill : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>An employee's level in a skill (1 Beginner … 5 Expert): their own rating and their manager's assessment.</summary>
public class EmployeeSkill : TenantEntity
{
    public int EmployeeId { get; set; }
    public int SkillId { get; set; }
    public int? SelfLevel { get; set; }
    public int? ManagerLevel { get; set; }
    public string? ManagerNote { get; set; }
    public string? AssessedByName { get; set; }
    public DateTime? SelfUpdatedAtUtc { get; set; }
    public DateTime? AssessedAtUtc { get; set; }
}

/// <summary>The level a designation (job title) needs in a skill; used for gap analysis.</summary>
public class SkillRequirement : TenantEntity
{
    public int DesignationId { get; set; }
    public int SkillId { get; set; }
    public int RequiredLevel { get; set; }
}
