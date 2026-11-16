using JobTracker.Extensions;
using JobTracker.Models;
using static JobTracker.Tests.TestSupport.QueryTestData;

namespace JobTracker.Tests.Extensions;

public class JobApplicationFilterExtensionsTests
{
    [Fact]
    public void Filter_ByStatus()
    {
        Assert.Equal([1, 3], Ids(Applications.Filter(status: ApplicationStatus.Applied)));
    }

    [Fact]
    public void Filter_ByLocation_IgnoresCaseAndSkipsMissingLocations()
    {
        Assert.Equal([1, 4], Ids(Applications.Filter(location: " VILN ")));
    }

    [Fact]
    public void Filter_ByDates_IncludesBothEndDays()
    {
        var dates = new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        Assert.Equal([2, 3, 4], Ids(Applications.Filter(dates: dates)));
    }

    [Fact]
    public void Filter_CombinesEveryCondition()
    {
        var dates = new DateRange(null, new DateTime(2026, 10, 15));

        Assert.Equal([1], Ids(Applications.Filter(ApplicationStatus.Applied, "vilnius", dates)));
    }

    [Fact]
    public void Filter_WithNoConditions_ReturnsEverything()
    {
        Assert.Equal([1, 2, 3, 4], Ids(Applications.Filter()));
    }

    [Fact]
    public void Filter_NullSource_Throws()
    {
        List<JobApplication> missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.Filter());
    }
}
