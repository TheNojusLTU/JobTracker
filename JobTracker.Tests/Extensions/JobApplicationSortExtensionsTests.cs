using JobTracker.Extensions;
using JobTracker.Models;
using static JobTracker.Tests.TestSupport.QueryTestData;

namespace JobTracker.Tests.Extensions;

public class JobApplicationSortExtensionsTests
{
    [Fact]
    public void SortByDate_NewestFirst_UsesIdForEqualDates()
    {
        Assert.Equal([4, 3, 2, 1], Ids(Applications.SortByDate()));
    }

    [Fact]
    public void SortByDate_OldestFirst_UsesIdForEqualDates()
    {
        Assert.Equal([1, 2, 3, 4], Ids(Applications.SortByDate(newestFirst: false)));
    }

    [Fact]
    public void SortByDate_NullSource_Throws()
    {
        List<JobApplication> missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.SortByDate());
    }
}
