using JobTracker.Extensions;
using JobTracker.Models;

namespace JobTracker.Tests.Unit;

public class QueryExtensionsTests
{
    private static readonly List<JobApplication> Applications =
    [
        new() { Id = 1, CompanyName = "Acme", JobTitle = "Backend Developer", Location = "Vilnius", Status = ApplicationStatus.Applied, ApplicationDate = new DateTime(2026, 9, 1) },
        new() { Id = 2, CompanyName = "Globex", JobTitle = "Frontend Developer", Location = "Kaunas", Status = ApplicationStatus.Interview, ApplicationDate = new DateTime(2026, 10, 1) },
        new() { Id = 3, CompanyName = "Initech", JobTitle = "QA Engineer", Location = null, Status = ApplicationStatus.Applied, ApplicationDate = new DateTime(2026, 10, 1) },
        new() { Id = 4, CompanyName = "Umbrella", JobTitle = "Data Analyst", Location = "vilnius", Status = ApplicationStatus.Rejected, ApplicationDate = new DateTime(2026, 11, 1) }
    ];

    private static int[] Ids(IEnumerable<JobApplication> applications) => applications.Select(a => a.Id).ToArray();

    [Theory]
    [InlineData("acme", new[] { 1 })]
    [InlineData("  DEVELOPER ", new[] { 1, 2 })]
    [InlineData("ini", new[] { 3 })]
    [InlineData("nothing", new int[0])]
    public void Search_MatchesCompanyOrTitleIgnoringCase(string text, int[] expected)
    {
        Assert.Equal(expected, Ids(Applications.Search(text)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_WithoutText_ReturnsEverything(string? text)
    {
        Assert.Equal([1, 2, 3, 4], Ids(Applications.Search(text)));
    }

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
    public void NullSource_Throws()
    {
        List<JobApplication> missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.Search("a"));
        Assert.Throws<ArgumentNullException>(() => missing.Filter());
        Assert.Throws<ArgumentNullException>(() => missing.SortByDate());
    }
}
