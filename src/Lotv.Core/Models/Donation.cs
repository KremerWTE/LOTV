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
    public string? AllocatedTo { get; set; }
    public AllocationStatus AllocationStatus { get; set; } = AllocationStatus.Unallocated;
    public string? Notes { get; set; }
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
    Other
}

public enum AllocationStatus
{
    Unallocated,
    PendingReview,
    Allocated,
    Restricted
}
