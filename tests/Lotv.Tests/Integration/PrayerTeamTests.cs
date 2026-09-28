using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Many volunteers can pray for one family, separate from the one volunteer who assembles and ships the package.
/// </summary>
[Collection("Integration")]
public class PrayerTeamTests
{
    private readonly LotvApiFactory _factory;
    public PrayerTeamTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"prayteam-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1PrayTeam!", FirstName = "Pat", LastName = "Admin", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1PrayTeam!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<(int Chapter, int Request, int PackerVol, int PrayerVolA, int PrayerVolB)> SetupAsync()
    {
        // Unique names, not just unique emails: FindMyVolunteersAsync matches by name as a fallback, and a literal
        // name repeated across this file's several tests would otherwise pull in another test's volunteer too.
        var tag = Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"PrayTeam {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        var packer = new Volunteer { FirstName = "Paula", LastName = $"Packer{tag}", Email = $"packer-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Inactive };
        var prayerA = new Volunteer { FirstName = "Ann", LastName = $"Prayer{tag}", Email = $"pa-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.PrayerAmbassador, Status = VolunteerStatus.Active };
        var prayerB = new Volunteer { FirstName = "Bea", LastName = $"Prayer{tag}", Email = $"pb-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.PrayerAmbassador, Status = VolunteerStatus.Active };
        db.Volunteers.AddRange(packer, prayerA, prayerB);
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily" };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        packer.ChapterId = chapter.Id; prayerA.ChapterId = chapter.Id; prayerB.ChapterId = chapter.Id; family.ChapterId = chapter.Id;
        var request = new PackageRequest { FamilyId = family.Id, ChapterId = chapter.Id, AssignedToId = packer.Id, AssignedTo = packer.FullName, Status = CaseStatus.InProgress };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return (chapter.Id, request.Id, packer.Id, prayerA.Id, prayerB.Id);
    }

    [Fact]
    public async Task TwoPrayerAmbassadors_CanBothBeOnOneFamilysTeam_WithoutTouchingWhoPacksIt()
    {
        var (_, request, packer, a, b) = await SetupAsync();
        var admin = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = b })).StatusCode);

        var team = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/requests/{request}/prayer-team", Json);
        Assert.NotNull(team);
        Assert.Equal(2, team!.Count);

        // The package assignment never moved.
        var req = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{request}", Json);
        Assert.Equal(packer, req.GetProperty("assignedToId").GetInt32());
    }

    [Fact]
    public async Task OnePrayerAmbassador_CanBeOnManyFamiliesTeams()
    {
        var (chapter, request1, _, a, _) = await SetupAsync();
        var admin = await AdminClientAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family2 = new Family { Parent1FirstName = "Sec", Parent1LastName = "Ond", ChapterId = chapter };
        db.Families.Add(family2);
        await db.SaveChangesAsync();
        var request2 = new PackageRequest { FamilyId = family2.Id, ChapterId = chapter, Status = CaseStatus.New };
        db.Requests.Add(request2);
        await db.SaveChangesAsync();

        await admin.PostAsJsonAsync($"/api/v1/requests/{request1}/prayer-team", new { volunteerId = a });
        await admin.PostAsJsonAsync($"/api/v1/requests/{request2.Id}/prayer-team", new { volunteerId = a });

        // As the volunteer.
        var vol = _factory.CreateClient();
        using var s2 = _factory.Services.CreateScope();
        var volEmail = (await s2.ServiceProvider.GetRequiredService<LotvDbContext>().Volunteers.FindAsync(a))!.Email;
        await vol.PostAsJsonAsync("/api/v1/auth/register", new { Email = volEmail, Password = "TestPass1PrayTeam!", FirstName = "Ann", LastName = "Prayer", Role = "Volunteer", ChapterId = (int?)null });
        var login = await vol.PostAsJsonAsync("/api/v1/auth/login", new { Username = volEmail, Password = "TestPass1PrayTeam!" });
        vol.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        var mine = await vol.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/my-prayer-list", Json);
        Assert.NotNull(mine);
        Assert.Equal(2, mine!.Count);
    }

    [Fact]
    public async Task ANonPrayerAmbassador_CannotBeAddedToAPrayerTeam()
    {
        var (_, request, packer, _, _) = await SetupAsync();
        var admin = await AdminClientAsync();

        var resp = await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = packer });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AddingTheSamePersonTwice_IsRefused()
    {
        var (_, request, _, a, _) = await SetupAsync();
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a });

        var again = await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task RemovingFromTheTeam_TakesThemOff_ButLeavesEveryoneElse()
    {
        var (_, request, _, a, b) = await SetupAsync();
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a });
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = b });

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/requests/{request}/prayer-team/{a}")).StatusCode);

        var team = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/requests/{request}/prayer-team", Json);
        Assert.Single(team!);
    }

    // ── Self-service: a Prayer Ambassador picks their own people ────────────────

    private async Task<HttpClient> VolunteerClientAsync(int volunteerId)
    {
        using var scope = _factory.Services.CreateScope();
        var email = (await scope.ServiceProvider.GetRequiredService<LotvDbContext>().Volunteers.FindAsync(volunteerId))!.Email;
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1PrayTeam!", FirstName = "V", LastName = "V", Role = "Volunteer", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1PrayTeam!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    [Fact]
    public async Task APrayerAmbassador_CanSeeAndJoinAFamilysTeamThemselves_WithoutStaff()
    {
        var (_, request, _, a, _) = await SetupAsync();
        var ann = await VolunteerClientAsync(a);

        var candidates = await ann.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/prayer-candidates", Json);
        Assert.Contains(candidates!, c => c.GetProperty("id").GetInt32() == request && !c.GetProperty("alreadyPraying").GetBoolean());

        var join = await ann.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a });
        Assert.Equal(HttpStatusCode.Created, join.StatusCode);

        var mine = await ann.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/my-prayer-list", Json);
        Assert.Single(mine!);

        var again = await ann.GetFromJsonAsync<List<JsonElement>>("/api/v1/requests/prayer-candidates", Json);
        Assert.True(again!.Single(c => c.GetProperty("id").GetInt32() == request).GetProperty("alreadyPraying").GetBoolean());
    }

    [Fact]
    public async Task APrayerAmbassador_CanLeaveATeamTheyJoined_ButNotSomeoneElsesTeam()
    {
        var (_, request, _, a, b) = await SetupAsync();
        var ann = await VolunteerClientAsync(a);
        await ann.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = a });
        var admin = await AdminClientAsync();
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = b });

        // Leaving herself is fine...
        Assert.Equal(HttpStatusCode.OK, (await ann.DeleteAsync($"/api/v1/requests/{request}/prayer-team/{a}")).StatusCode);
        // ...but she can't remove Bea from the team.
        Assert.Equal(HttpStatusCode.Forbidden, (await ann.DeleteAsync($"/api/v1/requests/{request}/prayer-team/{b}")).StatusCode);

        var team = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/requests/{request}/prayer-team", Json);
        Assert.Single(team!);
    }

    [Fact]
    public async Task APrayerAmbassador_CannotAddSomeoneElseToATeam()
    {
        var (_, request, _, a, b) = await SetupAsync();
        var ann = await VolunteerClientAsync(a);

        var resp = await ann.PostAsJsonAsync($"/api/v1/requests/{request}/prayer-team", new { volunteerId = b });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
