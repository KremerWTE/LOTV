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

    /// <summary>
    /// Set whenever an admin hands this person a temporary password (see POST /api/v1/users/{id}/set-temp-password).
    /// Checked at login: true forces the client to /change-password before anything else is reachable, so a temp
    /// password never becomes a standing one. Cleared by a successful POST /api/v1/auth/change-password.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
