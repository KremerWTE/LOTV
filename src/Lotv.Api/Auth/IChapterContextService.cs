namespace Lotv.Api.Auth;

/// <summary>
/// Exposes the current request's chapter scope from the authenticated user's JWT claims.
/// Inject as scoped. Returns null for HQAdmin (no chapter filter applied).
/// </summary>
public interface IChapterContextService
{
    /// <summary>ChapterId from JWT claim. Null if user is HQAdmin.</summary>
    int? ChapterId { get; }

    /// <summary>The authenticated user's ID.</summary>
    string UserId { get; }

    /// <summary>True if the current user is HQAdmin (no chapter filter).</summary>
    bool IsHqAdmin { get; }
}
