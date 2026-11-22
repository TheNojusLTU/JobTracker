using JobTracker.Extensions;
using JobTracker.Models;
using static JobTracker.Tests.TestSupport.QueryTestData;

namespace JobTracker.Tests.Extensions;

public class JobApplicationSearchExtensionsTests
{
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
    public void Search_NullSource_Throws()
    {
        List<JobApplication> missing = null!;

        Assert.Throws<ArgumentNullException>(() => missing.Search("a"));
    }
}
