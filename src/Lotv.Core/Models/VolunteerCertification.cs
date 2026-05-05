namespace Lotv.Core.Models;

public class VolunteerCertification
{
    public int Id { get; set; }
    public int VolunteerId { get; set; }
    public string CertType { get; set; } = ""; // "CPR", "BackgroundCheck", "SafeEnvironment", "FirstAid", "Other"
    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiresDate { get; set; }
    public bool IsVerified { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Volunteer? Volunteer { get; set; }
}
