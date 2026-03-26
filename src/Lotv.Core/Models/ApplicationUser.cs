namespace Lotv.Core.Models;

public class ApplicationUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public UserRole Role { get; set; }
    public int? ChapterId { get; set; }   // null for HQAdmin
    public Chapter? Chapter { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

public enum UserRole
{
    HQAdmin,
    ChapterAdmin,
    ChapterStaff,
    Volunteer,
    Donor,
    PublicUser
}
