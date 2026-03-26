using Lotv.Core.ValueObjects;

namespace Lotv.Tests.ValueObjects;

public class MoneyTests
{
    // ── Zero ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Zero_HasAmountZero()
    {
        Assert.Equal(0m, Money.Zero.Amount);
    }

    [Fact]
    public void Zero_CurrencyIsUSD()
    {
        Assert.Equal("USD", Money.Zero.Currency);
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_SameCurrency_ReturnsSummedAmount()
    {
        var a = new Money(100m);
        var b = new Money(250m);

        var result = a.Add(b);

        Assert.Equal(350m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Add_ToZero_ReturnsSameAmount()
    {
        var m = new Money(500m);

        var result = m.Add(Money.Zero);

        Assert.Equal(500m, result.Amount);
    }

    [Fact]
    public void Add_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var usd = new Money(100m, "USD");
        var eur = new Money(100m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void Add_ProducesNewInstance_NotMutating()
    {
        var original = new Money(100m);
        var other    = new Money(50m);

        var result = original.Add(other);

        Assert.Equal(100m, original.Amount); // original unchanged
        Assert.Equal(150m, result.Amount);
    }

    // ── Non-USD currency ──────────────────────────────────────────────────────

    [Fact]
    public void Add_NonUsdSameCurrency_Works()
    {
        var a = new Money(200m, "EUR");
        var b = new Money(300m, "EUR");

        var result = a.Add(b);

        Assert.Equal(500m, result.Amount);
        Assert.Equal("EUR", result.Currency);
    }

    // ── Default currency parameter ─────────────────────────────────────────────

    [Fact]
    public void Constructor_DefaultCurrencyIsUSD()
    {
        var m = new Money(99m);
        Assert.Equal("USD", m.Currency);
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void Money_EqualityByValue()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "USD");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Money_InequalityOnAmount()
    {
        var a = new Money(100m);
        var b = new Money(200m);

        Assert.NotEqual(a, b);
    }
}
