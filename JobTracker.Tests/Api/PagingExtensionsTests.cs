using JobTracker.Api;

namespace JobTracker.Tests.Api;

public class PagingExtensionsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(500, 20)]
    public void ValidatePaging_AcceptsValidValues(int page, int pageSize)
    {
        Assert.Empty(PagingExtensions.ValidatePaging(page, pageSize));
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(-5, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    [InlineData(int.MaxValue, 100, "page")]
    public void ValidatePaging_RejectsInvalidValues(int page, int pageSize, string expectedKey)
    {
        Assert.Equal([expectedKey], PagingExtensions.ValidatePaging(page, pageSize).Keys);
    }
}
