using Lotv.Core.ValueObjects;

namespace Lotv.Tests.ValueObjects;

public class GeoLocationTests
{
    // ── Same point → 0 miles ──────────────────────────────────────────────────

    [Fact]
    public void DistanceMilesTo_SamePoint_ReturnsZero()
    {
        var loc = new GeoLocation(40.7128, -74.0060);
        Assert.Equal(0.0, loc.DistanceMilesTo(loc), precision: 5);
    }

    // ── Known distances (verified against external references) ────────────────

    [Fact]
    public void DistanceMilesTo_NewYorkToLosAngeles_ApproxCorrect()
    {
        // NYC (40.7128, -74.0060) → LA (34.0522, -118.2437) ≈ 2,451 miles
        var nyc = new GeoLocation(40.7128, -74.0060);
        var la  = new GeoLocation(34.0522, -118.2437);

        var miles = nyc.DistanceMilesTo(la);

        Assert.InRange(miles, 2_440, 2_465);
    }

    [Fact]
    public void DistanceMilesTo_NewYorkToChicago_ApproxCorrect()
    {
        // NYC → Chicago ≈ 714 miles
        var nyc     = new GeoLocation(40.7128, -74.0060);
        var chicago = new GeoLocation(41.8781, -87.6298);

        var miles = nyc.DistanceMilesTo(chicago);

        Assert.InRange(miles, 705, 725);
    }

    // ── Symmetry: A→B == B→A ─────────────────────────────────────────────────

    [Fact]
    public void DistanceMilesTo_IsSymmetric()
    {
        var a = new GeoLocation(38.8951, -77.0364); // Washington DC
        var b = new GeoLocation(42.3601, -71.0589); // Boston

        Assert.Equal(a.DistanceMilesTo(b), b.DistanceMilesTo(a), precision: 8);
    }

    // ── Short distances (< 50 miles) ──────────────────────────────────────────

    [Fact]
    public void DistanceMilesTo_ShortDistance_IsPositiveAndReasonable()
    {
        var a = new GeoLocation(40.7128, -74.0060); // NYC
        var b = new GeoLocation(40.6892, -74.0445); // Statue of Liberty area

        var miles = a.DistanceMilesTo(b);

        Assert.True(miles > 0, "distance must be positive");
        Assert.True(miles < 5, "should be under 5 miles");
    }

    // ── Hemisphere crossing: negative latitudes ───────────────────────────────

    [Fact]
    public void DistanceMilesTo_CrossEquator_ReturnsPositive()
    {
        var north = new GeoLocation(10.0, 0.0);
        var south = new GeoLocation(-10.0, 0.0);

        var miles = north.DistanceMilesTo(south);

        Assert.True(miles > 0);
    }

    // ── Record equality (value object semantics) ──────────────────────────────

    [Fact]
    public void GeoLocation_EqualityByValue()
    {
        var a = new GeoLocation(40.7128, -74.0060);
        var b = new GeoLocation(40.7128, -74.0060);

        Assert.Equal(a, b);
    }

    [Fact]
    public void GeoLocation_InequalityOnLatitudeDifference()
    {
        var a = new GeoLocation(40.7128, -74.0060);
        var b = new GeoLocation(40.7129, -74.0060);

        Assert.NotEqual(a, b);
    }
}
