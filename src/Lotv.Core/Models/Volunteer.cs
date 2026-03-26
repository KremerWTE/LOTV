namespace Lotv.Core.Models;

public class Volunteer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public VolunteerRole Role { get; set; }
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
