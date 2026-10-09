using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Lotv.Api.Services;

/// <summary>Settings under the "Shippo" config section. Token comes from secrets/env, never the repo.</summary>
public class ShippoOptions
{
    public string? ApiToken { get; set; }
    public string BaseUrl { get; set; } = "https://api.goshippo.com";
    /// <summary>
    /// The return address is read from the Shippo account on every order (its saved addresses). If more than one is saved,
    /// this picks the one whose name or company contains this text. The From* settings below are only a fallback for when
    /// Shippo has no saved address.
    /// </summary>
    public string? ReturnAddressName { get; set; }
    public string? FromName { get; set; }
    public string? FromCompany { get; set; }
    public string? FromStreet1 { get; set; }
    public string? FromStreet2 { get; set; }
    public string? FromCity { get; set; }
    public string? FromState { get; set; }
    public string? FromZip { get; set; }
    public string FromCountry { get; set; } = "US";
    public string? FromPhone { get; set; }
    public string? FromEmail { get; set; }
    /// <summary>Default package weight in pounds, sent so Shippo can pre-fill the label form.</summary>
    public decimal DefaultWeightLb { get; set; } = 2;
}

public class ShippoOrderClient(HttpClient http, IOptions<ShippoOptions> options, ILogger<ShippoOrderClient> log) : IShippoOrderClient
{
    private readonly ShippoOptions _o = options.Value;

    // Only the token is needed up front: the return address is looked up in Shippo when the order is sent.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ApiToken);

    private sealed record ShipFrom(string? Name, string? Company, string? Street1, string? Street2, string? City, string? State, string? Zip,
                                   string? Country, string? Phone, string? Email);

    private bool FallbackComplete =>
        !string.IsNullOrWhiteSpace(_o.FromStreet1) && !string.IsNullOrWhiteSpace(_o.FromCity)
        && !string.IsNullOrWhiteSpace(_o.FromState) && !string.IsNullOrWhiteSpace(_o.FromZip);

    private ShipFrom Fallback() => new(_o.FromName, _o.FromCompany, _o.FromStreet1, _o.FromStreet2, _o.FromCity, _o.FromState, _o.FromZip,
                                       _o.FromCountry, _o.FromPhone, _o.FromEmail);

