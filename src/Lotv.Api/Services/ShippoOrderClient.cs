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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_o.ApiToken) && !string.IsNullOrWhiteSpace(_o.FromStreet1)
        && !string.IsNullOrWhiteSpace(_o.FromCity) && !string.IsNullOrWhiteSpace(_o.FromState) && !string.IsNullOrWhiteSpace(_o.FromZip);

    public async Task<ShippoOrderResult> CreateOrderAsync(PackageRequest request, Family family, CancellationToken ct = default)
    {
        if (!IsConfigured) return new(false, null, "Shippo is not configured.");
        try
        {
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
                    name = _o.FromName, company = _o.FromCompany, street1 = _o.FromStreet1, street2 = _o.FromStreet2,
                    city = _o.FromCity, state = _o.FromState, zip = _o.FromZip, country = _o.FromCountry,
                    phone = _o.FromPhone, email = _o.FromEmail
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

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];
}
