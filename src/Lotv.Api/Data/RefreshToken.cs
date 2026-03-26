namespace Lotv.Api.Data;

public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
}
