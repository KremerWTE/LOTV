namespace Lotv.Core.Models;

public class VolunteerMagicLink
{
    public int Id { get; set; }
    public int VolunteerId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
