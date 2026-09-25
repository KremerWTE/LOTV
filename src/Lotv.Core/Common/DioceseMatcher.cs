using Lotv.Core.Models;

namespace Lotv.Core.Common;

/// <summary>How a parish was tied to a diocese, or why it couldn't be.</summary>
/// <param name="Diocese">The diocese, or null when none could be chosen safely.</param>
/// <param name="How">Plain-language reason ("named in the list", "same city as the diocese's seat", ...).</param>
/// <param name="Candidates">When several dioceses are possible, which ones (so a person can choose).</param>
public sealed record DioceseMatch(Diocese? Diocese, string How, IReadOnlyList<Diocese> Candidates)
{
    public bool IsMatch => Diocese is not null;
}

/// <summary>
/// Works out which diocese a parish belongs to. A parish must always have one, and a wrong diocese is worse than none,
/// so this only answers when it is sure: the diocese was named, the parish is in the same city as a diocese's seat, or
/// the state has exactly one diocese. Anything else (a state with several dioceses and a parish that isn't in a seat
/// city) comes back with the candidates to choose from and no guess.
/// </summary>
public static class DioceseMatcher
{
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "roman", "catholic", "archdiocese", "diocese", "eparchy", "archeparchy", "of", "the", "for",
    };

    /// <summary>"Archdiocese of St. Paul and Minneapolis" and "Saint Paul and Minneapolis" both become "st paul and minneapolis".</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var cleaned = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Filler.Contains(w))
            .Select(w => w == "saint" ? "st" : w).ToList();
        // "Springfield in Illinois" and "Portland in Oregon": the state is where the diocese is, not part of what it is called.
        var inAt = words.LastIndexOf("in");
        if (inAt > 0 && inAt < words.Count - 1 && UsStates.IsStateName(string.Join(' ', words.Skip(inAt + 1)))) words = words.Take(inAt).ToList();
        return string.Join(' ', words);
    }

    /// <summary>A city or parish name reduced for comparing ("St. Mary's" and "Saint Marys" match).</summary>
    public static string NormalizePlace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cleaned = new string(text.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w == "saint" ? "st" : w));
    }

    public static DioceseMatch Find(IReadOnlyCollection<Diocese> dioceses, string? dioceseName, string? city, string? state,
        string? county = null, IDioceseGeography? geography = null)
    {
        var code = UsStates.ToCode(state);

        // 1. The list named the diocese.
        var wanted = Normalize(dioceseName);
        if (wanted.Length > 0)
        {
            var named = dioceses.Where(d => Normalize(d.Name) == wanted).ToList();
            if (named.Count > 1 && code is not null)
            {
                var inState = named.Where(d => UsStates.ToCode(d.State) == code).ToList();
                if (inState.Count > 0) named = inState;
            }
            return named.Count switch
            {
                1 => new DioceseMatch(named[0], "named in the list", []),
                0 => new DioceseMatch(null, $"no diocese called \"{dioceseName!.Trim()}\" exists", []),
                _ => new DioceseMatch(null, $"more than one diocese is called \"{dioceseName!.Trim()}\"; add the state", named),
            };
        }

        // 2. Otherwise go by the parish's state, and its city.
        if (code is null) return new DioceseMatch(null, "no diocese and no state to work one out from", []);

        var here = dioceses.Where(d => UsStates.ToCode(d.State) == code).ToList();

        var place = NormalizePlace(city);
        if (place.Length > 0)
        {
            var seat = here.Where(d => NormalizePlace(d.City) == place).ToList();
            if (seat.Count == 1) return new DioceseMatch(seat[0], "same city as the diocese's seat", []);
        }

        // The diocesan map: by county when the file gives one, then by town. A diocese may be seated in the next state over
        // (part of Idaho is in the Diocese of Cheyenne), so this looks at every diocese, not just this state's.
        if (geography is not null)
        {
            Diocese? Resolve(GeographyHit hit, string name)
            {
                var found = dioceses.Where(d => Normalize(d.Name) == Normalize(name) && (hit.DioceseState.Length == 0 || UsStates.ToCode(d.State) == hit.DioceseState)).Take(2).ToList();
                return found.Count == 1 ? found[0] : null;
            }

            IReadOnlyList<Diocese> split = [];
            var splitHow = "";
            var byCounty = geography.ByCounty(code, county);
            if (byCounty is not null)
            {
                var primary = Resolve(byCounty, byCounty.DioceseName);
                if (primary is not null && byCounty.AlsoIn is null) return new DioceseMatch(primary, byCounty.How, []);
                if (primary is not null)
                {
                    // A county split between two dioceses: only the parish's town can settle it.
                    var other = dioceses.Where(d => Normalize(d.Name) == Normalize(byCounty.AlsoIn)).Take(2).ToList();
                    split = other.Count == 1 ? [primary, other[0]] : [primary];
                    splitHow = byCounty.How;
                }
            }
            var byPlace = geography.ByPlace(code, city);
            var viaTown = byPlace is null ? null : Resolve(byPlace, byPlace.DioceseName);
            if (viaTown is not null && (split.Count == 0 || split.Any(s => s.Id == viaTown.Id)))
                return new DioceseMatch(viaTown, byPlace!.How, []);
            if (split.Count > 1) return new DioceseMatch(null, splitHow, split);
        }

        if (here.Count == 0) return new DioceseMatch(null, $"no diocese is on file for {code}", []);
        if (here.Count == 1) return new DioceseMatch(here[0], $"the only diocese in {code}", []);

        return new DioceseMatch(null, $"{code} has {here.Count} dioceses and the city isn't a diocese seat; choose one", here);
    }
}
