using Codenexify.HRMS.Domain.Strength;

namespace Codenexify.HRMS.Application.Time;

public sealed record GeoPoint(decimal Latitude, decimal Longitude, int? AccuracyMeters);
public sealed record GeoOffice(int Id, string Name, decimal Latitude, decimal Longitude, int RadiusMeters);

public sealed record GeoCheck(PunchLocationStatus Status, GeoPoint? Point, int? OfficeId, int? DistanceMeters, string? Error)
{
    public static readonly GeoCheck None = new(PunchLocationStatus.NotCaptured, null, null, null, null);
}

/// <summary>Pure geo-fence rules for clock-in/out (unit tested).</summary>
public static class GeoFence
{
    /// <summary>Phone/browser fixes worse than this are too vague to prove presence at an office.</summary>
    public const int MaxUsableAccuracyMeters = 500;
    /// <summary>GPS error allowance added to an office radius (capped so a vague fix can't stretch the fence).</summary>
    public const int MaxAccuracyAllowance = 100;

    /// <summary>Great-circle distance in metres (haversine).</summary>
    public static double DistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double r = 6371000;
        double Rad(decimal d) => (double)d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static bool IsValid(GeoPoint p) => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180 && !(p.Latitude == 0 && p.Longitude == 0);

    public static GeoCheck Evaluate(GeoPoint? point, IReadOnlyList<GeoOffice> offices, bool enforce, bool exempt)
    {
        if (point is not null && !IsValid(point)) point = null;
        if (point is null)
            return enforce
                ? GeoCheck.None with { Error = "Your location is needed to clock in. Allow location access in your browser or app and try again." }
                : GeoCheck.None;

        var nearest = offices.Select(o => (Office: o, Distance: DistanceMeters(point.Latitude, point.Longitude, o.Latitude, o.Longitude)))
            .OrderBy(x => x.Distance).Cast<(GeoOffice Office, double Distance)?>().FirstOrDefault();
        var distance = nearest is { } n ? (int)Math.Round(n.Distance) : (int?)null;
        var allowance = Math.Min(point.AccuracyMeters ?? 0, MaxAccuracyAllowance);
        var inside = nearest is { } m && m.Distance - allowance <= m.Office.RadiusMeters;
        var status = exempt ? PunchLocationStatus.Exempt : nearest is null ? PunchLocationStatus.Captured : inside ? PunchLocationStatus.Inside : PunchLocationStatus.Outside;
        var result = new GeoCheck(status, point, nearest?.Office.Id, distance, null);

        if (!enforce) return result;
        if (offices.Count == 0) return result with { Error = "No office location is set up for geo-fenced attendance. Ask HR." };
        if (point.AccuracyMeters is > MaxUsableAccuracyMeters)
            return result with { Error = $"Your location is too imprecise (±{point.AccuracyMeters} m). Turn on precise location / GPS and try again." };
        if (!inside)
            return result with { Error = $"You are about {FormatDistance(distance!.Value)} from {nearest!.Value.Office.Name}. Clock in from the office, or ask HR if you work on site." };
        return result;
    }

    private static string FormatDistance(int m) => m >= 1000 ? $"{m / 1000.0:0.#} km" : $"{m} m";
}
