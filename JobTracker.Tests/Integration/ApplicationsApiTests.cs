using System.Net;
using System.Net.Http.Json;
using System.Text;
using JobTracker.Api;
using JobTracker.Models;

namespace JobTracker.Tests.Integration;

public class ApplicationsApiTests(JobTrackerFactory factory) : IClassFixture<JobTrackerFactory>
{
    private static object NewApplication(
        string company = "Acme",
        string title = "Backend Developer",
        string? location = "Vilnius",
        string date = "2026-10-01",
        ApplicationStatus? status = null) =>
        new { companyName = company, jobTitle = title, location, salary = 2500m, jobUrl = "https://acme.lt/jobs/1", applicationDate = date, status, notes = "  " };

    private static async Task<ApplicationResponse> CreateAsync(HttpClient client, object body)
    {
        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<ApplicationResponse>();
    }

    [Fact]
    public async Task List_WhenAnonymous_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/applications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidBody_ReturnsCreatedApplication()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications",
            NewApplication(company: "  Acme  ", date: "2026-10-01T15:30:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<ApplicationResponse>();
        Assert.Equal($"/api/applications/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal("Acme", created.CompanyName);
        Assert.Equal(ApplicationStatus.Saved, created.Status);
        Assert.Equal(new DateTime(2026, 10, 1), created.ApplicationDate.Date);
        Assert.Null(created.Notes);

        var loaded = await client.GetFromJsonAsync<ApplicationResponse>($"/api/applications/{created.Id}", TestClientExtensions.Json);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal(created.CompanyName, loaded.CompanyName);
    }

    [Fact]
    public async Task Create_WithInvalidBody_ReturnsEveryError()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications",
            new { companyName = " ", jobTitle = "", salary = -1, jobUrl = "ftp://acme.lt", location = new string('x', 201), notes = new string('x', 5001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Equal(
            ["CompanyName", "JobTitle", "Location", "Salary", "JobUrl", "ApplicationDate", "Notes"],
            errors.Keys);
    }

    [Fact]
    public async Task Create_WithBrokenJson_Returns400()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/applications",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Writes_WithoutCsrfToken_Return400()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, NewApplication());

        var post = await client.PostAsJsonAsync("/api/applications", NewApplication(), TestClientExtensions.Json);
        var put = await client.PutAsJsonAsync($"/api/applications/{created.Id}", NewApplication(status: ApplicationStatus.Applied), TestClientExtensions.Json);
        var delete = await client.DeleteAsync($"/api/applications/{created.Id}");

        Assert.All([post, put, delete], response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        Assert.Equal("Invalid CSRF token.", await delete.ReadTitleAsync());
    }

    [Fact]
    public async Task Update_WithValidBody_ChangesApplication()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, NewApplication());

        var response = await client.SendWithCsrfAsync(HttpMethod.Put, $"/api/applications/{created.Id}",
            NewApplication(company: "Globex", location: null, status: ApplicationStatus.Interview));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsAsync<ApplicationResponse>();
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Globex", updated.CompanyName);
        Assert.Null(updated.Location);
        Assert.Equal(ApplicationStatus.Interview, updated.Status);
    }

    [Fact]
    public async Task Update_WithoutStatus_Returns400()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, NewApplication(status: ApplicationStatus.Offer));

        var response = await client.SendWithCsrfAsync(HttpMethod.Put, $"/api/applications/{created.Id}", NewApplication());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["Status"], (await response.ReadErrorsAsync()).Keys);
    }

    [Fact]
    public async Task Delete_RemovesApplication()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(client, NewApplication());

        var response = await client.SendWithCsrfAsync(HttpMethod.Delete, $"/api/applications/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/applications/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task MissingApplication_Returns404()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var put = await client.SendWithCsrfAsync(HttpMethod.Put, "/api/applications/999999", NewApplication(status: ApplicationStatus.Applied));
        var delete = await client.SendWithCsrfAsync(HttpMethod.Delete, "/api/applications/999999");

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task OtherUsersApplication_Returns404AndStaysUnchanged()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (stranger, _) = await factory.CreateUserClientAsync();
        var created = await CreateAsync(owner, NewApplication());

        var get = await stranger.GetAsync($"/api/applications/{created.Id}");
        var put = await stranger.SendWithCsrfAsync(HttpMethod.Put, $"/api/applications/{created.Id}", NewApplication(company: "Hacked", status: ApplicationStatus.Rejected));
        var delete = await stranger.SendWithCsrfAsync(HttpMethod.Delete, $"/api/applications/{created.Id}");
        var list = await stranger.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications", TestClientExtensions.Json);

        Assert.All([get, put, delete], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Empty(list!.Items);
        var stillThere = await owner.GetFromJsonAsync<ApplicationResponse>($"/api/applications/{created.Id}", TestClientExtensions.Json);
        Assert.Equal("Acme", stillThere!.CompanyName);
    }

    [Fact]
    public async Task List_AppliesSearchFiltersAndSorting()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        await CreateAsync(client, NewApplication(company: "Acme", title: "Backend Developer", location: "Vilnius", date: "2026-09-01", status: ApplicationStatus.Applied));
        await CreateAsync(client, NewApplication(company: "Globex", title: "Frontend Developer", location: "Kaunas", date: "2026-10-01", status: ApplicationStatus.Interview));
        await CreateAsync(client, NewApplication(company: "Initech", title: "QA Engineer", location: "Vilnius", date: "2026-11-01", status: ApplicationStatus.Applied));

        async Task<string[]> Companies(string query)
        {
            var page = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications" + query, TestClientExtensions.Json);
            return page!.Items.Select(application => application.CompanyName).ToArray();
        }

        Assert.Equal(["Initech", "Globex", "Acme"], await Companies(""));
        Assert.Equal(["Acme", "Globex", "Initech"], await Companies("?newestFirst=false"));
        Assert.Equal(["Globex", "Acme"], await Companies("?search=DEVELOPER"));
        Assert.Equal(["Initech", "Acme"], await Companies("?status=Applied"));
        Assert.Equal(["Initech", "Acme"], await Companies("?location=vilnius"));
        Assert.Equal(["Globex"], await Companies("?from=2026-09-15&to=2026-10-01"));
        Assert.Equal(["Acme"], await Companies("?search=acme&status=Applied&location=Vil&to=2026-10-31"));
    }

    [Fact]
    public async Task List_ReturnsRequestedPage()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        for (var day = 1; day <= 5; day++)
        {
            await CreateAsync(client, NewApplication(company: $"Company {day}", date: $"2026-10-0{day}"));
        }

        var page = await client.GetFromJsonAsync<PagedResponse<ApplicationResponse>>("/api/applications?page=3&pageSize=2", TestClientExtensions.Json);

        Assert.Equal(3, page!.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("Company 1", Assert.Single(page.Items).CompanyName);
    }

    [Theory]
    [InlineData("?page=0&pageSize=101&from=2026-10-02&to=2026-10-01", new[] { "page", "pageSize", "from" })]
    [InlineData("?page=2147483647&pageSize=100", new[] { "page" })]
    public async Task List_WithInvalidQuery_Returns400(string query, string[] expectedErrors)
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.GetAsync("/api/applications" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedErrors, (await response.ReadErrorsAsync()).Keys);
    }
}
