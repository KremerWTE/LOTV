using Lotv.Core.ValueObjects;

namespace Lotv.Tests.ValueObjects;

public class AddressTests
{
    // ── OneLine without Apt ───────────────────────────────────────────────────

    [Fact]
    public void OneLine_NoApt_FormatsCorrectly()
    {
        var addr = new Address("123 Main St", null, "Springfield", "IL", "62701");

        Assert.Equal("123 Main St, Springfield, IL 62701", addr.OneLine);
    }

    [Fact]
    public void OneLine_EmptyApt_FormatsWithoutApt()
    {
        var addr = new Address("456 Oak Ave", "", "Dallas", "TX", "75201");

        Assert.Equal("456 Oak Ave, Dallas, TX 75201", addr.OneLine);
    }

    // ── OneLine with Apt ─────────────────────────────────────────────────────

    [Fact]
    public void OneLine_WithApt_IncludesAptNumber()
    {
        var addr = new Address("789 Pine Rd", "Apt 4B", "Chicago", "IL", "60601");

        Assert.Equal("789 Pine Rd Apt 4B, Chicago, IL 60601", addr.OneLine);
    }

    // ── Default country ───────────────────────────────────────────────────────

    [Fact]
    public void DefaultCountry_IsUS()
    {
        var addr = new Address("1 Test St", null, "Anytown", "CA", "90001");

        Assert.Equal("US", addr.Country);
    }

    [Fact]
    public void ExplicitCountry_IsPreserved()
    {
        var addr = new Address("1 Rue de Rivoli", null, "Paris", "IDF", "75001", "FR");

        Assert.Equal("FR", addr.Country);
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void Address_EqualityByValue()
    {
        var a = new Address("123 Main St", null, "Springfield", "IL", "62701");
        var b = new Address("123 Main St", null, "Springfield", "IL", "62701");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Address_InequalityOnStreet()
    {
        var a = new Address("123 Main St", null, "Springfield", "IL", "62701");
        var b = new Address("456 Main St", null, "Springfield", "IL", "62701");

        Assert.NotEqual(a, b);
    }
}
