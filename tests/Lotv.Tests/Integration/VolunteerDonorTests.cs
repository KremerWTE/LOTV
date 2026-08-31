using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// Integration tests for volunteer CRUD, donor CRUD, donations,
/// and verifying that donation amounts appear in dashboard stats.
/// </summary>
[Collection("Integration")]
public class VolunteerDonorTests
{
    private readonly LotvApiFactory _factory;

    public VolunteerDonorTests(LotvApiFactory factory) => _factory = factory;

    // ── Volunteers ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PostVolunteer_Returns201()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.PostAsJsonAsync("/api/v1/volunteers", new
        {
            FirstName          = "Volunteer",
            LastName           = $"Test-{Guid.NewGuid():N}",
            Email              = $"vol-{Guid.NewGuid():N}@test.com",
            Role               = "PackageAssembler",
            Status             = "Active",
            ChapterId          = 1,
            JoinedDate         = DateTime.UtcNow.ToString("O"),
            ServiceRadiusMiles = 25.0
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task GetVolunteers_Returns200()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/volunteers");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PostVolunteer_ThenGetAll_IncludesCreatedVolunteer()
    {
        var client    = await AuthedClientAsync();
        var lastName  = $"VTest-{Guid.NewGuid():N}";

        var postResp = await client.PostAsJsonAsync("/api/v1/volunteers", new
        {
            FirstName          = "NewVol",
            LastName           = lastName,
            Email              = $"vol2-{Guid.NewGuid():N}@test.com",
            Role               = "PrayerAmbassador",
            Status             = "Active",
            ChapterId          = 1,
            JoinedDate         = DateTime.UtcNow.ToString("O"),
            ServiceRadiusMiles = 15.0
        });
        postResp.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<List<VolunteerResponseDto>>("/api/v1/volunteers");
        Assert.NotNull(list);
        Assert.Contains(list, v => v.LastName == lastName);
    }

    [Fact]
    public async Task PostActiveVolunteer_DashboardStats_ShowsIncreasedCount()
    {
        var client = await AuthedClientAsync();

        var before = await client.GetFromJsonAsync<DashboardStatsDto>("/api/v1/dashboard/stats");
        int beforeCount = before!.ActiveVolunteers;

        await client.PostAsJsonAsync("/api/v1/volunteers", new
        {
            FirstName          = "Stats",
            LastName           = $"Vol-{Guid.NewGuid():N}",
            Email              = $"statsv-{Guid.NewGuid():N}@test.com",
            Role               = "Driver",
            Status             = "Active",
            ChapterId          = 1,
            JoinedDate         = DateTime.UtcNow.ToString("O"),
            ServiceRadiusMiles = 10.0
        });

        var after = await client.GetFromJsonAsync<DashboardStatsDto>("/api/v1/dashboard/stats");
        Assert.True(after!.ActiveVolunteers > beforeCount);
    }

    // ── Donors ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostDonor_Returns201()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.PostAsJsonAsync("/api/v1/donors", new
        {
            FirstName    = "Donor",
            LastName     = $"Test-{Guid.NewGuid():N}",
            Email        = $"donor-{Guid.NewGuid():N}@test.com",
            ChapterId    = 1,
            Tier         = "Friend",
            FirstGiftDate = DateTime.UtcNow.ToString("O"),
            LastGiftDate  = DateTime.UtcNow.ToString("O")
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task GetDonors_Returns200()
    {
        var client = await AuthedClientAsync();
        var resp   = await client.GetAsync("/api/v1/donors");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Donations ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostDonation_Returns200OrCreated()
    {
        var client  = await AuthedClientAsync();
        var donorId = await CreateDonorAsync(client);

        var resp = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            DonorId           = donorId,
            Amount            = 100.00m,
            Date              = DateTime.UtcNow.ToString("O"),
            Channel           = "Check",
            ChapterId         = 1,
            ContributionStatus = "Processed",
            AllocationStatus  = "Unallocated"
        });

        Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Created,
            $"Expected 200 or 201 but got {(int)resp.StatusCode}");
    }

    [Fact]
    public async Task PostDonation_ThisMonth_AppearsinDashboardStats()
    {
        var client  = await AuthedClientAsync();
        var donorId = await CreateDonorAsync(client);

        var before = await client.GetFromJsonAsync<DashboardStatsDto>("/api/v1/dashboard/stats");

        await client.PostAsJsonAsync("/api/v1/donations", new
        {
            DonorId           = donorId,
            Amount            = 250.00m,
            Date              = DateTime.UtcNow.ToString("O"),
            Channel           = "Online",
            ChapterId         = 1,
            ContributionStatus = "Processed",
            AllocationStatus  = "Unallocated"
        });

        var after = await client.GetFromJsonAsync<DashboardStatsDto>("/api/v1/dashboard/stats");
        Assert.True(after!.DonationsThisMonth >= before!.DonationsThisMonth + 250m);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client   = _factory.CreateClient();
        var email    = $"voldnr-{Guid.NewGuid():N}@test.com";
        const string password = "VolDnr1Pass!";

        (await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email     = email,
            Password  = password,
            FirstName = "VolDnr",
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

    private static async Task<int> CreateDonorAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/donors", new
        {
            FirstName    = "Jane",
            LastName     = $"Donor-{Guid.NewGuid():N}",
            Email        = $"jd-{Guid.NewGuid():N}@test.com",
            ChapterId    = 1,
            Tier         = "Friend",
            FirstGiftDate = DateTime.UtcNow.ToString("O"),
            LastGiftDate  = DateTime.UtcNow.ToString("O")
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DonorResponseDto>())!.Id;
    }
}
