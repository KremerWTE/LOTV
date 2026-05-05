namespace Lotv.Core.Models;

public class StaffTask
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string AssignedToUserId { get; set; } = "";
    public string AssignedToName { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    public string Status { get; set; } = "Open"; // Open, InProgress, Done, Cancelled
    public int? LinkedCaseId { get; set; }
    public int? LinkedDonorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
