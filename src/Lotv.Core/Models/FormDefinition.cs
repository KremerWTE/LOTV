namespace Lotv.Core.Models;

/// <summary>
/// Stored, staff-editable definition of a public form (currently only the
/// prayer care intake form). One row per <see cref="Key"/>; no row means the
/// built-in default definition is used.
/// </summary>
public class FormDefinition
{
    public int      Id             { get; set; }
    public string   Key            { get; set; } = "";
    public string   DefinitionJson { get; set; } = "";
    public DateTime UpdatedAt      { get; set; } = DateTime.UtcNow;
    public string?  UpdatedBy      { get; set; }
}
