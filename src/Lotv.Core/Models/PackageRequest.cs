namespace Lotv.Core.Models;

public class PackageRequest
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public Family? Family { get; set; }

    // Who submitted the request
    public bool IsForSelf { get; set; }
    public string? ReferrerName { get; set; }
    public string? ReferrerEmail { get; set; }

    public PackageReason Reason { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.New;
    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ShippedDate { get; set; }
    public string? TrackingNumber { get; set; }
    public string? InternalNotes { get; set; }
    public string? ChildrenInitials { get; set; }
    public bool StaffOutreachRequested { get; set; } = true;
    public bool IsOverdue => Status is not (CaseStatus.Fulfilled or CaseStatus.Shipped or CaseStatus.Cancelled) && CreatedAt < DateTime.UtcNow.AddDays(-7);
}

public enum CaseStatus
{
    New,
    InProgress,
    AwaitingShipment,
    Shipped,
    Fulfilled,
    OnHold,
    Cancelled
}
