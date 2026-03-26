namespace Lotv.Core.Models;

public class Donor
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? ParishName { get; set; }
    public string? DioceseName { get; set; }
    public int? DioceseId { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public bool IsAnonymous { get; set; }
    public string? StripeCustomerId { get; set; }
    public bool IsRecurring { get; set; }
    public decimal? RecurringAmount { get; set; }
    public DateTime FirstGiftDate { get; set; }
    public DateTime LastGiftDate { get; set; }
    public decimal TotalGiven { get; set; }
    public int GiftCount { get; set; }
    public DonorTier Tier { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}";
}

public enum DonorTier
{
    Friend,       // < $250
    Supporter,    // $250–$999
    Champion,     // $1,000–$4,999
    Benefactor    // $5,000+
}
