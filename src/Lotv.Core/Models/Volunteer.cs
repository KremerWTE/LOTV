namespace Lotv.Core.Models;

public class Volunteer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public VolunteerRole Role { get; set; }
    public VolunteerLevel Level { get; set; } = VolunteerLevel.New;
    public VolunteerStatus Status { get; set; } = VolunteerStatus.Active;
    public string? ParishName { get; set; }
    public string? DioceseName { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double ServiceRadiusMiles { get; set; } = 25.0;
    public int ActiveCases { get; set; }
    public int TotalCasesFulfilled { get; set; }
    public DateTime JoinedDate { get; set; }
    public string? Notes { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

public enum VolunteerRole
{
    PackageAssembler,
    PrayerAmbassador,
    ParishLiaison,
    EventHelper,
    Driver,
    Admin
}

public enum VolunteerStatus
{
    Active,
    Inactive,
    Onboarding
}

// Tenure/seniority tier — distinct from VolunteerRole (what they do) and
// VolunteerStatus (are they currently active). Drives which extra content
// shows in the self-service volunteer portal (onboarding checklist for
// New, recognition/mentoring panel for Senior/Lead).
public enum VolunteerLevel
{
    New,
    Standard,
    Senior,
    Lead
}

public static class VolunteerLevelExtensions
{
    public static string ToDisplayName(this VolunteerLevel l) => l switch
    {
        VolunteerLevel.New      => "New Volunteer",
        VolunteerLevel.Standard => "Volunteer",
        VolunteerLevel.Senior   => "Senior Volunteer",
        VolunteerLevel.Lead     => "Lead Volunteer",
        _                       => l.ToString()
    };
}

public static class VolunteerRoleExtensions
{
    public static string ToDisplayName(this VolunteerRole r) => r switch
    {
        VolunteerRole.PackageAssembler => "Package Assembler",
        VolunteerRole.PrayerAmbassador => "Prayer Ambassador",
        VolunteerRole.ParishLiaison    => "Parish Liaison",
        VolunteerRole.EventHelper      => "Event Helper",
        VolunteerRole.Admin            => "Admin Support",
        _                              => r.ToString()
    };
}
