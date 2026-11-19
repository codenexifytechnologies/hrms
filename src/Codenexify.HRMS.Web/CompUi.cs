using Codenexify.HRMS.Domain.Performance;

namespace Codenexify.HRMS.Web;

public static class CompUi
{
    public static string Css(CompensationCycleStatus s) => s switch
    {
        CompensationCycleStatus.Draft => "text-bg-secondary",
        CompensationCycleStatus.Planning => "text-bg-warning",
        CompensationCycleStatus.Review => "text-bg-info",
        CompensationCycleStatus.Approved => "text-bg-primary",
        _ => "text-bg-success"
    };

    public static string Rating(int r) => r == 0 ? "No rating" : $"Rating {r}";
}
