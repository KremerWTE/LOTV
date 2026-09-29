namespace Lotv.Core.Models;

/// <summary>
/// One ingredient of the chapter's standard comfort package — how many of a given <see cref="ResourceItem"/>
/// go into a single box. The Build Day planner multiplies this by a target box count to say what a group
/// build day actually needs, and whether current stock covers it.
/// </summary>
public class PackageRecipeItem
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public int ResourceItemId { get; set; }
    public ResourceItem? ResourceItem { get; set; }
    public int QuantityPerBox { get; set; } = 1;
}
