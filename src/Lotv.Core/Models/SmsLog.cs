namespace Lotv.Core.Models;

/// <summary>
/// Immutable log of every outbound SMS message.
/// Used for audit, debugging, and Twilio reconciliation.
/// </summary>
public class SmsLog
{
    public int Id { get; set; }

    /// <summary>Destination phone number (E.164 format, e.g. +13125550100).</summary>
    public string ToPhoneNumber { get; set; } = "";

    public SmsMessageType MessageType { get; set; }

    /// <summary>Case / request this SMS relates to (nullable).</summary>
    public int? CaseId { get; set; }

    /// <summary>Volunteer or staff user this was sent to (nullable).</summary>
    public string? UserId { get; set; }

    /// <summary>The message body that was sent.</summary>
    public string Body { get; set; } = "";

    public bool Success { get; set; }

    /// <summary>Twilio Message SID (or equivalent provider ID) when successful.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Error description if delivery failed.</summary>
    public string? ErrorMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public enum SmsMessageType
{
    AssignmentNotification,
    OverdueReminder,
    CheckInConfirmation,
    AssignmentAccepted,
    Custom
}
