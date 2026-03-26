namespace Lotv.Core.Models;

/// <summary>
/// Tracks the full assignment history for a request. Each auto-assign or manual assign
/// creates a new row. Supports the volunteer accept/decline workflow.
/// </summary>
public class RequestAssignment
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PackageRequest? Request { get; set; }
    public int AssignedToId { get; set; }          // VolunteerId or StaffId
    public string AssignedToName { get; set; } = "";
    public string AssignedById { get; set; } = ""; // ApplicationUser.Id ("system" for auto-assign)
    public string AssignedByName { get; set; } = "";
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime AcceptanceDeadline { get; set; }
    public int AttemptNumber { get; set; } = 1;
}

public enum AssignmentStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Reassigned,
    Completed
}
