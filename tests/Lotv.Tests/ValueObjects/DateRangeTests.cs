using Lotv.Core.ValueObjects;

namespace Lotv.Tests.ValueObjects;

public class DateRangeTests
{
    // ── Contains ──────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_DateWithinRange_ReturnsTrue()
    {
        var range = new DateRange(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31));

        Assert.True(range.Contains(new DateTime(2026, 6, 15)));
    }

    [Fact]
    public void Contains_DateBeforeStart_ReturnsFalse()
    {
        var range = new DateRange(
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));

        Assert.False(range.Contains(new DateTime(2026, 2, 28)));
    }

    [Fact]
    public void Contains_DateAfterEnd_ReturnsFalse()
    {
        var range = new DateRange(
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 31));

        Assert.False(range.Contains(new DateTime(2026, 4, 1)));
    }

    [Fact]
    public void Contains_ExactStartDate_ReturnsTrue()
    {
        var start = new DateTime(2026, 3, 1);
        var range = new DateRange(start, new DateTime(2026, 3, 31));

        Assert.True(range.Contains(start));
    }

    [Fact]
    public void Contains_ExactEndDate_ReturnsTrue()
    {
        var end   = new DateTime(2026, 3, 31);
        var range = new DateRange(new DateTime(2026, 3, 1), end);

        Assert.True(range.Contains(end));
    }

    // ── DaysSpan ─────────────────────────────────────────────────────────────

    [Fact]
    public void DaysSpan_ThirtyOneDays_ReturnsCorrect()
    {
        var range = new DateRange(
            new DateTime(2026, 3, 1),
            new DateTime(2026, 4, 1));

        Assert.Equal(31, range.DaysSpan);
    }

    [Fact]
    public void DaysSpan_SameDay_ReturnsZero()
    {
        var day   = new DateTime(2026, 3, 15);
        var range = new DateRange(day, day);

        Assert.Equal(0, range.DaysSpan);
    }

    [Fact]
    public void DaysSpan_SevenDays_ReturnsCorrect()
    {
        var range = new DateRange(
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 8));

        Assert.Equal(7, range.DaysSpan);
    }

    // ── LastDays ──────────────────────────────────────────────────────────────

    [Fact]
    public void LastDays_30_ContainsYesterday()
    {
        var range     = DateRange.LastDays(30);
        var yesterday = DateTime.UtcNow.AddDays(-1);

        Assert.True(range.Contains(yesterday));
    }

    [Fact]
    public void LastDays_30_DoesNotContain60DaysAgo()
    {
        var range   = DateRange.LastDays(30);
        var tooOld  = DateTime.UtcNow.AddDays(-60);

        Assert.False(range.Contains(tooOld));
    }

    [Fact]
    public void LastDays_DaysSpanApproxEquals_RequestedDays()
    {
        var range = DateRange.LastDays(7);

        Assert.InRange(range.DaysSpan, 6, 7);
    }

    // ── ThisMonth ─────────────────────────────────────────────────────────────

    [Fact]
    public void ThisMonth_ContainsNow()
    {
        var range = DateRange.ThisMonth();

        Assert.True(range.Contains(DateTime.UtcNow));
    }

    [Fact]
    public void ThisMonth_StartIsFirstOfMonth()
    {
        var range = DateRange.ThisMonth();
        var now   = DateTime.UtcNow;

        Assert.Equal(new DateTime(now.Year, now.Month, 1), range.Start);
    }

    [Fact]
    public void ThisMonth_DoesNotContainLastMonthDate()
    {
        var range     = DateRange.ThisMonth();
        var lastMonth = DateTime.UtcNow.AddMonths(-1);

        Assert.False(range.Contains(lastMonth));
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void DateRange_EqualityByValue()
    {
        var start = new DateTime(2026, 1, 1);
        var end   = new DateTime(2026, 12, 31);

        var a = new DateRange(start, end);
        var b = new DateRange(start, end);

        Assert.Equal(a, b);
    }
}
