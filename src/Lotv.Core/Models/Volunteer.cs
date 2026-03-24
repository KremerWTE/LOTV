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
