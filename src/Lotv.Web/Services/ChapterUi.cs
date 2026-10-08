namespace Lotv.Web.Services;

/// <summary>
/// One-organization mode for the staff portal. LOTV has no chapters today, so chapter pickers, chapter pages and
/// chapter menu entries are hidden, and any form that still stores a ChapterId gets the single default value.
/// Set Chapters:Enabled=true (in the API and the Web app) to bring the chapter screens back.
/// </summary>
public static class ChapterUi
{
    public static bool Enabled { get; set; }

    /// <summary>The ChapterId a form submits when it has no picker. 0 means "must choose" (chapters on).</summary>
    public static int DefaultChapterId => Enabled ? 0 : 1;
}
