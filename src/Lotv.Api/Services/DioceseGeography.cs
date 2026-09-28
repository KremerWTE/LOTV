using System.Reflection;
using Lotv.Api.Data;
using Lotv.Core.Common;

namespace Lotv.Api.Services;

/// <summary>
/// The diocesan map of the United States, shipped as two reference files:
///   • us-county-dioceses.csv: every county and the diocese covering it (26 counties are split between two dioceses).
///     Source: the county-by-diocese table of kburchfiel/us_diocese_mapper (public domain), which assigns each county
///     to a diocese from the published diocesan boundaries.
///   • us-place-dioceses.csv: towns and the diocese their county is in, kept only where every place with that name in the
///     state is in the same diocese. Built from GeoNames populated places (CC BY 4.0) and the county table above.
/// See docs/us-dioceses-source.md.
/// </summary>
public sealed class DioceseGeography : IDioceseGeography
{
    public static readonly DioceseGeography Instance = new();

    private readonly Lazy<Dictionary<string, GeographyHit>> _counties = new(() => Load("us-county-dioceses.csv", isCounty: true));
    private readonly Lazy<Dictionary<string, GeographyHit>> _places = new(() => Load("us-place-dioceses.csv", isCounty: false));

    public GeographyHit? ByCounty(string? stateCode, string? county) =>
        stateCode is null || string.IsNullOrWhiteSpace(county) ? null
        : _counties.Value.TryGetValue(Key(stateCode, CountyName(county)), out var hit) ? hit : null;

    public GeographyHit? ByPlace(string? stateCode, string? city) =>
        stateCode is null || string.IsNullOrWhiteSpace(city) ? null
        : _places.Value.TryGetValue(Key(stateCode, DioceseMatcher.NormalizePlace(city)), out var hit) ? hit : null;

    private static string Key(string state, string name) => $"{state.ToUpperInvariant()}|{name}";

    /// <summary>"Cook County", "St. Charles Parish" and "Saint Charles" all become the same key.</summary>
    public static string CountyName(string county)
    {
        var n = DioceseMatcher.NormalizePlace(county);
        foreach (var suffix in new[] { " county", " parish", " borough", " census area", " municipality" })
            if (n.EndsWith(suffix, StringComparison.Ordinal)) { n = n[..^suffix.Length]; break; }
        return n.Trim();
    }

    private static Dictionary<string, GeographyHit> Load(string file, bool isCounty)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase));
        var map = new Dictionary<string, GeographyHit>();
        if (resource is null) return map;
        using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
        foreach (var r in CsvReader.Parse(reader.ReadToEnd()).Skip(1))
        {
            if (r.Count < 4 || string.IsNullOrWhiteSpace(r[1])) continue;
            var also = isCounty && r.Count > 4 && !string.IsNullOrWhiteSpace(r[4]) ? r[4].Trim() : null;
            var name = isCounty ? CountyName(r[1]) : DioceseMatcher.NormalizePlace(r[1]);
            var how = isCounty ? $"{r[1].Trim()} County is in the {r[2].Trim()}" : $"{r[1].Trim()} is in the {r[2].Trim()}";
            map[Key(r[0].Trim(), name)] = new GeographyHit(r[2].Trim(), r[3].Trim(), also, how + (also is null ? "" : $" (split with {also})"));
        }
        return map;
    }
}
