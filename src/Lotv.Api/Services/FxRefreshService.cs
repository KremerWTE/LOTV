using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Daily FX refresh: pulls USD-base rates from exchangerate.host (free, no key) and snapshots them.
/// Falls back to a sensible default set if the network call fails and the table is empty.
/// </summary>
public class FxRefreshService(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<FxRefreshService> log)
    : BackgroundService
{
    // 1 unit of CurrencyCode = X USD. Rough defaults so the app works offline / unconfigured.
    private static readonly Dictionary<string, decimal> Fallback = new()
    {
        ["USD"] = 1.00m,
        ["CAD"] = 0.74m,
        ["EUR"] = 1.08m,
        ["GBP"] = 1.27m,
        ["MXN"] = 0.058m,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial seed on boot, then refresh once a day.
        await SeedIfEmptyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RefreshAsync(stoppingToken); }
            catch (Exception ex) { log.LogWarning(ex, "FX refresh failed"); }

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        if (await db.ExchangeRates.AnyAsync(ct)) return;

        foreach (var (code, rate) in Fallback)
        {
            db.ExchangeRates.Add(new ExchangeRate { CurrencyCode = code, RateToUsd = rate, AsOf = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Seeded default FX rates ({Count})", Fallback.Count);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var client = http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var symbols = string.Join(",", Fallback.Keys.Where(c => c != "USD"));
        var url = $"https://api.exchangerate.host/latest?base=USD&symbols={symbols}";

        using var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) { log.LogDebug("FX provider returned {Status}", resp.StatusCode); return; }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("rates", out var rates)) return;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();

        // exchangerate.host gives "1 USD = X CCY". Convert to "1 CCY = (1/X) USD" for our schema.
        foreach (var prop in rates.EnumerateObject())
        {
            if (!prop.Value.TryGetDecimal(out var ccyPerUsd) || ccyPerUsd <= 0) continue;
            var rateToUsd = 1m / ccyPerUsd;
            db.ExchangeRates.Add(new ExchangeRate
            {
                CurrencyCode = prop.Name,
                RateToUsd    = decimal.Round(rateToUsd, 8),
                AsOf         = DateTime.UtcNow,
            });
        }

        // USD self-rate stays 1.00 (one row per refresh keeps history).
        db.ExchangeRates.Add(new ExchangeRate { CurrencyCode = "USD", RateToUsd = 1m, AsOf = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        log.LogInformation("FX rates refreshed");
    }
}
