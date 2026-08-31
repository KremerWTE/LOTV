namespace Lotv.Core.Models;

// Structured "what's physically going in this family's package" record, linking a
// PackageRequest to specific inventory (ResourceItem). Distinct from the older
// free-text-note-only inventory allocation flow — this is the one that actually
// backs a per-request packing checklist.
public class PackageContentItem
{
    public int Id { get; set; }
    public int PackageRequestId { get; set; }
    public PackageRequest? PackageRequest { get; set; }
    public int ResourceItemId { get; set; }
    public ResourceItem? ResourceItem { get; set; }
    public int Quantity { get; set; } = 1;
    public bool Packed { get; set; }
    public DateTime? PackedAt { get; set; }
    public string? PackedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
