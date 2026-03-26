namespace Lotv.Core.Models;

/// <summary>
/// API key issued to a partner organisation for public API access.
/// Keys are stored hashed (SHA-256); the raw key is shown only once at creation.
/// </summary>
public class ApiKey
{
    public int Id { get; set; }

    /// <summary>SHA-256 hash of the raw API key (hex string).</summary>
    public string KeyHash { get; set; } = "";

    /// <summary>Human-readable name identifying the partner.</summary>
    public string PartnerName { get; set; } = "";

    public string? ContactEmail { get; set; }
    public int? ChapterId { get; set; }  // null = all chapters

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsValid   => IsActive && !IsExpired;
}

public enum ApiKeyScope
{
    ReadOnly,   // GET /api/public/v1/... only
    Write,      // POST intake requests and donations
    Admin       // all public endpoints including DELETE
}
