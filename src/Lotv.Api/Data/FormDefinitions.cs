using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lotv.Core.Models;

namespace Lotv.Api.Data;

/// <summary>
/// Built-in defaults and (de)serialization for the staff-editable public forms.
/// The default JSON ships inside the assembly (Data/FormDefaults/*.json) and is
/// served whenever no edited copy has been saved.
/// </summary>
public static class FormDefinitions
{
    public const string PrayerCareIntakeKey = "prayer-care-intake";

    private static readonly string[] KnownKeys = [PrayerCareIntakeKey];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Reason values the intake endpoint can actually store (PackageReason enum names).</summary>
    public static readonly IReadOnlySet<string> ReasonValues =
        Enum.GetNames<PackageReason>().ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string key) => KnownKeys.Contains(key);

    public static string DefaultJson(string key)
    {
        var asm = typeof(FormDefinitions).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".{key}.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No default form definition embedded for '{key}'.");
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IntakeFormDefinition Default(string key) =>
        JsonSerializer.Deserialize<IntakeFormDefinition>(DefaultJson(key), Json)!;

    public static IntakeFormDefinition? TryParse(string json)
    {
        try { return JsonSerializer.Deserialize<IntakeFormDefinition>(json, Json); }
        catch (JsonException) { return null; }
    }
}
