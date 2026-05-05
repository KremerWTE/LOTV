namespace Lotv.Core.Models;

public class Campaign
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal GoalAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Completed, Paused, Cancelled
    public string? ExternalCode { get; set; } // maps to GiveButter campaign slug or Stripe metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
