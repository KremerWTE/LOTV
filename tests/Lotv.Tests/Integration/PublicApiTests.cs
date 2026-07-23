using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests for the anonymous public API (/api/public/v1/) and
/// the authenticated dashboard reporting endpoints (/api/v1/dashboard/).
///
/// Public endpoints must return 2xx with no auth token.
/// Dashboard endpoints must return 200 for authenticated Staff.
/// Donor-impact and family-request endpoints are data-driven end-to-end.
/// </summary>
[Collection("Integration")]
public class PublicApiTests
{
    private readonly LotvApiFactory _factory;

    public PublicApiTests(LotvApiFactory factory) => _factory = factory;

    // ── Anonymous public endpoints — no token needed ──────────────────────────

    [Theory]
    [InlineData("/api/public/v1/impact")]
    [InlineData("/api/public/v1/transparency/money")]
    [InlineData("/api/public/v1/transparency/timeline")]
    [InlineData("/api/public/v1/events")]
    [InlineData("/api/public/v1/chapters")]
    [InlineData("/api/public/v1/wishlist")]
    public async Task AnonymousPublicEndpoint_NoToken_Returns200(string url)
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PublicImpact_Returns_ExpectedShape()
    {
        var client = _factory.CreateClient();
        var json   = await client.GetStringAsync("/api/public/v1/impact");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify top-level numeric fields are present
        Assert.True(root.TryGetProperty("totalDonations",   out _), "missing totalDonations");
        Assert.True(root.TryGetProperty("peopleHelped",     out _), "missing peopleHelped");
        Assert.True(root.TryGetProperty("activeVolunteers", out _), "missing activeVolunteers");
        Assert.True(root.TryGetProperty("openRequests",     out _), "missing openRequests");
        Assert.True(root.TryGetProperty("familiesServed",   out _), "missing familiesServed");
        Assert.True(root.TryGetProperty("diocesesReached",  out _), "missing diocesesReached");
    }

