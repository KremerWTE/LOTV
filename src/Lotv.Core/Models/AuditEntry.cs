namespace Lotv.Core.Models;

public class AuditEntry
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";    // Created, Updated, Deleted, Exported, etc.
    public string Entity { get; set; } = "";    // Family, Donation, Volunteer, etc.
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
