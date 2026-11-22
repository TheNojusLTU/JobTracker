using System.Net;

namespace JobTracker.Tests;

public class ProgramTests(JobTrackerFactory factory) : IClassFixture<JobTrackerFactory>
{
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
