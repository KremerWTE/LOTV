namespace Lotv.Core.Models;

public class Donation
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public DonationChannel Channel { get; set; }
    public string? Campaign { get; set; }
    public bool IsRecurring { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? CheckNumber { get; set; }
    public int? EventId { get; set; }
    public int ChapterId { get; set; }
    public ContributionStatus ContributionStatus { get; set; } = ContributionStatus.Processed;
    public string? AllocatedTo { get; set; }
    public AllocationStatus AllocationStatus { get; set; } = AllocationStatus.Unallocated;
    public string? Notes { get; set; }
    public string? GiveButterTransactionId { get; set; }  // dedup for GB sync
}

public enum DonationChannel
{
    Online,
    Check,
    Cash,
    Gala,
    SilentAuction,
    CorporateMatch,
    PlannedGiving,
    GiveButter,
    Other
}

public enum AllocationStatus
{
    Unallocated,
    PendingReview,
    Allocated,
    Restricted
}

public enum ContributionStatus
{
    Pending,
    Processed,
    Failed,
    Refunded
}

public static class DonationChannelExtensions
{
    public static string ToDisplayName(this DonationChannel c) => c switch
    {
        DonationChannel.SilentAuction  => "Silent Auction",
        DonationChannel.CorporateMatch => "Corporate Match",
        DonationChannel.PlannedGiving  => "Planned Giving",
        _                              => c.ToString()
    };
}
