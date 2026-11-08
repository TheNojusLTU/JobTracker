using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobTracker.Auth;

namespace JobTracker.Tests.Integration;

public static class TestClientExtensions
{
    public const string Password = "Secret123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<(HttpClient Client, string Username)> CreateUserClientAsync(this JobTrackerFactory factory)
    {
        var client = factory.CreateClient();
        var username = "user" + Guid.NewGuid().ToString("N")[..8];
        var response = await client.SendWithCsrfAsync(HttpMethod.Post, "/api/auth/register",
            new { username, displayName = "Test User", password = Password });
        response.EnsureSuccessStatusCode();
        return (client, username);
    }

    public static async Task<HttpResponseMessage> SendWithCsrfAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null)
    {
        // the token is tied to the signed-in user, so get a fresh one for every write
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf", Json);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(csrf!.HeaderName, csrf.RequestToken);
        request.Content = body switch
        {
            null => null,
            HttpContent content => content,
            _ => JsonContent.Create(body, options: Json)
        };
        return await client.SendAsync(request);
    }

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value!;
    }

    public static async Task<Dictionary<string, string[]>> ReadErrorsAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(Json)!;
    }

    public static async Task<string?> ReadTitleAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
    }
}