    /// <summary>
    /// Reads the saved addresses from the Shippo account and picks the return address. Never guesses: with several saved
    /// addresses it needs a name hint or a default flag, otherwise it fails with a message the case page shows.
    /// </summary>
    private async Task<(ShipFrom? From, string? Error)> ResolveReturnAddressAsync(CancellationToken ct)
    {
        List<ShipFrom> saved = [];
        List<bool> isDefault = [];
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_o.BaseUrl.TrimEnd('/')}/addresses/?results=100");
            req.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _o.ApiToken);
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
                if (json.ValueKind == System.Text.Json.JsonValueKind.Object && json.TryGetProperty("results", out var results)
                    && results.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var a in results.EnumerateArray())
                    {
                        string? S(string n) => a.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
                        var from = new ShipFrom(S("name"), S("company"), S("street1"), S("street2"), S("city"), S("state"), S("zip"),
                                                S("country") ?? "US", S("phone"), S("email"));
                        if (string.IsNullOrWhiteSpace(from.Street1) || string.IsNullOrWhiteSpace(from.City)
                            || string.IsNullOrWhiteSpace(from.State) || string.IsNullOrWhiteSpace(from.Zip)) continue;
                        saved.Add(from);
                        isDefault.Add(new[] { "is_default_sender", "is_default", "default" }
                            .Any(n => a.TryGetProperty(n, out var d) && d.ValueKind == System.Text.Json.JsonValueKind.True));
                    }
                }
            }
            else log.LogWarning("Shippo address lookup returned HTTP {Status}", (int)resp.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Shippo address lookup failed");
        }

        if (saved.Count == 0)
            return FallbackComplete ? (Fallback(), null)
                : (null, "No return address found. Save one in Shippo (Settings → Addresses), or set the Shippo return-address settings.");

        var idx = Enumerable.Range(0, saved.Count).ToList();
        if (!string.IsNullOrWhiteSpace(_o.ReturnAddressName))
        {
            var hint = _o.ReturnAddressName.Trim();
            idx = idx.Where(i => (saved[i].Name?.Contains(hint, StringComparison.OrdinalIgnoreCase) ?? false)
                              || (saved[i].Company?.Contains(hint, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            if (idx.Count == 0)
                return (null, $"No address saved in Shippo matches the return-address name \"{hint}\".");
        }
        if (idx.Count == 1) return (saved[idx[0]], null);
        var defaults = idx.Where(i => isDefault[i]).ToList();
        if (defaults.Count == 1) return (saved[defaults[0]], null);
        return (null, $"{idx.Count} addresses are saved in Shippo and none can be chosen safely. Set the Shippo return-address name (SHIPPO_RETURN_ADDRESS_NAME) to part of the right address's name.");
    }

    public async Task<ShippoOrderResult> CreateOrderAsync(PackageRequest request, Family family, CancellationToken ct = default)
    {
        if (!IsConfigured) return new(false, null, "Shippo is not configured.");
        try
        {
            var (shipFrom, fromError) = await ResolveReturnAddressAsync(ct);
            if (shipFrom is null) return new(false, null, fromError);

            // Shipping essentials only — no reason, loss type, story or notes.
            var body = new
            {
                order_number = $"LOTV-{request.Id}",
                order_status = "PAID",
                placed_at = request.CreatedAt.ToString("o"),
                notes = $"LOTV case #{request.Id}",
                weight = _o.DefaultWeightLb.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                weight_unit = "lb",
                to_address = new
                {
                    name = family.FullName, street1 = family.StreetAddress, street2 = family.Apt,
                    city = family.City, state = family.State, zip = family.Zip, country = "US",
                    phone = family.Phone, email = family.Email
                },
                from_address = new
                {
                    name = shipFrom.Name, company = shipFrom.Company, street1 = shipFrom.Street1, street2 = shipFrom.Street2,
                    city = shipFrom.City, state = shipFrom.State, zip = shipFrom.Zip, country = shipFrom.Country ?? "US",
                    phone = shipFrom.Phone, email = shipFrom.Email
                },
                line_items = new[] { new { title = "Prayer Care Package", sku = "PCP", quantity = 1 } }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_o.BaseUrl.TrimEnd('/')}/orders/") { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _o.ApiToken);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var detail = await resp.Content.ReadAsStringAsync(ct);
                log.LogWarning("Shippo order create failed for case {CaseId}: HTTP {Status}", request.Id, (int)resp.StatusCode);
                return new(false, null, $"Shippo returned HTTP {(int)resp.StatusCode}. {Truncate(detail, 300)}");
            }
            var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
            var id = json.TryGetProperty("object_id", out var oid) ? oid.GetString() : null;
            return string.IsNullOrEmpty(id) ? new(false, null, "Shippo accepted the order but returned no id.") : new(true, id, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Shippo order create threw for case {CaseId}", request.Id);
            return new(false, null, "Couldn't reach Shippo: " + ex.Message);
        }
    }

    public async Task<ShippoLabelResult> GetLabelAsync(string orderId, CancellationToken ct = default)
    {
        if (!IsConfigured) return new(false, false, null, null, null, null, "Shippo is not configured.");
        try
        {
            var json = await GetJsonAsync($"/orders/{Uri.EscapeDataString(orderId)}", ct);
            if (json is null) return new(false, false, null, null, null, null, "Could not read the order from Shippo.");
            string? S(System.Text.Json.JsonElement e, string n) =>
                e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

            // The label shows up as a successful "transaction" on the order once it has been bought.
            System.Text.Json.JsonElement? bought = null;
            if (json.Value.TryGetProperty("transactions", out var txs) && txs.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var t in txs.EnumerateArray())
                    if (string.Equals(S(t, "status"), "SUCCESS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(S(t, "tracking_number")))
                        bought = t;
            if (bought is null) return new(true, false, null, null, null, null, null);

            string? carrier = null, service = null;
            var rateId = S(bought.Value, "rate");
            if (!string.IsNullOrEmpty(rateId))
            {
                var rate = await GetJsonAsync($"/rates/{Uri.EscapeDataString(rateId)}", ct);
                if (rate is not null)
                {
                    carrier = S(rate.Value, "provider");
                    if (rate.Value.TryGetProperty("servicelevel", out var sl)) service = S(sl, "name");
                }
            }
            return new(true, true, S(bought.Value, "tracking_number"), carrier, service, S(bought.Value, "label_url"), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Shippo label lookup threw for order {OrderId}", orderId);
            return new(false, false, null, null, null, null, "Could not reach Shippo: " + ex.Message);
        }
    }

    private async Task<System.Text.Json.JsonElement?> GetJsonAsync(string path, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_o.BaseUrl.TrimEnd('/')}{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _o.ApiToken);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
