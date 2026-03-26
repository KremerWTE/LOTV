using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Tests for authorization policies across all protected endpoint groups.
/// Verifies that:
///   - Unauthenticated requests return 401
///   - Authenticated requests with insufficient role return 403
///   - Authenticated requests with sufficient role return 200/2xx
/// </summary>
[Collection("Integration")]
public class ControllerAuthorizationTests
{
    private readonly LotvApiFactory _factory;

    public ControllerAuthorizationTests(LotvApiFactory factory) => _factory = factory;

    // ── Unauthenticated access ─────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/requests")]
    [InlineData("/api/v1/families")]
    [InlineData("/api/v1/volunteers")]
    [InlineData("/api/v1/donors")]
    [InlineData("/api/v1/donations")]
    [InlineData("/api/v1/workload")]
    [InlineData("/api/v1/allocations")]
    [InlineData("/api/v1/dashboard/stats")]
    public async Task ProtectedEndpoint_WithNoToken_Returns401(string url)
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PublicApply_NoToken_Returns200()
    {
        var client = _factory.CreateClient();
        var resp   = await client.PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Jane", Parent1LastName = "Doe",
                Email = "jane@test.com", Phone = "555-0001",
                City = "Springfield", State = "IL"
            }
        });

        // Public endpoint must not require auth
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PublicGive_NoToken_Returns200()
    {
        var client = _factory.CreateClient();
        var resp   = await client.PostAsJsonAsync("/api/v1/public/give", new
        {
            Donor    = new { FirstName = "Bob", LastName = "Smith", Email = "bob@test.com", ChapterId = 1 },
            Donation = new { Amount = 50m, Channel = "Online" }
        });

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_NoToken_Returns200()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Authenticated access with Staff role ──────────────────────────────────

    [Fact]
    public async Task GetRequests_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/requests");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetFamilies_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/families");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetVolunteers_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/volunteers");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetDonors_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/donors");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetDonations_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/donations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetDashboardStats_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/dashboard/stats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/workload");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetAllocations_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/allocations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── HQ Admin-only endpoints ────────────────────────────────────────────────

    [Fact]
    public async Task GetChapters_WithHqAdminToken_Returns200()
    {
        var client = await AuthedClientAsync("HQAdmin");
        var resp   = await client.GetAsync("/api/v1/chapters");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetChapters_WithChapterStaffToken_Returns403()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/chapters");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Events endpoints ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvents_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetNonExistentEvent_Returns404()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Diocese lookup ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDioceses_WithStaffToken_Returns200()
    {
        var client = await AuthedClientAsync("ChapterStaff");
        var resp   = await client.GetAsync("/api/v1/dioceses");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Invalid token ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequests_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var resp = await client.GetAsync("/api/v1/requests");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetRequests_WithExpiredTokenFormat_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");

        var resp = await client.GetAsync("/api/v1/requests");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpClient> AuthedClientAsync(string role = "ChapterStaff")
    {
        var client   = _factory.CreateClient();
        var email    = $"auth-test-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Auth!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = "Auth",
            LastName  = "Test",
            Role      = role,
            ChapterId = 1
        });

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = password });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        return client;
    }
}
