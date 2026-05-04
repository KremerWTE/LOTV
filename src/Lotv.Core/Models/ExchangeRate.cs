namespace Lotv.Core.Models;

/// <summary>Snapshot of a currency conversion rate to USD.</summary>
public class ExchangeRate
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = "USD"; // ISO 4217
    public decimal RateToUsd { get; set; } = 1m;      // 1 unit of CurrencyCode = X USD
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}

public static class SupportedCurrencies
{
    public static readonly IReadOnlyList<(string Code, string Symbol, string Name)> All = new (string, string, string)[]
    {
        ("USD", "$",   "US Dollar"),
        ("CAD", "CA$", "Canadian Dollar"),
        ("EUR", "€",   "Euro"),
        ("GBP", "£",   "British Pound"),
        ("MXN", "MX$", "Mexican Peso"),
    };
}
