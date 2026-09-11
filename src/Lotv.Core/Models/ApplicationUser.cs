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
    public string? AvatarUrl { get; set; }   // data URL or hosted URL

    public string FullName => $"{FirstName} {LastName}";
}

public enum UserRole
{
    HQAdmin,
    ChapterAdmin,
    ChapterStaff,
    Volunteer,
    Donor,
    PublicUser,
    // Near-admin oversight role — same operational access as ChapterAdmin
    // (cases, donations, volunteers) but not System Admin / HQ-only pages.
    Director,
    // Governance role — read-only access to a small set of high-level
    // reports (money/resource flow, mission outcomes, health score,
    // resource forecast) via its own separate portal, not AdminLayout.
    // Deliberately excluded from "Staff"/"ChapterAdmin" policies so it
    // can never reach case/family PII or operational admin endpoints,
    // even by navigating directly to a URL.
    Board
}
