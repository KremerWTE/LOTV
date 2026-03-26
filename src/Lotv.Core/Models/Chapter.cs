namespace Lotv.Core.Models;

public class Chapter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Configuration for auto-assignment engine (overrides system defaults)
    public int MaxActiveCasesPerVolunteer { get; set; } = 6;
    public int AcceptanceWindowHours { get; set; } = 24;
    public int UrgentAcceptanceWindowHours { get; set; } = 4;
    public int MaxReassignmentAttempts { get; set; } = 3;
    public double DefaultServiceRadiusMiles { get; set; } = 25.0;
}
