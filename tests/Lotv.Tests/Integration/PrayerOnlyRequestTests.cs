using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// A request for prayer alone, no comfort package — the intake form's "What would help most right now?" question
/// maps to PackageRequest.WantsPackage. Both kinds must show up for Prayer Ambassadors to join.
/// </summary>
[Collection("Integration")]
public class PrayerOnlyRequestTests
{
    private readonly LotvApiFactory _factory;
    public PrayerOnlyRequestTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private async Task<int> NewChapterAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var ch = new Chapter { Name = $"PrayOnly {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow, MaxActiveCasesPerVolunteer = 6, AcceptanceWindowHours = 24, UrgentAcceptanceWindowHours = 4 };
        db.Chapters.Add(ch);
        await db.SaveChangesAsync();
        return ch.Id;
    }

    private async Task<HttpClient> AddVolunteerAsync(int chapterId, VolunteerRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Volunteers.Add(new Volunteer { FirstName = "Pack", LastName = $"Er{Guid.NewGuid():N}"[..12], Email = $"packer-{Guid.NewGuid():N}@test.com", Role = role, Status = VolunteerStatus.Active, ChapterId = chapterId });
        await db.SaveChangesAsync();
        return _factory.CreateClient();
    }

    private async Task<(int FamilyId, int RequestId)> ApplyAsync(int chapterId, bool wantsPackage, bool excludeFromPrayerQueue = false)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Flow{tag}",
                Email = $"flow-{tag}@test.example.com", StreetAddress = "1 Test St",
                City = "Testville", State = "IL", Zip = "60000", Reason = "Infertility", ChapterId = chapterId,
            },
            ForSelf = true, WantsPackage = wantsPackage, ExcludeFromPrayerQueue = excludeFromPrayerQueue,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("familyId").GetInt32(), body.GetProperty("requestId").GetInt32());
    }

    [Fact]
    public async Task ARequest_DefaultsToWantingAPackage_WhenTheFormSaysNothing()
    {
        var chapter = await NewChapterAsync();
        var (_, requestId) = await ApplyAsync(chapter, wantsPackage: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.True((await db.Requests.AsNoTracking().SingleAsync(r => r.Id == requestId)).WantsPackage);
    }

    [Fact]
    public async Task APrayerOnlyRequest_IsStoredAsSuch_AndNeverGetsAPackageAssemblerAutoAssigned()
    {
        var chapter = await NewChapterAsync();
        await AddVolunteerAsync(chapter, VolunteerRole.PackageAssembler);   // would otherwise be auto-assigned

        var (_, requestId) = await ApplyAsync(chapter, wantsPackage: false);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var saved = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        Assert.False(saved.WantsPackage);
        Assert.Null(saved.AssignedToId);   // Intake:AutoAssign is on in tests; a prayer-only request still gets no packer
    }

    [Fact]
    public async Task BothKinds_AppearForAPrayerAmbassadorToChooseFrom()
    {
        var chapter = await NewChapterAsync();
        var (_, packageRequest) = await ApplyAsync(chapter, wantsPackage: true);
        var (_, prayerOnlyRequest) = await ApplyAsync(chapter, wantsPackage: false);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var pray = new Volunteer { FirstName = "Pray", LastName = $"Er{Guid.NewGuid():N}"[..10], Email = $"pray-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.PrayerAmbassador, Status = VolunteerStatus.Active, ChapterId = chapter };
        db.Volunteers.Add(pray);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = pray.Email, Password = "TestPass1PrayOnly!", FirstName = "Pray", LastName = "Er", Role = "Volunteer", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = pray.Email, Password = "TestPass1PrayOnly!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        var candidates = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/prayer-candidates", Json);
        Assert.NotNull(candidates);
        Assert.Contains(candidates!, c => c.GetProperty("id").GetInt32() == packageRequest && c.GetProperty("wantsPackage").GetBoolean());
        Assert.Contains(candidates!, c => c.GetProperty("id").GetInt32() == prayerOnlyRequest && !c.GetProperty("wantsPackage").GetBoolean());

        await client.PostAsJsonAsync($"/api/v1/requests/{prayerOnlyRequest}/prayer-team", new { volunteerId = pray.Id });
        var mine = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/my-prayer-list", Json);
        Assert.Single(mine!);
        Assert.Equal(prayerOnlyRequest, mine![0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task APackageOnlyRequest_IsStoredAsSuch_AndNeverAppearsInThePrayerCandidatesQueue()
    {
        var chapter = await NewChapterAsync();
        var (_, packageOnlyRequest) = await ApplyAsync(chapter, wantsPackage: true, excludeFromPrayerQueue: true);
        var (_, ordinaryRequest) = await ApplyAsync(chapter, wantsPackage: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var saved = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == packageOnlyRequest);
        Assert.True(saved.WantsPackage);
        Assert.True(saved.ExcludeFromPrayerQueue);

        var client = _factory.CreateClient();
        var email = $"hq-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1PrayOnly!", FirstName = "H", LastName = "Q", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1PrayOnly!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        var candidates = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/prayer-candidates", Json);
        Assert.NotNull(candidates);
        Assert.DoesNotContain(candidates!, c => c.GetProperty("id").GetInt32() == packageOnlyRequest);
        Assert.Contains(candidates!, c => c.GetProperty("id").GetInt32() == ordinaryRequest);
    }
}
