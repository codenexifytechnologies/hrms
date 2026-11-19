using Codenexify.HRMS.Domain.Common;
using Codenexify.HRMS.Domain.CoreHr;

namespace Codenexify.HRMS.Domain.Performance;

// ====================================================================== workflow automation

/// <summary>What starts a rule. Date triggers use <see cref="WorkflowRule.DaysOffset"/>.</summary>
public enum WorkflowTrigger
{
    /// <summary>An employee record is added (after the rule was created).</summary>
    EmployeeAdded,
    /// <summary>N days before the joining date (0 = on the day).</summary>
    BeforeJoining,
    /// <summary>N days before probation ends.</summary>
    BeforeProbationEnds,
    /// <summary>N days before the last working day of a leaver.</summary>
    BeforeLastDay,
    /// <summary>A promotion took effect.</summary>
    PromotionApplied,
}

public enum WorkflowActionType { Notify, CreateTask, StartOnboarding, EnrolBenefit }

/// <summary>Who an action is for.</summary>
public enum WorkflowRecipient { Employee, Manager, HrTeam, SpecificUser }

public class WorkflowRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public WorkflowTrigger Trigger { get; set; }
    public int DaysOffset { get; set; }
    // Conditions (empty = any)
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAtUtc { get; set; }
    public ICollection<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();
}

public class WorkflowAction : TenantEntity
{
    public int WorkflowRuleId { get; set; }
    public WorkflowActionType Type { get; set; }
    public WorkflowRecipient Recipient { get; set; }
    /// <summary>For <see cref="WorkflowRecipient.SpecificUser"/>.</summary>
    public int? UserId { get; set; }
    /// <summary>Notification/task title; may contain {Employee}, {Date}, {Department}.</summary>
    public string? Title { get; set; }
    public string? Message { get; set; }
    /// <summary>Tasks: due this many days after the rule fires.</summary>
    public int DueInDays { get; set; }
    /// <summary>Onboarding template or benefit plan id.</summary>
    public int? TargetId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One firing of a rule for one subject (never repeated).</summary>
public class WorkflowRun : TenantEntity
{
    public int WorkflowRuleId { get; set; }
    public string SubjectKey { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public DateTime RanAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string? Detail { get; set; }
}

/// <summary>A to-do created by a workflow (or by HR) for a user.</summary>
public class WorkTask : TenantEntity
{
    public int AssigneeUserId { get; set; }
    /// <summary>The employee the task is about.</summary>
    public int? EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateOnly? DueOn { get; set; }
    public bool IsDone { get; set; }
    public DateTime? DoneAtUtc { get; set; }
    public int? WorkflowRuleId { get; set; }
}

// ====================================================================== custom report builder

public enum ReportSource { Employees, Leave, Attendance, Payroll }

/// <summary>A saved report definition: source, columns, filters, grouping. Definitions are JSON validated against a whitelist.</summary>
public class SavedReport : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ReportSource Source { get; set; }
    /// <summary>JSON <c>ReportDefinition</c>.</summary>
    public string Definition { get; set; } = "{}";
    public int OwnerUserId { get; set; }
    /// <summary>Everyone with the report-builder permission can run it.</summary>
    public bool IsShared { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}
