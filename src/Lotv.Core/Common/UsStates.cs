namespace Lotv.Core.Common;

/// <summary>
/// Turns what people type into the State box ("Illinois", "il", "Ill.", " TX ") into the two-letter postal code, so the
/// same state is counted and mapped once. Anything that can't be recognised comes back null (never guessed).
/// </summary>
public static class UsStates
{
    private static readonly Dictionary<string, string> FullNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alabama"]="AL", ["alaska"]="AK", ["arizona"]="AZ", ["arkansas"]="AR", ["california"]="CA", ["colorado"]="CO",
        ["connecticut"]="CT", ["delaware"]="DE", ["district of columbia"]="DC", ["washington dc"]="DC", ["washington d.c."]="DC",
        ["florida"]="FL", ["georgia"]="GA", ["hawaii"]="HI", ["idaho"]="ID", ["illinois"]="IL", ["indiana"]="IN", ["iowa"]="IA",
        ["kansas"]="KS", ["kentucky"]="KY", ["louisiana"]="LA", ["maine"]="ME", ["maryland"]="MD", ["massachusetts"]="MA",
        ["michigan"]="MI", ["minnesota"]="MN", ["mississippi"]="MS", ["missouri"]="MO", ["montana"]="MT", ["nebraska"]="NE",
        ["nevada"]="NV", ["new hampshire"]="NH", ["new jersey"]="NJ", ["new mexico"]="NM", ["new york"]="NY",
        ["north carolina"]="NC", ["north dakota"]="ND", ["ohio"]="OH", ["oklahoma"]="OK", ["oregon"]="OR", ["pennsylvania"]="PA",
        ["rhode island"]="RI", ["south carolina"]="SC", ["south dakota"]="SD", ["tennessee"]="TN", ["texas"]="TX", ["utah"]="UT",
        ["puerto rico"]="PR", ["guam"]="GU", ["u.s. virgin islands"]="VI", ["us virgin islands"]="VI", ["virgin islands"]="VI",
        ["northern mariana islands"]="MP", ["american samoa"]="AS",
        ["vermont"]="VT", ["virginia"]="VA", ["washington"]="WA", ["west virginia"]="WV", ["wisconsin"]="WI", ["wyoming"]="WY",
    };

    // Older traditional abbreviations people still write.
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ala"]="AL", ["ariz"]="AZ", ["ark"]="AR", ["calif"]="CA", ["cal"]="CA", ["colo"]="CO", ["conn"]="CT", ["del"]="DE",
        ["fla"]="FL", ["ill"]="IL", ["ind"]="IN", ["kans"]="KS", ["kan"]="KS", ["mass"]="MA", ["mich"]="MI", ["minn"]="MN",
        ["miss"]="MS", ["mont"]="MT", ["neb"]="NE", ["nebr"]="NE", ["nev"]="NV", ["okla"]="OK", ["ore"]="OR", ["oreg"]="OR",
        ["penn"]="PA", ["penna"]="PA", ["tenn"]="TN", ["tex"]="TX", ["wash"]="WA", ["wis"]="WI", ["wisc"]="WI", ["wyo"]="WY",
    };

    private static readonly Dictionary<string, string> Names = FullNames.Concat(Abbreviations).ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","DC","FL","GA","HI","ID","IL","IN","IA","KS","KY","LA","ME","MD","MA","MI","MN",
        "MS","MO","MT","NE","NV","NH","NJ","NM","NY","NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA",
        "WV","WI","WY",
        "PR","GU","VI","MP","AS",   // territories
    };

    /// <summary>True for a state's full name ("Illinois", "New York") - how a diocese name can end ("Springfield in Illinois") - but not an abbreviation.</summary>
    public static bool IsStateName(string? text) => !string.IsNullOrWhiteSpace(text) && FullNames.ContainsKey(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

    /// <summary>The two-letter code for what was typed, or null when it isn't a recognisable US state.</summary>
    public static string? ToCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().TrimEnd('.').Trim();
        if (s.Length == 2 && Codes.Contains(s)) return s.ToUpperInvariant();
        s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));   // collapse inner whitespace
        return Names.TryGetValue(s, out var code) ? code : null;
    }
}
