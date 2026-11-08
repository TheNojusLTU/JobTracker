using System.Net;
using System.Net.Http.Json;
using System.Text;
using JobTracker.Api;
using JobTracker.Auth;
using JobTracker.Data;
using JobTracker.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobTracker.Tests.Integration;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JOBTRACKER_TEST_POSTGRES")))
        {
            Skip = "Set JOBTRACKER_TEST_POSTGRES to a disposable PostgreSQL database to run this test.";
        }
    }
}

public class PostgresImportTests
{
    private const string Header = "CompanyName,JobTitle,Location,Salary,JobUrl,ApplicationDate,Status,Notes";
    private const string ValidCsv = Header + "\nAcme,Dev,,,,2026-10-01,,";

    [PostgresFact]
    public async Task Imports_AcrossInstances_RejectOverlap_AndReleaseLocks()
    {
        var connectionString = Environment.GetEnvironmentVariable("JOBTRACKER_TEST_POSTGRES")!;
        await using var first = new PostgresFactory(connectionString);
        await using var second = new PostgresFactory(connectionString);
        using (var setup = first.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        }

        var (client, _) = await first.CreateUserClientAsync();
        var (otherClient, _) = await first.CreateUserClientAsync();
        var user = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", TestClientExtensions.Json);
        var userId = user!.Id;

        using var importScope = second.Services.CreateScope();
        var importer = importScope.ServiceProvider.GetRequiredService<CsvApplicationImportService>();
        await using var stream = new PausedStream(ValidCsv);
        using var cancellation = new CancellationTokenSource();
        var runningImport = importer.ImportAsync(stream, userId, cancellation.Token);

        try
        {
            await stream.Started.WaitAsync(TimeSpan.FromSeconds(10));

            var conflict = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(ValidCsv));
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Equal("A CSV import is already in progress.", await conflict.ReadTitleAsync());

            var list = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
            Assert.Equal(0, list!.TotalCount);

            var other = await otherClient.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(ValidCsv));
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        }
        finally
        {
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runningImport);

        var invalid = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(Header));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var retry = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(ValidCsv));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var afterRetry = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
        Assert.Equal(1, afterRetry!.TotalCount);

        using var successScope = second.Services.CreateScope();
        var successImporter = successScope.ServiceProvider.GetRequiredService<CsvApplicationImportService>();
        await using var successStream = new PausedStream(ValidCsv);
        var successfulImport = successImporter.ImportAsync(successStream, userId, CancellationToken.None);
        try
        {
            await successStream.Started.WaitAsync(TimeSpan.FromSeconds(10));
            var conflict = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(ValidCsv));
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }
        finally
        {
            successStream.Release();
        }

        Assert.Equal(1, (await successfulImport.WaitAsync(TimeSpan.FromSeconds(10))).ImportedCount);
        using (var afterSuccessScope = first.Services.CreateScope())
        {
            var afterSuccessGuard = afterSuccessScope.ServiceProvider.GetRequiredService<IImportConcurrencyGuard>();
            await using var afterSuccessLease = await afterSuccessGuard.TryAcquireAsync(userId, CancellationToken.None);
            Assert.NotNull(afterSuccessLease);
        }

        using (var errorScope = second.Services.CreateScope())
        {
            var errorImporter = errorScope.ServiceProvider.GetRequiredService<CsvApplicationImportService>();
            await using var errorStream = new FailingStream();
            await Assert.ThrowsAsync<IOException>(
                () => errorImporter.ImportAsync(errorStream, userId, CancellationToken.None));
        }
        using (var afterErrorScope = first.Services.CreateScope())
        {
            var afterErrorGuard = afterErrorScope.ServiceProvider.GetRequiredService<IImportConcurrencyGuard>();
            await using var afterErrorLease = await afterErrorGuard.TryAcquireAsync(userId, CancellationToken.None);
            Assert.NotNull(afterErrorLease);
        }

        using var failingScope = second.Services.CreateScope();
        var failingImporter = failingScope.ServiceProvider.GetRequiredService<CsvApplicationImportService>();
        var missingUserId = Guid.NewGuid().ToString();
        await using var validStream = new MemoryStream(Encoding.UTF8.GetBytes(ValidCsv));
        await Assert.ThrowsAsync<DbUpdateException>(
            () => failingImporter.ImportAsync(validStream, missingUserId, CancellationToken.None));

        using var verifyScope = first.Services.CreateScope();
        var guard = verifyScope.ServiceProvider.GetRequiredService<IImportConcurrencyGuard>();
        await using var lease = await guard.TryAcquireAsync(missingUserId, CancellationToken.None);
        Assert.NotNull(lease);
    }

    private static StringContent Csv(string content) => new(content, Encoding.UTF8, "text/csv");

    private sealed class PostgresFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
            });
        }
    }

    private sealed class PausedStream(string content) : MemoryStream(Encoding.UTF8.GetBytes(content))
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        public void Release() => release.TrySetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Simulated request stream failure."));
    }
}
