namespace JobTracker.Tests.TestSupport;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JOBTRACKER_TEST_POSTGRES")) &&
            Environment.GetEnvironmentVariable("JOBTRACKER_REQUIRE_POSTGRES_TESTS") != "true")
        {
            Skip = "Set JOBTRACKER_TEST_POSTGRES to a disposable PostgreSQL database to run this test.";
        }
    }
}
