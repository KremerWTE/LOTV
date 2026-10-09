using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

/// <summary>Outcome of pushing one case to Shippo. Never thrown — a Shippo problem must not block case work.</summary>
public record ShippoOrderResult(bool Success, string? OrderId, string? Error);

/// <summary>What Shippo says about an order label. <see cref="Purchased"/> is false until staff buy the label in Shippo.</summary>
public record ShippoLabelResult(bool Success, bool Purchased, string? TrackingNumber, string? Carrier, string? ServiceLevel, string? LabelUrl, string? Error);

/// <summary>
/// Hands a package case's shipping details to the ministry's Shippo account as an Order, so staff
/// buy the label there without uploading a CSV. Labels are NOT purchased here.
/// </summary>
public interface IShippoOrderClient
{
    /// <summary>False until a Shippo API token and ship-from address are configured; callers skip silently.</summary>
    bool IsConfigured { get; }
    Task<ShippoOrderResult> CreateOrderAsync(PackageRequest request, Family family, CancellationToken ct = default);
    /// <summary>Reads the order back from Shippo: once staff have bought the label there, returns its tracking number, carrier and label link.</summary>
    Task<ShippoLabelResult> GetLabelAsync(string orderId, CancellationToken ct = default);
}
