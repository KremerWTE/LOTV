namespace Lotv.Core.Models;

public class Announcement
{
    public int Id { get; set; }
    public int? ChapterId { get; set; } // null = national (all chapters)
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Audience { get; set; } = "Staff"; // Staff, Volunteers, Donors, Public
    public bool IsPinned { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string AuthorName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
