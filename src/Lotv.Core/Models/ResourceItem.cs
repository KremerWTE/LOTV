namespace Lotv.Core.Models;

/// <summary>
/// Physical resource donated to a chapter (baby clothing, memory boxes, etc.).
/// Quantity is decremented when an item is allocated to a request.
/// </summary>
public class ResourceItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ResourceCategory Category { get; set; }
    public string? Description { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }    // allocated but not yet delivered
    public string Unit { get; set; } = "unit";  // "box", "bag", "set", etc.
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
}

public enum ResourceCategory
{
    BabyClothing,
    MemoryBox,
    GriefBook,
    KnittedBlanket,
    CarePackage,
    HospitalSupply,
    OtherPhysicalGood
}
