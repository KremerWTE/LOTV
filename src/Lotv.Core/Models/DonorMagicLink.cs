namespace Lotv.Core.Models;

/// <summary>One-time login token for donor self-service portal.</summary>
public class DonorMagicLink
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public string Token { get; set; } = "";          // 32+ chars random
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
