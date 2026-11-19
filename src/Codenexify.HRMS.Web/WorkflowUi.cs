using Codenexify.HRMS.Domain.Performance;

namespace Codenexify.HRMS.Web;

/// <summary>Display labels for automation rules and the report builder.</summary>
public static class WorkflowUi
{
    public static string Label(WorkflowTrigger t) => t switch
    {
        WorkflowTrigger.EmployeeAdded => "An employee is added",
        WorkflowTrigger.BeforeJoining => "Days before someone joins",
        WorkflowTrigger.BeforeProbationEnds => "Days before probation ends",
        WorkflowTrigger.BeforeLastDay => "Days before someone's last day",
        WorkflowTrigger.PromotionApplied => "A promotion takes effect",
        _ => t.ToString()
    };

    public static string Describe(WorkflowTrigger t, int days) => t switch
    {
        WorkflowTrigger.BeforeJoining => days == 0 ? "On the joining date" : $"{days} day(s) before joining",
        WorkflowTrigger.BeforeProbationEnds => days == 0 ? "On the probation end date" : $"{days} day(s) before probation ends",
        WorkflowTrigger.BeforeLastDay => days == 0 ? "On the last working day" : $"{days} day(s) before the last day",
        _ => Label(t)
    };

    public static string Label(WorkflowActionType t) => t switch
    {
        WorkflowActionType.Notify => "Send a notification",
        WorkflowActionType.CreateTask => "Create a task",
        WorkflowActionType.StartOnboarding => "Start onboarding",
        WorkflowActionType.EnrolBenefit => "Enrol in a benefit plan",
        _ => t.ToString()
    };

    public static string Label(WorkflowRecipient r) => r switch
    {
        WorkflowRecipient.Employee => "The employee",
        WorkflowRecipient.Manager => "Their manager",
        WorkflowRecipient.HrTeam => "The HR team",
        WorkflowRecipient.SpecificUser => "A specific person",
        _ => r.ToString()
    };

    public static string Label(ReportSource s) => s switch
    {
        ReportSource.Employees => "Employees",
        ReportSource.Leave => "Leave requests",
        ReportSource.Attendance => "Attendance (per month)",
        ReportSource.Payroll => "Payroll (per payslip)",
        _ => s.ToString()
    };
}
