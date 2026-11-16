using JobTracker.Api;

namespace JobTracker.Tests.Api;

public class PagedResponseTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expected)
    {
        var page = new PagedResponse<int>([], 1, pageSize, totalCount);

        Assert.Equal(expected, page.TotalPages);
    }
}
