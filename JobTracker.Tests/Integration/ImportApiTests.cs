using System.Net;
using System.Net.Http.Json;
using System.Text;
using JobTracker.Api;
using JobTracker.Auth;
using JobTracker.Models;
using JobTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Tests.Integration;

public class ImportApiTests(JobTrackerFactory factory) : IClassFixture<JobTrackerFactory>
{
    private const string Header = "CompanyName,JobTitle,Location,Salary,JobUrl,ApplicationDate,Status,Notes";

    private static StringContent Csv(string text) => new(text, Encoding.UTF8, "text/csv");

    private async Task<HttpResponseMessage> ImportAsync(string csv)
    {
        var (client, _) = await factory.CreateUserClientAsync();
        return await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));
    }

    [Fact]
    public async Task Import_WithValidRows_SavesThem()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var csv = $"""
            {Header}
            Acme,Backend Developer,Vilnius,2500.50,https://acme.lt,2026-10-01,Applied,"first, with comma"
            Globex,QA Engineer,,,,2026-10-02,,
            """;

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.ReadAsAsync<ApplicationImportResponse>()).ImportedCount);
        var list = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
        Assert.Equal(2, list!.TotalCount);
        var acme = list.Items.Single(application => application.CompanyName == "Acme");
        Assert.Equal(2500.50m, acme.Salary);
        Assert.Equal(ApplicationStatus.Applied, acme.Status);
        Assert.Equal("first, with comma", acme.Notes);
        var globex = list.Items.Single(application => application.CompanyName == "Globex");
        Assert.Equal(ApplicationStatus.Saved, globex.Status);
        Assert.Null(globex.Location);
    }

    [Fact]
    public async Task Import_WithWrongContentType_Returns415()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import",
            new StringContent(Header, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Import_RequiresAuthenticationAndCsrf()
    {
        var anonymous = factory.CreateClient();
        var (authenticated, _) = await factory.CreateUserClientAsync();

        var unauthorized = await anonymous.PostAsync("/api/applications/import", Csv(Header));
        var missingCsrf = await authenticated.PostAsync("/api/applications/import", Csv(Header));

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.Equal("Invalid CSRF token.", await missingCsrf.ReadTitleAsync());
    }

    [Fact]
    public async Task Import_LargerThanOneMegabyte_Returns413()
    {
        var response = await ImportAsync(Header + "\n" + new string('x', 1_048_577));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData("Company,Title\nAcme,Dev", "header")]
    [InlineData(Header, "file")]
    [InlineData(Header + "\nAcme,Dev,Vilnius,lots,,2026-10-01,,", "row 2")]
    [InlineData(Header + "\nAcme,Dev,Vilnius,,,01/10/2026,,", "row 2")]
    [InlineData(Header + "\nAcme,Dev,Vilnius,,,2026-10-01,Hired,", "row 2")]
    [InlineData(Header + "\nAcme,Dev,Vilnius,,not-a-url,2026-10-01,,", "row 2")]
    [InlineData(Header + "\nAcme,Dev,Vilnius,,,2026-10-01,,\nonly,three,columns", "row 3")]
    [InlineData(Header + "\nAcme,\"Dev,Vilnius,,,2026-10-01,,", "line 2")]
    public async Task Import_WithInvalidCsv_Returns400WithErrorLocation(string csv, string expectedKey)
    {
        var response = await ImportAsync(csv);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedKey, (await response.ReadErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Import_WithTooManyRows_Returns400()
    {
        var rows = Enumerable.Range(1, 501).Select(row => $"Company {row},Dev,,,,2026-10-01,,");
        var csv = Header + "\n" + string.Join("\n", rows);

        var response = await ImportAsync(csv);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["file"], (await response.ReadErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Import_WithOneBadRow_SavesNothing()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var csv = Header + "\nAcme,Dev,,,,2026-10-01,,\n,Missing company,,,,2026-10-01,,";

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var list = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
        Assert.Equal(0, list!.TotalCount);
    }

    [Fact]
    public async Task Import_WhenSameUserHasActiveImport_Returns409_WhileOtherUsersCanImport()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var (otherClient, _) = await factory.CreateUserClientAsync();
        var user = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", TestClientExtensions.Json);
        var guard = factory.Services.GetRequiredService<IImportConcurrencyGuard>();
        var csv = Header + "\nAcme,Dev,,,,2026-10-01,,";

        await using (var lease = await guard.TryAcquireAsync(user!.Id, CancellationToken.None))
        {
            Assert.NotNull(lease);
            var conflict = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Equal("A CSV import is already in progress.", await conflict.ReadTitleAsync());

            var list = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
            Assert.Equal(0, list!.TotalCount);
            var otherResponse = await otherClient.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));
            Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
        }

        var retry = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(csv));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var afterRetry = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);
        Assert.Equal(1, afterRetry!.TotalCount);
    }

    [Fact]
    public async Task Import_AfterInvalidCsv_CanRetry()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var invalid = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import", Csv(Header));
        var retry = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications/import",
            Csv(Header + "\nAcme,Dev,,,,2026-10-01,,"));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }
}
