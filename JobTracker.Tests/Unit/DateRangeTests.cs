using JobTracker.Models;

namespace JobTracker.Tests.Unit;

public class DateRangeTests
{
    [Fact]
    public void Constructor_FromAfterTo_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DateRange(new DateTime(2026, 10, 2), new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void Constructor_SameDayWithDifferentTimes_IsAllowed()
    {
        var range = new DateRange(new DateTime(2026, 10, 1, 18, 0, 0), new DateTime(2026, 10, 1, 9, 0, 0));

        Assert.Equal(new DateTime(2026, 10, 1), range.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 2), range.EndUtc);
    }

    [Fact]
    public void StartAndEnd_AreUtcDayBoundaries()
    {
        var range = new DateRange(new DateTime(2026, 10, 1, 13, 0, 0), new DateTime(2026, 10, 5, 13, 0, 0));

        Assert.Equal(DateTimeKind.Utc, range.StartUtc!.Value.Kind);
        Assert.Equal(new DateTime(2026, 10, 1), range.StartUtc);
        Assert.Equal(new DateTime(2026, 10, 6), range.EndUtc);
    }

    [Theory]
    [InlineData("2026-09-30T23:59:59", false)]
    [InlineData("2026-10-01T00:00:00", true)]
    [InlineData("2026-10-05T23:59:59", true)]
    [InlineData("2026-10-06T00:00:00", false)]
    public void Contains_IncludesWholeFirstAndLastDay(string date, bool expected)
    {
        var range = new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 5));

        Assert.Equal(expected, range.Contains(DateTime.Parse(date)));
    }

    [Fact]
    public void OpenEndedRange_HasNoLimitOnMissingSide()
    {
        var onlyFrom = new DateRange(new DateTime(2026, 10, 1), null);
        var onlyTo = new DateRange(null, new DateTime(2026, 10, 1));

        Assert.Null(onlyFrom.EndUtc);
        Assert.Null(onlyTo.StartUtc);
        Assert.True(onlyFrom.Contains(DateTime.MaxValue));
        Assert.True(onlyTo.Contains(DateTime.MinValue));
    }

    [Fact]
    public void Equality_ComparesBothDates()
    {
        var a = new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 5));
        var b = new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 5));
        var c = new DateRange(new DateTime(2026, 10, 1), null);

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a != c);
        Assert.False(a.Equals("not a range"));
    }
}
