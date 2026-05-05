namespace Lotv.Core.Models;

public class NotificationPref
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string EventType { get; set; } = ""; // NewRequest, Assignment, Escalation, Donation, DailyDigest, WeeklySummary
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
}
