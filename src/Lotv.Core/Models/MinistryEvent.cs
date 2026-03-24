namespace Lotv.Core.Models;

public class MinistryEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public EventType Type { get; set; }
    public DateTime Date { get; set; }
    public string Location { get; set; } = "";
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public int Registered { get; set; }
    public bool IsVirtual { get; set; }
    public string? MeetingLink { get; set; }
    public string? Notes { get; set; }
}

public enum EventType
{
    PrayerNight,
    InPersonGathering,
    Gala,
    SilentAuction,
    Workshop,
    Other
}
