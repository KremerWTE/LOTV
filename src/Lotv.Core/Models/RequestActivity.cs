namespace Lotv.Core.Models;

/// <summary>
/// Immutable per-request audit trail. Records every status change, assignment change, note added, etc.
/// </summary>
public class RequestActivity
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PackageRequest? Request { get; set; }
    public string ActorId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public ActivityType ActivityType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum ActivityType
{
    StatusChanged,
    Assigned,
    Reassigned,
    NoteAdded,
    DueDateSet,
    PriorityChanged,
    ProcessStageChanged,
    Fulfilled,
    Cancelled,
    Escalated,
    Created
}
