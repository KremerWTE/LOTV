namespace Lotv.Core.Models;

/// <summary>
/// The annual card mailing runs "Mother's Day to Mother's Day": a family who joins after this
/// year's Mother's Day (2nd Sunday of May) belongs to next year's mailing, one who joins before
/// it belongs to this year's.
/// </summary>
public static class MothersDayCycle
{
    /// <summary>Second Sunday of May.</summary>
    public static DateTime MothersDay(int year)
    {
        var d = new DateTime(year, 5, 1);
        while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
        return d.AddDays(7);
    }

    /// <summary>The mailing year a family added on <paramref name="date"/> belongs to.</summary>
    public static int YearFor(DateTime date) =>
        date.Date > MothersDay(date.Year) ? date.Year + 1 : date.Year;
}

/// <summary>
/// Which parent on a family record is the mother. The system doesn't record it; the public
/// intake form stores the husband as parent 1 and the wife as parent 2, so when there is a
/// second parent she is treated as the mother, otherwise the only parent is.
/// </summary>
public static class FamilyParents
{
    private static string Full(string? first, string? last) => $"{first} {last}".Trim();

    private static bool HasSecond(Family f) => !string.IsNullOrWhiteSpace(f.Parent2FirstName);

    public static string MomFirstName(Family f) => HasSecond(f) ? f.Parent2FirstName! : f.Parent1FirstName;

    public static string MomLastName(Family f) =>
        HasSecond(f) && !string.IsNullOrWhiteSpace(f.Parent2LastName) ? f.Parent2LastName! : f.Parent1LastName;

    public static string MomFullName(Family f) => Full(MomFirstName(f), MomLastName(f));

    /// <summary>The other parent's full name, or null for a single-parent record.</summary>
    public static string? DadFullName(Family f) =>
        HasSecond(f) ? Full(f.Parent1FirstName, f.Parent1LastName) : null;
}
