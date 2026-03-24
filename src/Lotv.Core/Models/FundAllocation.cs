namespace Lotv.Core.Models;

public class FundAllocation
{
    public int Id { get; set; }
    public int DonationId { get; set; }
    public Donation? Donation { get; set; }
    public string AllocatedTo { get; set; } = "";  // Case ID, program name, etc.
    public decimal Amount { get; set; }
    public AllocationStatus Status { get; set; } = AllocationStatus.PendingReview;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
