namespace Lotv.Core.Models;

/// <summary>
/// Stores per-chapter (or global) key-value configuration entries.
/// ChapterId null = global/HQ setting; non-null = chapter-scoped override.
/// </summary>
public class AppSetting
{
    public int     Id        { get; set; }
    public int?    ChapterId { get; set; }
    public string  Key       { get; set; } = "";
    public string  Value     { get; set; } = "";
}
