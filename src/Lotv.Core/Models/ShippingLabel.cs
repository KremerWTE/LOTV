namespace Lotv.Core.Models;

/// <summary>
/// A shipping label for a Prayer Care Package case. Phase one has no real carrier integration — the
/// platform (maybe Shippo, not confirmed) is still undecided — so this only records that a label was
/// generated and holds a placeholder tracking number. Carrier, ServiceLevel and LabelFileUrl stay null
/// until a real IShippingLabelGenerator implementation fills them in.
/// </summary>
public class ShippingLabel
{
    public int Id { get; set; }
    public int PackageRequestId { get; set; }
    public PackageRequest? PackageRequest { get; set; }

    public string TrackingNumber { get; set; } = "";
    public bool IsPlaceholder { get; set; } = true;
    public string? Carrier { get; set; }
    public string? ServiceLevel { get; set; }
    public string? LabelFileUrl { get; set; }
    /// <summary>Shippo Order id once the case has been pushed to the ministry's Shippo account.</summary>
    public string? ShippoOrderId { get; set; }
    public DateTime? ShippoSyncedAt { get; set; }
    /// <summary>Last Shippo failure message, cleared on success; null when never attempted or fine.</summary>
    public string? ShippoSyncError { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
