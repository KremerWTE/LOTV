using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests for the /api/v1/requests and related protected endpoints.
/// Verifies authentication enforcement and basic CRUD behavior.
/// </summary>
[Collection("Integration")]
public class RequestsApiTests
{
    private readonly LotvApiFactory _factory;
    private readonly HttpClient _client;

    public RequestsApiTests(LotvApiFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequests_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/v1/requests");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetFamilies_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/v1/families");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetDashboardStats_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/v1/dashboard/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetVolunteers_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/v1/volunteers");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Authenticated access ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRequests_WithValidToken_Returns200()
    {
        var token = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/v1/requests");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetRequests_WithValidToken_ReturnsJsonArray()
    {
        var token = await RegisterAndLoginAsync();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var items = await authedClient.GetFromJsonAsync<List<object>>("/api/v1/requests");

        Assert.NotNull(items);
    }

    [Fact]
    public async Task GetFamilies_WithValidToken_Returns200()
    {
        var token = await RegisterAndLoginAsync();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await authedClient.GetAsync("/api/v1/families");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndLoginAsync()
    {
        var email    = $"integ-{Guid.NewGuid():N}@test.com";
        const string password = "Integration1Pass!";

        var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = "Integration",
            LastName  = "Tester",
            Role      = "ChapterStaff"
        });
        regResp.EnsureSuccessStatusCode();

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = email, Password = password });
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        return body!.AccessToken;
    }
}
