namespace Lotv.Core.ValueObjects;

public record GeoLocation(double Latitude, double Longitude)
{
    /// <summary>
    /// Returns the great-circle distance in miles between two points using the Haversine formula.
    /// </summary>
    public double DistanceMilesTo(GeoLocation other)
    {
        const double R = 3958.8; // Earth radius in miles
        var dLat = ToRad(other.Latitude - Latitude);
        var dLon = ToRad(other.Longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(Latitude)) * Math.Cos(ToRad(other.Latitude))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
