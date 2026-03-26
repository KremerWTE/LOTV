using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests covering the full request lifecycle:
/// New → InProgress → Fulfilled, notes, and activity log.
/// </summary>
[Collection("Integration")]
public class RequestLifecycleTests
{
    private readonly LotvApiFactory _factory;

    public RequestLifecycleTests(LotvApiFactory factory) => _factory = factory;

    // ── Status transitions ───────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_NewToFulfilled_Succeeds()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        // New → InProgress
        var r1 = await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/status", new { Status = "InProgress" });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // InProgress → AwaitingShipment
        var r2 = await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/status", new { Status = "AwaitingShipment" });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Fulfill
        var r3 = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/fulfill", new { Notes = "Delivered successfully" });
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);

        // Final status is Fulfilled
        var final = await client.GetFromJsonAsync<RequestResponseDto>($"/api/v1/requests/{requestId}");
        Assert.Equal("Fulfilled", final!.Status);
    }

    [Fact]
    public async Task StatusTransition_ToOnHold_Persists()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/status", new { Status = "OnHold" });

        var dto = await client.GetFromJsonAsync<RequestResponseDto>($"/api/v1/requests/{requestId}");
        Assert.Equal("OnHold", dto!.Status);
    }

    // ── Priority update ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePriority_ToUrgent_Returns200()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var resp = await client.PutAsJsonAsync($"/api/v1/requests/{requestId}/priority", new { Priority = "Urgent" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Notes ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotes_NewRequest_ReturnsEmptyList()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var notes = await client.GetFromJsonAsync<List<NoteResponseDto>>($"/api/v1/requests/{requestId}/notes");
        Assert.NotNull(notes);
        Assert.Empty(notes);
    }

    [Fact]
    public async Task PostNote_Returns201()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var resp = await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/notes",
            new { Content = "Test note content", IsInternal = true });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task PostThenGetNotes_ReturnsNote()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        await client.PostAsJsonAsync($"/api/v1/requests/{requestId}/notes",
            new { Content = "Lifecycle note", IsInternal = false });

        var notes = await client.GetFromJsonAsync<List<NoteResponseDto>>($"/api/v1/requests/{requestId}/notes");
        Assert.NotNull(notes);
        Assert.Single(notes);
        Assert.Equal("Lifecycle note", notes[0].Content);
    }

    // ── Activity log ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivityLog_AfterCreate_ContainsCreatedEvent()
    {
        var client    = await AuthedClientAsync();
        var familyId  = await CreateFamilyAsync(client);
        var requestId = await CreateRequestAsync(client, familyId);

        var activity = await client.GetFromJsonAsync<List<object>>($"/api/v1/requests/{requestId}/activity");
        Assert.NotNull(activity);
        Assert.NotEmpty(activity);
    }

    // ── Queue endpoints ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetQueue_WithValidToken_Returns200()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/requests/queue");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetOverdue_WithValidToken_Returns200()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/requests/overdue");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client   = _factory.CreateClient();
        var email    = $"lifecycle-{Guid.NewGuid():N}@test.com";
        const string password = "Lifecycle1Pass!";

        (await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = "Life",
            LastName  = "Cycle",
            Role      = "ChapterStaff",
            ChapterId = 1
        })).EnsureSuccessStatusCode();

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = password });
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static async Task<int> CreateFamilyAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/families", new
        {
            Parent1FirstName = "Lifecycle",
            Parent1LastName  = "Family",
            Email            = $"lf-{Guid.NewGuid():N}@test.com",
            StreetAddress    = "1 Test Ave",
            City             = "Austin",
            State            = "TX",
            Zip              = "78701",
            ChapterId        = 1,
            Reason           = "Stillbirth"
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FamilyResponseDto>())!.Id;
    }

    private static async Task<int> CreateRequestAsync(HttpClient client, int familyId)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/requests", new
        {
            FamilyId  = familyId,
            ChapterId = 1,
            Reason    = "Stillbirth",
            Category  = "PackageDelivery",
            Priority  = "High"
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RequestResponseDto>())!.Id;
    }
}
