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
    public RequestCategory Category { get; set; } = RequestCategory.Other;
    public CaseStatus Status { get; set; } = CaseStatus.New;
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public int? AssignedToId { get; set; }         // VolunteerId or null
    public string? AssignedTo { get; set; }        // display name (kept for UI compat)
    public DateTime? DueDate { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ShippedDate { get; set; }
    public string? TrackingNumber { get; set; }
    public string? InternalNotes { get; set; }
    public string? ChildrenInitials { get; set; }
    public bool StaffOutreachRequested { get; set; } = true;
    public double? Latitude { get; set; }   // for geo-based auto-assignment scoring
    public double? Longitude { get; set; }
    public bool IsOverdue => Status is not (CaseStatus.Fulfilled or CaseStatus.Shipped or CaseStatus.Cancelled or CaseStatus.OnHold) && CreatedAt < DateTime.UtcNow.AddDays(-7);
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

public enum RequestPriority
{
    Urgent,
    High,
    Normal,
    Low
}

public enum RequestCategory
{
    PackageDelivery,
    PrayerSupport,
    CounselingReferral,
    HospitalVisit,
    Memorial,
    ResourceProvision,
    Other
}
