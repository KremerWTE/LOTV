namespace Lotv.Api.Auth;

/// <summary>
/// Whether the organization is split into chapters. Off by default: LOTV currently runs as ONE organization, so every
/// signed-in user sees all cases/families/volunteers and nobody is filtered by a chapter. The data model keeps its
/// ChapterId columns (they hold a single default value), so turning chapters on later (Chapters:Enabled=true) restores
/// the old per-chapter scoping without a migration.
/// </summary>
public static class ChapterMode
{
    public static bool Enabled { get; set; }

    /// <summary>The live-update (SignalR) group a chapter's events go to. One shared group when chapters are off.</summary>
    public static string GroupFor(int chapterId) => Enabled ? $"chapter-{chapterId}" : "org";
}
