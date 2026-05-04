using Microsoft.JSInterop;
using static Lotv.Web.Services.ApiService;

namespace Lotv.Web.Services;

/// <summary>
/// Holds the user-selected display currency + cached rates from the public /currencies endpoint.
/// Amounts in the system are stored in USD; this service converts at display time.
/// </summary>
public class CurrencyService
{
    private readonly ApiService _api;
    private readonly IJSRuntime _js;
    private List<CurrencyDto> _cache = new();
    private string _current = "USD";

    public event Action? OnCurrencyChanged;

    public string Current => _current;
    public IReadOnlyList<CurrencyDto> Available => _cache;

    public CurrencyService(ApiService api, IJSRuntime js) { _api = api; _js = js; }

    public async Task InitializeAsync()
    {
        var list = await _api.GetCurrenciesAsync();
        if (list is not null) _cache = list;
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", "lotv.currency");
            if (!string.IsNullOrWhiteSpace(stored) && _cache.Any(c => c.Code == stored))
                _current = stored!;
        }
        catch { }
    }

    public async Task SetAsync(string code)
    {
        if (_cache.All(c => c.Code != code) || _current == code) return;
        _current = code;
        try { await _js.InvokeVoidAsync("localStorage.setItem", "lotv.currency", code); } catch { }
        OnCurrencyChanged?.Invoke();
    }

    public string Format(decimal amountUsd)
    {
        var c = _cache.FirstOrDefault(x => x.Code == _current);
        if (c is null || c.RateToUsd <= 0) return amountUsd.ToString("C");
        // RateToUsd: 1 unit of CurrencyCode = X USD. So display = USD ÷ RateToUsd.
        var converted = c.Code == "USD" ? amountUsd : amountUsd / c.RateToUsd;
        return $"{c.Symbol}{converted:0.00}";
    }
}