    [Fact]
    public async Task PublicTransparencyMoney_Returns_Array()
    {
        var client = _factory.CreateClient();
        var json   = await client.GetStringAsync("/api/public/v1/transparency/money");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task PublicTransparencyTimeline_Returns_Array()
    {
        var client = _factory.CreateClient();
        var json   = await client.GetStringAsync("/api/public/v1/transparency/timeline");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task PublicEvents_Returns_Array()
    {
        var client = _factory.CreateClient();
        var json   = await client.GetStringAsync("/api/public/v1/events");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task PublicWishList_Returns_Array()
    {
        var client = _factory.CreateClient();
        var json   = await client.GetStringAsync("/api/public/v1/wishlist");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ── Donor impact — anonymous, data-driven ─────────────────────────────────

    [Fact]
    public async Task DonorImpact_UnknownDonor_Returns404()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/public/v1/donors/999999/impact");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DonorImpact_DonorWithDonation_Returns200_WithCorrectTotals()
    {
        var authed = await AuthedClientAsync();

        // Create a donor
        var donorResp = await authed.PostAsJsonAsync("/api/v1/donors", new
        {
            FirstName = "Impact",
            LastName  = $"Donor-{Guid.NewGuid():N}",
            Email     = $"impact-{Guid.NewGuid():N}@test.com",
            ChapterId = 1
        });
        donorResp.EnsureSuccessStatusCode();
        var donor = await donorResp.Content.ReadFromJsonAsync<DonorResponseDto>();
        Assert.NotNull(donor);

        // Record two donations
        await authed.PostAsJsonAsync("/api/v1/donations", new
        {
            DonorId            = donor.Id,
            Amount             = 150.00m,
            Date               = DateTime.UtcNow.ToString("O"),
            Channel            = "Online",
            ChapterId          = 1,
            ContributionStatus = "Processed",
            AllocationStatus   = "Unallocated"
        });
        await authed.PostAsJsonAsync("/api/v1/donations", new
        {
            DonorId            = donor.Id,
            Amount             = 75.00m,
            Date               = DateTime.UtcNow.ToString("O"),
            Channel            = "Check",
            ChapterId          = 1,
            ContributionStatus = "Processed",
            AllocationStatus   = "Unallocated"
        });

        // Anonymous client fetches impact
        var anon = _factory.CreateClient();
        var json = await anon.GetStringAsync($"/api/public/v1/donors/{donor.Id}/impact");
        using var doc  = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalGiven",     out var totalGivenEl), "missing totalGiven");
        Assert.True(root.TryGetProperty("giftCount",      out var giftCountEl),  "missing giftCount");
        Assert.True(root.TryGetProperty("donationHistory",out _),                "missing donationHistory");
        Assert.True(root.TryGetProperty("categoryBreakdown", out _),             "missing categoryBreakdown");

        Assert.Equal(225.00m, totalGivenEl.GetDecimal());
        Assert.Equal(2,       giftCountEl.GetInt32());
    }

    [Fact]
    public async Task DonorImpact_CategoryBreakdown_IsNonEmpty()
    {
        var authed = await AuthedClientAsync();

        var donorResp = await authed.PostAsJsonAsync("/api/v1/donors", new
        {
            FirstName = "Cat",
            LastName  = $"Breakdown-{Guid.NewGuid():N}",
            Email     = $"cat-{Guid.NewGuid():N}@test.com",
            ChapterId = 1
        });
        donorResp.EnsureSuccessStatusCode();
        var donor = await donorResp.Content.ReadFromJsonAsync<DonorResponseDto>();
        Assert.NotNull(donor);

        await authed.PostAsJsonAsync("/api/v1/donations", new
        {
            DonorId            = donor.Id,
            Amount             = 500.00m,
            Date               = DateTime.UtcNow.ToString("O"),
            Channel            = "Online",
            ChapterId          = 1,
            ContributionStatus = "Processed",
            AllocationStatus   = "Unallocated"
        });

        var anon = _factory.CreateClient();
        var json = await anon.GetStringAsync($"/api/public/v1/donors/{donor.Id}/impact");
        using var doc  = JsonDocument.Parse(json);
        var breakdown = doc.RootElement.GetProperty("categoryBreakdown");

        Assert.Equal(JsonValueKind.Array, breakdown.ValueKind);
        Assert.True(breakdown.GetArrayLength() > 0, "categoryBreakdown should have at least one entry");
    }

    // ── Family requests — anonymous, data-driven ──────────────────────────────

    [Fact]
    public async Task FamilyRequests_UnknownFamily_Returns404()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/public/v1/families/999999/requests");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FamilyRequests_ExistingFamily_Returns200_Array()
    {
        var authed = await AuthedClientAsync();

        // Create a family
        var famResp = await authed.PostAsJsonAsync("/api/v1/families", new
        {
            Parent1FirstName = "Public",
            Parent1LastName  = $"Fam-{Guid.NewGuid():N}",
            Email            = $"pubfam-{Guid.NewGuid():N}@test.com",
            Phone            = "555-0042",
            City             = "Springfield",
            State            = "IL",
            ChapterId        = 1
        });
        famResp.EnsureSuccessStatusCode();
        var family = await famResp.Content.ReadFromJsonAsync<FamilyResponseDto>();
        Assert.NotNull(family);

        // Anonymous client fetches requests (should be empty array, not 404)
        var anon = _factory.CreateClient();
        var json = await anon.GetStringAsync($"/api/public/v1/families/{family.Id}/requests");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task FamilyRequests_AfterSubmittingRequest_ReturnsItWithStringFields()
    {
        var authed = await AuthedClientAsync();

        // Create a family + one request
        var famResp = await authed.PostAsJsonAsync("/api/v1/families", new
        {
            Parent1FirstName = "Req",
            Parent1LastName  = $"Tester-{Guid.NewGuid():N}",
            Email            = $"reqfam-{Guid.NewGuid():N}@test.com",
            Phone            = "555-0099",
            City             = "Shelbyville",
            State            = "IL",
            ChapterId        = 1
        });
        famResp.EnsureSuccessStatusCode();
        var family = await famResp.Content.ReadFromJsonAsync<FamilyResponseDto>();
        Assert.NotNull(family);

        var reqResp = await authed.PostAsJsonAsync("/api/v1/requests", new
        {
            FamilyId  = family.Id,
            Reason    = "Stillbirth",
            Category  = "PackageDelivery",
            Priority  = "Normal",
            ChapterId = 1
        });
        reqResp.EnsureSuccessStatusCode();

        // Verify the anonymous endpoint returns the request with string enum values
        var anon = _factory.CreateClient();
        var json = await anon.GetStringAsync($"/api/public/v1/families/{family.Id}/requests");
        using var doc  = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.True(arr.GetArrayLength() >= 1);

        var first = arr[0];
        Assert.True(first.TryGetProperty("id",       out _), "missing id");
        Assert.True(first.TryGetProperty("category", out _), "missing category");
        Assert.True(first.TryGetProperty("status",   out var statusEl), "missing status");
        Assert.True(first.TryGetProperty("priority", out _), "missing priority");
        Assert.True(first.TryGetProperty("reason",   out _), "missing reason");

        // Status should be a string, not an integer
        Assert.Equal(JsonValueKind.String, statusEl.ValueKind);
        Assert.Equal("New", statusEl.GetString());
    }

    // ── Dashboard reporting endpoints — require Staff auth ────────────────────

    [Theory]
    [InlineData("/api/v1/dashboard/donations/by-city")]
    [InlineData("/api/v1/dashboard/donations/by-amount")]
    [InlineData("/api/v1/dashboard/donations/by-diocese")]
    [InlineData("/api/v1/dashboard/timeline")]
    [InlineData("/api/v1/dashboard/money")]
    [InlineData("/api/v1/dashboard/resources")]
    public async Task DashboardEndpoint_WithNoToken_Returns401(string url)
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/dashboard/donations/by-city")]
    [InlineData("/api/v1/dashboard/donations/by-amount")]
    [InlineData("/api/v1/dashboard/donations/by-diocese")]
    [InlineData("/api/v1/dashboard/timeline")]
    [InlineData("/api/v1/dashboard/money")]
    [InlineData("/api/v1/dashboard/resources")]
    public async Task DashboardEndpoint_WithStaffToken_Returns200(string url)
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DashboardByAmount_Returns_SixBands()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/donations/by-amount");
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(6, arr.GetArrayLength());

        // Each element must have band, giftCount, totalAmount, percentage
        foreach (var el in arr.EnumerateArray())
        {
            Assert.True(el.TryGetProperty("band",        out _), "missing band");
            Assert.True(el.TryGetProperty("giftCount",   out _), "missing giftCount");
            Assert.True(el.TryGetProperty("totalAmount", out _), "missing totalAmount");
            Assert.True(el.TryGetProperty("percentage",  out _), "missing percentage");
        }
    }

    [Fact]
    public async Task DashboardByCity_Returns_Array()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/donations/by-city");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task DashboardTimeline_DefaultWindow_Returns12Periods()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/timeline");
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(12, arr.GetArrayLength());

        // Each period must have period label, donations, requestsFulfilled, newRequests
        var first = arr[0];
        Assert.True(first.TryGetProperty("period",            out _), "missing period");
        Assert.True(first.TryGetProperty("donations",         out _), "missing donations");
        Assert.True(first.TryGetProperty("requestsFulfilled", out _), "missing requestsFulfilled");
        Assert.True(first.TryGetProperty("newRequests",       out _), "missing newRequests");
    }

    [Fact]
    public async Task DashboardTimeline_CustomWindow_ReturnsMatchingCount()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/timeline?months=6");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(6, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task DashboardResources_Returns_Array()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/resources");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task DashboardMoney_Returns_Array()
    {
        var client = await AuthedClientAsync();
        var json   = await client.GetStringAsync("/api/v1/dashboard/money");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client   = _factory.CreateClient();
        var email    = $"pubapi-{Guid.NewGuid():N}@test.com";
        const string password = "PubApi1Pass!";

        (await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = "PubApi",
            LastName  = "Tester",
            Role      = "ChapterStaff",
            ChapterId = 1
        })).EnsureSuccessStatusCode();

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = email, Password = password });
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
