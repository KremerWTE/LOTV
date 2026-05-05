namespace Lotv.Core.Models;

/// <summary>Idempotency record for external webhook deliveries (Stripe events, etc).</summary>
public class WebhookEvent
{
    public int Id { get; set; }
    public string Source { get; set; } = "";       // "stripe", "givebutter", etc.
    public string ExternalId { get; set; } = "";   // Stripe Event.Id
    public string EventType { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Payload { get; set; }   // raw JSON, capped at ~32KB
}
