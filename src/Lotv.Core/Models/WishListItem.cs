namespace Lotv.Core.Models;

/// <summary>
/// A specific good or resource that a family has requested.
/// Donors can browse open wish-list items and donate to fulfill them.
/// </summary>
public class WishListItem
{
    public int Id { get; set; }
    public int? FamilyId { get; set; }   // null = chapter-wide wish (not tied to a specific family)
    public Family? Family { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Short name of the requested item (e.g. "Knitted blanket — newborn size").</summary>
    public string Title { get; set; } = "";

    public string? Description { get; set; }
    public WishListCategory Category { get; set; }

    /// <summary>How many the family needs.</summary>
    public int QuantityRequested { get; set; } = 1;

    /// <summary>How many have been donated/fulfilled so far.</summary>
    public int QuantityFulfilled { get; set; } = 0;

    public WishListStatus Status { get; set; } = WishListStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }

    /// <summary>UserId of the donor who claimed/fulfilled this item (if any).</summary>
    public string? FulfilledByDonorId { get; set; }

    public string? Notes { get; set; }

    // ── Computed ─────────────────────────────────────────────────────────────

    public int QuantityRemaining => QuantityRequested - QuantityFulfilled;
    public bool IsFullyFulfilled => QuantityFulfilled >= QuantityRequested;
}

public enum WishListCategory
{
    KnittedBlanket,
    MemoryBox,
    GriefBook,
    BabyClothing,
    CarePackage,
    HospitalSupply,
    PhotoAlbum,
    JournalOrKeepsake,
    GiftCard,
    Other
}

public enum WishListStatus
{
    Open,
    PartiallyFulfilled,
    Fulfilled,
    Cancelled
}

public static class WishListCategoryExtensions
{
    public static string ToDisplayName(this WishListCategory c) => c switch
    {
        WishListCategory.KnittedBlanket     => "Knitted Blanket",
        WishListCategory.MemoryBox          => "Memory Box",
        WishListCategory.GriefBook         => "Grief Book",
        WishListCategory.BabyClothing      => "Baby Clothing",
        WishListCategory.CarePackage       => "Care Package",
        WishListCategory.HospitalSupply    => "Hospital Supply",
        WishListCategory.PhotoAlbum        => "Photo Album",
        WishListCategory.JournalOrKeepsake => "Journal / Keepsake",
        WishListCategory.GiftCard          => "Gift Card",
        _                                  => "Other"
    };
}
