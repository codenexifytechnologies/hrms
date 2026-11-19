using Codenexify.HRMS.Application.Planning;
using Codenexify.HRMS.Domain.Planning;

namespace Codenexify.HRMS.Web;

/// <summary>Display helpers for headcount planning and pre-boarding.</summary>
public static class PlanningUi
{
    public static string Css(HeadcountPlanStatus s) => s switch
    {
        HeadcountPlanStatus.Draft => "text-bg-secondary",
        HeadcountPlanStatus.Approved => "text-bg-success",
        _ => "text-bg-dark"
    };

    public static string Label(HeadcountPositionKind k) => k == HeadcountPositionKind.NewRole ? "New role" : "Replacement";

    public static string Label(HeadcountPositionStatus s) => s == HeadcountPositionStatus.OnHold ? "On hold" : s.ToString();

    public static string Css(HeadcountPositionStatus s) => s switch
    {
        HeadcountPositionStatus.Open => "text-bg-primary",
        HeadcountPositionStatus.OnHold => "text-bg-warning",
        _ => "text-bg-secondary"
    };

    public static string Css(HeadcountPriority p) => p switch
    {
        HeadcountPriority.High => "text-danger fw-semibold",
        HeadcountPriority.Low => "text-muted",
        _ => ""
    };

    public static string Label(PreboardingState s) => s switch
    {
        PreboardingState.NotInvited => "Not invited",
        PreboardingState.Invited => "Link sent",
        PreboardingState.Opened => "In progress",
        PreboardingState.Submitted => "Submitted",
        PreboardingState.Expired => "Link expired",
        PreboardingState.Revoked => "Link cancelled",
        _ => s.ToString()
    };

    public static string Css(PreboardingState s) => s switch
    {
        PreboardingState.Submitted => "text-bg-success",
        PreboardingState.Opened => "text-bg-primary",
        PreboardingState.Invited => "text-bg-info",
        PreboardingState.NotInvited => "text-bg-light border",
        _ => "text-bg-secondary"
    };

    public static string Label(PreboardingItemKind k) => k == PreboardingItemKind.Document ? "Upload a document" : "Read and accept";
}
