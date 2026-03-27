namespace Lotv.Core.Models;

/// <summary>
/// A corporate or organizational sponsor that provides funding or in-kind support to the ministry.
/// Distinct from individual Donors — typically companies with a contact person.
/// </summary>
public class Sponsor
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }

    public string CompanyName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? TaxId { get; set; }

    public SponsorTier Tier { get; set; } = SponsorTier.Bronze;
    public SponsorStatus Status { get; set; } = SponsorStatus.Active;

    public decimal CommittedAmount { get; set; }
    public decimal PaidToDate { get; set; }

    public DateTime? RenewalDate { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SponsorTier  { Platinum, Gold, Silver, Bronze }
public enum SponsorStatus { Active, Inactive, Lapsed, Prospect }
