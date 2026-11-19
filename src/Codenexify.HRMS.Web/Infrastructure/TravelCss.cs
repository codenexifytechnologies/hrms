using Codenexify.HRMS.Domain.Strength;

namespace Codenexify.HRMS.Web;

/// <summary>Status badge colour for travel requests (shared by HR and self-service views).</summary>
public static class TravelCss
{
    public static string Of(TravelStatus s) => s switch
    {
        TravelStatus.Submitted => "PendingVerification",
        TravelStatus.Approved => "Issued",
        TravelStatus.Booked => "Active",
        TravelStatus.Completed => "Paid",
        TravelStatus.Rejected => "Expired",
        _ => "Inactive"
    };
}
