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

    [Fact]
    public void Map_ConvertsItemsToDifferentTypes_AndPreservesPaging()
    {
        var page = new PagedResponse<int>([3, 1, 2], 2, 3, 8);

        var labels = page.Map(value => $"Application {value}");
        var flags = page.Map(value => value > 1);

        Assert.Equal(["Application 3", "Application 1", "Application 2"], labels.Items);
        Assert.Equal([true, false, true], flags.Items);
        Assert.Equal(page.Page, labels.Page);
        Assert.Equal(page.PageSize, labels.PageSize);
        Assert.Equal(page.TotalCount, labels.TotalCount);
        Assert.Equal(page.TotalPages, labels.TotalPages);
        Assert.Equal(page.Page, flags.Page);
        Assert.Equal(page.PageSize, flags.PageSize);
        Assert.Equal(page.TotalCount, flags.TotalCount);
        Assert.Equal([3, 1, 2], page.Items);
    }

    [Fact]
    public void Map_EmptyPage_PreservesTotalCountWithoutCallingMapper()
    {
        var page = new PagedResponse<int>([], 3, 10, 12);

        var mapped = page.Map<string>(_ => throw new InvalidOperationException("No items to map."));

        Assert.Empty(mapped.Items);
        Assert.Equal(3, mapped.Page);
        Assert.Equal(10, mapped.PageSize);
        Assert.Equal(12, mapped.TotalCount);
        Assert.Equal(2, mapped.TotalPages);
    }

    [Fact]
    public void Map_NullMapper_Throws()
    {
        var page = new PagedResponse<int>([], 1, 10, 0);

        Assert.Throws<ArgumentNullException>(() => page.Map<string>(null!));
    }
}
