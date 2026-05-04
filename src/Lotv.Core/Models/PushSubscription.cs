namespace Lotv.Core.Models;

/// <summary>Browser push subscription for staff request alerts.</summary>
public class PushSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
