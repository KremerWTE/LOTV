using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests for request CRUD endpoints and the dashboard stats endpoint.
/// Covers the authenticated happy-path: create family → create request → read → update status.
/// </summary>
[Collection("Integration")]
public class RequestCrudTests
{
    private readonly LotvApiFactory _factory;

    public RequestCrudTests(LotvApiFactory factory) => _factory = factory;

    // ── Dashboard stats ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardStats_WithValidToken_Returns200()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/dashboard/stats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsExpectedShape()
    {
        var client = await AuthedClientAsync();
        var stats  = await client.GetFromJsonAsync<DashboardStatsDto>("/api/v1/dashboard/stats");

        Assert.NotNull(stats);
        Assert.True(stats.OpenCases   >= 0);
        Assert.True(stats.Overdue     >= 0);
        Assert.True(stats.ActiveVolunteers >= 0);
    }

    // ── Request CRUD ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostRequest_WithValidData_Returns201()
    {
        var client   = await AuthedClientAsync();
        var familyId = await CreateFamilyAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            FamilyId  = familyId,
            ChapterId = 1,
            Reason    = "Miscarriage",
            Category  = "Other",
            Priority  = "Normal"
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task GetRequest_ById_Returns200()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var resp = await client.GetAsync($"/api/v1/requests/{requestId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetRequest_ById_ReturnsSavedData()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var dto = await client.GetFromJsonAsync<RequestResponseDto>($"/api/v1/requests/{requestId}");

        Assert.NotNull(dto);
        Assert.Equal(requestId, dto.Id);
        Assert.Equal("New", dto.Status);
    }

    [Fact]
    public async Task UpdateRequestStatus_Returns200()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var resp = await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/status",
            new { Status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateRequestStatus_PersistsChange()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/status", new { Status = "OnHold" });

        var dto = await client.GetFromJsonAsync<RequestResponseDto>($"/api/v1/requests/{requestId}");
        Assert.Equal("OnHold", dto!.Status);
    }

    [Fact]
    public async Task GetRequest_NonExistentId_Returns404()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/requests/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a fresh authenticated HttpClient (unique user per call).</summary>
    private async Task<HttpClient> AuthedClientAsync()
    {
        var client   = _factory.CreateClient();
        var email    = $"crud-{Guid.NewGuid():N}@test.com";
        const string password = "CrudTest1Pass!";

        var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email      = email,
            Password   = password,
            FirstName  = "Crud",
            LastName   = "Tester",
            Role       = "ChapterStaff",
            ChapterId  = 1
        });
        regResp.EnsureSuccessStatusCode();

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = email, Password = password });
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    /// <summary>Seeds a minimal family via the API and returns its ID.</summary>
    private static async Task<int> CreateFamilyAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/families", new
        {
            Parent1FirstName = "Test",
            Parent1LastName  = "Family",
            Email            = $"family-{Guid.NewGuid():N}@test.com",
            StreetAddress    = "123 Test St",
            City             = "Testville",
            State            = "TX",
            Zip              = "78701",
            ChapterId        = 1,
            Reason           = "Miscarriage"
        });
        resp.EnsureSuccessStatusCode();

        var family = await resp.Content.ReadFromJsonAsync<FamilyResponseDto>();
        return family!.Id;
    }

    /// <summary>Creates a request for the given family and returns its ID.</summary>
    private static async Task<int> CreateRequestAsync(HttpClient client, int familyId)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            FamilyId  = familyId,
            ChapterId = 1,
            Reason    = "Miscarriage",
            Category  = "Other",
            Priority  = "Normal"
        });
        resp.EnsureSuccessStatusCode();

        var req = await resp.Content.ReadFromJsonAsync<RequestResponseDto>();
        return req!.Id;
    }
}
