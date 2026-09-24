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

/// <summary>Father's Day to Father's Day (third Sunday of June), the same rule as <see cref="MothersDayCycle"/>.</summary>
public static class FathersDayCycle
{
    public static DateTime FathersDay(int year)
    {
        var d = new DateTime(year, 6, 1);
        while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
        return d.AddDays(14);
    }

    public static int YearFor(DateTime date) =>
        date.Date > FathersDay(date.Year) ? date.Year + 1 : date.Year;
}

public static class MailingCycle
{
    public static DateTime Holiday(MailingKind kind, int year) =>
        kind == MailingKind.FathersDay ? FathersDayCycle.FathersDay(year) : MothersDayCycle.MothersDay(year);

    public static int YearFor(MailingKind kind, DateTime date) =>
        kind == MailingKind.FathersDay ? FathersDayCycle.YearFor(date) : MothersDayCycle.YearFor(date);

    /// <summary>Requests made after last year's holiday, up to and including this year's, belong to the <paramref name="year"/> mailing.</summary>
    public static (DateTime AfterExclusive, DateTime BeforeExclusive) Window(MailingKind kind, int year) =>
        (Holiday(kind, year - 1).AddDays(1), Holiday(kind, year).AddDays(1));

    public static string HolidayName(MailingKind kind) => kind == MailingKind.FathersDay ? "Father's Day" : "Mother's Day";
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
