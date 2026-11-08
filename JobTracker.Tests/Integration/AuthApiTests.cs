using System.Net;
using System.Net.Http.Json;
using JobTracker.Auth;

namespace JobTracker.Tests.Integration;

public class AuthApiTests(JobTrackerFactory factory) : IClassFixture<JobTrackerFactory>
{
    [Fact]
    public async Task Me_WhenAnonymous_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutCsrfToken_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { username = "nocsrf", displayName = "No Csrf", password = TestClientExtensions.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid CSRF token.", await response.ReadTitleAsync());
    }

    [Fact]
    public async Task Register_WithInvalidFields_ReturnsEveryError()
    {
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/register",
            new { username = "ab", displayName = "x", password = "", email = "not-an-email", avatarKey = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Equal(["username", "displayName", "email", "password", "avatarKey"], errors.Keys);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsPasswordErrors()
    {
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/register",
            new { username = "weakuser", displayName = "Weak User", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.ReadErrorsAsync();
        Assert.Equal(["password"], errors.Keys);
        Assert.True(errors["password"].Length >= 2);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesUserAndSignsIn()
    {
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/register",
            new { username = " newuser ", displayName = "New User", password = TestClientExtensions.Password, email = "new@user.lt" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/auth/me", response.Headers.Location?.ToString());
        var user = await response.ReadAsAsync<UserResponse>();
        Assert.Equal("newuser", user.Username);
        Assert.Equal("new@user.lt", user.Email);
        Assert.Equal("sunrise", user.AvatarKey);

        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me", TestClientExtensions.Json);
        Assert.Equal(user, me);
    }

    [Fact]
    public async Task Register_WithTakenUsername_Returns409EvenWithDifferentCase()
    {
        var (_, username) = await factory.CreateUserClientAsync();
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/register",
            new { username = username.ToUpperInvariant(), displayName = "Copy Cat", password = TestClientExtensions.Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Username is already taken.", await response.ReadTitleAsync());
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/login",
            new { username = " ", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["username", "password"], (await response.ReadErrorsAsync()).Keys);
    }

    [Theory]
    [InlineData("Wrong123")]
    [InlineData(null)]
    public async Task Login_WithWrongPasswordOrUnknownUser_Returns401(string? password)
    {
        var (_, username) = await factory.CreateUserClientAsync();
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/login",
            password is null
                ? new { username = "ghost" + username, password = TestClientExtensions.Password }
                : new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid username or password.", await response.ReadTitleAsync());
    }

    [Fact]
    public async Task Login_WithValidCredentials_SignsIn()
    {
        var (_, username) = await factory.CreateUserClientAsync();
        var client = factory.CreateClient();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/login",
            new { username = $"  {username} ", password = TestClientExtensions.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(username, (await response.ReadAsAsync<UserResponse>()).Username);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_SignsOut()
    {
        var (client, _) = await factory.CreateUserClientAsync();

        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
