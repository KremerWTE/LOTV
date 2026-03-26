namespace Lotv.Core.ValueObjects;

public record DateRange(DateTime Start, DateTime End)
{
    public bool Contains(DateTime date) => date >= Start && date <= End;
    public int DaysSpan => (int)(End - Start).TotalDays;

    public static DateRange ThisMonth()
    {
        var now   = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1);
        var end   = start.AddMonths(1).AddTicks(-1);
        return new DateRange(start, end);
    }

    public static DateRange LastDays(int days) =>
        new(DateTime.UtcNow.AddDays(-days), DateTime.UtcNow);
}
