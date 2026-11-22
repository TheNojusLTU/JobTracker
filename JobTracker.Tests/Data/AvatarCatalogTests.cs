using JobTracker.Data;

namespace JobTracker.Tests.Data;

public class AvatarCatalogTests
{
    [Fact]
    public void Contains_KnowsItsKeys()
    {
        Assert.Equal("sunrise", AvatarCatalog.Default.Key);
        Assert.True(AvatarCatalog.Contains("ocean"));
        Assert.False(AvatarCatalog.Contains("Ocean"));
        Assert.False(AvatarCatalog.Contains(null));
    }
}
