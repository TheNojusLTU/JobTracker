using JobTracker.Services;

namespace JobTracker.Tests.Services;

public class ActiveImportsTests
{
    [Fact]
    public async Task TryStart_InParallelForSameUser_LetsOnlyOneIn()
    {
        var activeImports = new ActiveImports();
        using var start = new ManualResetEventSlim();

        var attempts = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return activeImports.TryStart("user-1");
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, started => started);
    }

    [Fact]
    public void Finish_LetsTheSameUserStartAgain()
    {
        var activeImports = new ActiveImports();

        Assert.True(activeImports.TryStart("user-1"));
        Assert.False(activeImports.TryStart("user-1"));
        activeImports.Finish("user-1");

        Assert.True(activeImports.TryStart("user-1"));
    }

    [Fact]
    public void TryStart_DifferentUsers_DoNotBlockEachOther()
    {
        var activeImports = new ActiveImports();

        Assert.True(activeImports.TryStart("user-1"));
        Assert.True(activeImports.TryStart("user-2"));
    }
}
