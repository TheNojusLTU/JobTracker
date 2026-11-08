using JobTracker.Api;
using JobTracker.Data;

namespace JobTracker.Tests.Unit;

public class PagingTests
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

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    public void PagedResponse_TotalPagesRoundsUp(int totalCount, int pageSize, int expected)
    {
        var page = new PagedResponse<int>([], 1, pageSize, totalCount);

        Assert.Equal(expected, page.TotalPages);
    }

    [Fact]
    public void AvatarCatalog_KnowsItsKeys()
    {
        Assert.Equal("sunrise", AvatarCatalog.Default.Key);
        Assert.True(AvatarCatalog.Contains("ocean"));
        Assert.False(AvatarCatalog.Contains("Ocean"));
        Assert.False(AvatarCatalog.Contains(null));
    }
}
