namespace Lotv.Core.Models;

public class MinistryEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public EventType Type { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public string Location { get; set; } = "";
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public int Registered { get; set; }
    public decimal? TicketPrice { get; set; }
    public decimal? GoalAmount { get; set; }
    public bool IsVirtual { get; set; }
    public string? MeetingLink { get; set; }
    public string CreatedBy { get; set; } = "";    // ApplicationUser.Id
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum EventType
{
    PrayerNight,
    InPersonGathering,
    Gala,
    SilentAuction,
    Workshop,
    Dinner,
    Concert,
    GolfTournament,
    Walkathon,
    Other
}

public enum EventStatus
{
    Draft,
    Published,
    Open,
    Closed,
    Completed,
    Cancelled
}
