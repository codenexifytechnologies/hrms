using Codenexify.HRMS.Domain.Performance;

namespace Codenexify.HRMS.Web;

/// <summary>One question in a review form.</summary>
public sealed record ReviewQuestionModel(ReviewQuestion Question, ReviewAnswer? Answer, int Index, IReadOnlyList<string> Labels);

/// <summary>Display text and colours for goals and reviews.</summary>
public static class GoalUi
{
    public static string Label(GoalStatus s) => s switch
    {
        GoalStatus.NotStarted => "Not started",
        GoalStatus.OnTrack => "On track",
        GoalStatus.AtRisk => "At risk",
        GoalStatus.OffTrack => "Off track",
        GoalStatus.Completed => "Completed",
        _ => "Cancelled"
    };

    public static string StatusCss(GoalStatus s) => s switch
    {
        GoalStatus.OnTrack => "text-bg-success",
        GoalStatus.AtRisk => "text-bg-warning",
        GoalStatus.OffTrack => "text-bg-danger",
        GoalStatus.Completed => "text-bg-primary",
        _ => "text-bg-secondary"
    };

    public static string BarCss(GoalStatus s) => s switch
    {
        GoalStatus.AtRisk => "bg-warning",
        GoalStatus.OffTrack => "bg-danger",
        GoalStatus.Completed => "bg-primary",
        GoalStatus.Cancelled => "bg-secondary",
        _ => "bg-success"
    };

    public static string Label(KeyResultType t) => t switch
    {
        KeyResultType.Number => "Number",
        KeyResultType.Percentage => "Percentage",
        KeyResultType.Currency => "Amount (₹)",
        _ => "Milestone (done / not done)"
    };

    public static string Value(KeyResult k, decimal v) => k.Type switch
    {
        KeyResultType.Milestone => v >= 1 ? "Done" : "Not done",
        KeyResultType.Currency => $"₹{v:N0}",
        KeyResultType.Percentage => $"{v:0.##}%",
        _ => $"{v:0.##}{(k.Unit is null ? "" : " " + k.Unit)}"
    };

    public static string Label(ReviewStatus s) => s switch
    {
        ReviewStatus.SelfReview => "Self-review",
        ReviewStatus.ManagerReview => "Manager review",
        ReviewStatus.Submitted => "Submitted",
        ReviewStatus.Released => "Shared",
        _ => "Acknowledged"
    };

    public static string Css(ReviewStatus s) => s switch
    {
        ReviewStatus.SelfReview => "text-bg-secondary",
        ReviewStatus.ManagerReview => "text-bg-warning",
        ReviewStatus.Submitted => "text-bg-info",
        ReviewStatus.Released => "text-bg-primary",
        _ => "text-bg-success"
    };

    public static string Css(PromotionStatus s) => s switch
    {
        PromotionStatus.Proposed => "text-bg-warning",
        PromotionStatus.Approved => "text-bg-info",
        PromotionStatus.Applied => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string Css(PipStatus s) => s switch
    {
        PipStatus.Draft => "text-bg-secondary",
        PipStatus.Active => "text-bg-warning",
        PipStatus.Passed => "text-bg-success",
        PipStatus.NotPassed => "text-bg-danger",
        _ => "text-bg-light border"
    };

    public static string Label(PipStatus s) => s switch { PipStatus.NotPassed => "Not passed", _ => s.ToString() };

    public static string Label(PipProgress p) => p switch { PipProgress.OnTrack => "On track", PipProgress.SomeConcerns => "Some concerns", _ => "Off track" };

    public static string Css(ReviewCycleStatus s) => s switch
    {
        ReviewCycleStatus.Draft => "text-bg-secondary",
        ReviewCycleStatus.Active => "text-bg-warning",
        ReviewCycleStatus.Released => "text-bg-primary",
        _ => "text-bg-success"
    };
}
