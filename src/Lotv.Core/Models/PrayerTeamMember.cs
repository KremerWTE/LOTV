namespace Lotv.Core.Models;

/// <summary>
/// Who is praying for a request — deliberately separate from <see cref="PackageRequest.AssignedToId"/>, the one
/// volunteer who assembles and ships the package. Many Prayer Ambassadors can be on one family's prayer team,
/// and a Prayer Ambassador can be on many families' teams at once. No accept/decline workflow: staff just add
/// or remove someone.
/// </summary>
public class PrayerTeamMember
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PackageRequest? Request { get; set; }
    public int VolunteerId { get; set; }
    public Volunteer? Volunteer { get; set; }
    public string AddedById { get; set; } = "";
    public string AddedByName { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
