using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

/// <summary>Outcome of pushing one case to Shippo. Never thrown — a Shippo problem must not block case work.</summary>
public record ShippoOrderResult(bool Success, string? OrderId, string? Error);

/// <summary>
/// Hands a package case's shipping details to the ministry's Shippo account as an Order, so staff
/// buy the label there without uploading a CSV. Labels are NOT purchased here.
/// </summary>
public interface IShippoOrderClient
{
    /// <summary>False until a Shippo API token and ship-from address are configured; callers skip silently.</summary>
    bool IsConfigured { get; }
    Task<ShippoOrderResult> CreateOrderAsync(PackageRequest request, Family family, CancellationToken ct = default);
}
