using System.Net;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests for the /api/v1/auth/* endpoints.
/// Uses a real ASP.NET Core test host + isolated SQLite database.
/// </summary>
[Collection("Integration")]
public class AuthApiTests
{
    private readonly HttpClient _client;

    public AuthApiTests(LotvApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = "nobody@test.com", Password = "DoesNotExist1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Register first so the user exists
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "wrongpwd@test.com",
            Password  = "Correct1Pass!",
            FirstName = "Test",
            LastName  = "User",
            Role      = "Volunteer"
        });

        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = "wrongpwd@test.com", Password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_Returns200OrCreated()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = "newuser@lotvtest.com",
            Password  = "ValidPass1234!",
            FirstName = "New",
            LastName  = "User",
            Role      = "Volunteer"
        });

        Assert.True(
            resp.StatusCode == HttpStatusCode.OK ||
            resp.StatusCode == HttpStatusCode.Created,
            $"Expected 200 or 201 but got {(int)resp.StatusCode}");
    }

    [Fact]
    public async Task Register_ThenLogin_ReturnsAccessToken()
    {
        var email = $"roundtrip-{Guid.NewGuid():N}@test.com";

        var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = "RoundTrip1Pass!",
            FirstName = "Round",
            LastName  = "Trip",
            Role      = "Volunteer"
        });
        regResp.EnsureSuccessStatusCode();

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = email, Password = "RoundTrip1Pass!" });

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.AccessToken), "AccessToken should not be empty");
        Assert.False(string.IsNullOrEmpty(body.RefreshToken), "RefreshToken should not be empty");
    }
}
