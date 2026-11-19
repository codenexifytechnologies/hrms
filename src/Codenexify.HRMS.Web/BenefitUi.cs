using Codenexify.HRMS.Domain.Benefits;

namespace Codenexify.HRMS.Web;

/// <summary>Model for the shared family-members form (HR and self-service).</summary>
public sealed record DependentsFormModel(string Action, int Max, IReadOnlyList<BenefitDependent> Current, int? PlanId = null);

/// <summary>Display names and icons for benefit categories and relations.</summary>
public static class BenefitUi
{
    public static string Label(BenefitCategory c) => c switch
    {
        BenefitCategory.HealthInsurance => "Health insurance",
        BenefitCategory.LifeInsurance => "Life insurance",
        BenefitCategory.AccidentInsurance => "Accident insurance",
        BenefitCategory.Allowance => "Allowance",
        BenefitCategory.Meal => "Meal",
        BenefitCategory.Transport => "Transport",
        BenefitCategory.Wellness => "Wellness",
        BenefitCategory.Education => "Learning & education",
        _ => "Other"
    };

    public static string Icon(BenefitCategory c) => c switch
    {
        BenefitCategory.HealthInsurance => "bi-heart-pulse",
        BenefitCategory.LifeInsurance => "bi-shield-check",
        BenefitCategory.AccidentInsurance => "bi-bandaid",
        BenefitCategory.Allowance => "bi-cash-coin",
        BenefitCategory.Meal => "bi-cup-hot",
        BenefitCategory.Transport => "bi-bus-front",
        BenefitCategory.Wellness => "bi-activity",
        BenefitCategory.Education => "bi-mortarboard",
        _ => "bi-gift"
    };

    public static string Label(DependentRelation r) => r switch
    {
        DependentRelation.FatherInLaw => "Father-in-law",
        DependentRelation.MotherInLaw => "Mother-in-law",
        _ => r.ToString()
    };

    public static string Money(decimal v) => v == 0 ? "—" : $"₹{v:N0}";

    public static string Label(Domain.CoreHr.EmploymentType t) => t switch
    {
        Domain.CoreHr.EmploymentType.FullTime => "Full-time",
        Domain.CoreHr.EmploymentType.PartTime => "Part-time",
        _ => t.ToString()
    };
}
