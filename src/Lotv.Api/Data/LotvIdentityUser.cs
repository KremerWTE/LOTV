using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Data;

/// <summary>
/// Extends IdentityUser with LOTV-specific profile fields and role/chapter claims.
/// </summary>
public class LotvIdentityUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.PublicUser;
    public int? ChapterId { get; set; }    // null for HQAdmin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? AvatarUrl { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
