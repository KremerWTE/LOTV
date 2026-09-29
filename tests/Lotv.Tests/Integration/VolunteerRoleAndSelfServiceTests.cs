using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Two things:
///  1. A Package-Assembler-only volunteer is blocked server-side from the Prayer-Ambassador-only
///     routes (the UI already hides these; this is the same rule enforced where a direct API call
///     can't be talked out of it).
///  2. The volunteer-portal self-service assignment endpoints
///     (GET/POST /api/public/v1/volunteers/{id}/...) that back the magic-link portal pages
///     (/volunteer/my-assignments, /volunteer/available, /volunteer/history).
/// </summary>
[Collection("Integration")]
public class VolunteerRoleAndSelfServiceTests
{
    private readonly LotvApiFactory _factory;
    public VolunteerRoleAndSelfServiceTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private async Task<(int ChapterId, Volunteer Vol, HttpClient Client)> SeedVolunteerLoginAsync(VolunteerRole role)
    {
        var email = $"volrole-{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1VolRole!", FirstName = "Vo", LastName = "Lunteer", Role = "Volunteer", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1VolRole!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"VolRole {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var vol = new Volunteer { FirstName = "Vo", LastName = "Lunteer", Email = email, Role = role, Status = VolunteerStatus.Active, ChapterId = chapter.Id, JoinedDate = DateTime.UtcNow };
        db.Volunteers.Add(vol);
        await db.SaveChangesAsync();
        return (chapter.Id, vol, client);
    }

    // ── Server-side VolunteerRole gate on the prayer-only routes ──────────────────────────────

    [Fact]
    public async Task PackageAssemblerOnly_IsForbidden_FromPrayerRoutes()
    {
        var (_, _, client) = await SeedVolunteerLoginAsync(VolunteerRole.PackageAssembler);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/requests/my-prayer-list")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/requests/prayer-candidates")).StatusCode);
    }

    [Fact]
    public async Task PrayerAmbassador_CanUseThePrayerRoutes()
    {
        var (_, _, client) = await SeedVolunteerLoginAsync(VolunteerRole.PrayerAmbassador);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/requests/my-prayer-list")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/requests/prayer-candidates")).StatusCode);
    }

    [Fact]
    public async Task PackageAssemblerOnly_CannotJoinAPrayerTeam_EvenNamingThemselves()
    {
        var (chapterId, vol, client) = await SeedVolunteerLoginAsync(VolunteerRole.PackageAssembler);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = chapterId };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var req = new PackageRequest { FamilyId = family.Id, ChapterId = chapterId, Status = CaseStatus.New };
        db.Requests.Add(req);
        await db.SaveChangesAsync();

        var resp = await client.PostAsJsonAsync($"/api/v1/requests/{req.Id}/prayer-team", new { VolunteerId = vol.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMyVolunteer_DefaultsToPackageAssembler_ButAcceptsAnExplicitRole()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"CreateVol {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        var email1 = $"createvol-{Guid.NewGuid():N}@test.com";
        var client1 = _factory.CreateClient();
        await client1.PostAsJsonAsync("/api/v1/auth/register", new { Email = email1, Password = "TestPass1CreateVol!", FirstName = "De", LastName = "Fault", Role = "ChapterStaff", ChapterId = chapter.Id });
        var login1 = await client1.PostAsJsonAsync("/api/v1/auth/login", new { Username = email1, Password = "TestPass1CreateVol!" });
        client1.DefaultRequestHeaders.Authorization = new("Bearer", (await login1.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        var defaultCreated = await client1.PostAsJsonAsync("/api/v1/volunteers/me", new { });
        Assert.Equal(HttpStatusCode.Created, defaultCreated.StatusCode);
        var defaultVol = await defaultCreated.Content.ReadFromJsonAsync<Volunteer>(Json);
        Assert.Equal(VolunteerRole.PackageAssembler, defaultVol!.Role);

        var email2 = $"createvol-{Guid.NewGuid():N}@test.com";
        var client2 = _factory.CreateClient();
        await client2.PostAsJsonAsync("/api/v1/auth/register", new { Email = email2, Password = "TestPass1CreateVol!", FirstName = "Pray", LastName = "Er", Role = "ChapterStaff", ChapterId = chapter.Id });
        var login2 = await client2.PostAsJsonAsync("/api/v1/auth/login", new { Username = email2, Password = "TestPass1CreateVol!" });
        client2.DefaultRequestHeaders.Authorization = new("Bearer", (await login2.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        var prayerCreated = await client2.PostAsJsonAsync("/api/v1/volunteers/me", new { Role = "PrayerAmbassador" });
        Assert.Equal(HttpStatusCode.Created, prayerCreated.StatusCode);
        var prayerVol = await prayerCreated.Content.ReadFromJsonAsync<Volunteer>(Json);
        Assert.Equal(VolunteerRole.PrayerAmbassador, prayerVol!.Role);
    }

    // ── Public self-service assignment endpoints (magic-link portal) ──────────────────────────

    [Fact]
    public async Task Assignments_ReturnsOnlyThatVolunteersOwnCases()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"SelfSvc {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var mine  = new Volunteer { FirstName = "Mine", LastName = "V", Email = "mine@test.com", Role = VolunteerRole.PackageAssembler, ChapterId = chapter.Id, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        var other = new Volunteer { FirstName = "Other", LastName = "V", Email = "other@test.com", Role = VolunteerRole.PackageAssembler, ChapterId = chapter.Id, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        db.Volunteers.AddRange(mine, other);
        await db.SaveChangesAsync();
        var fam1 = new Family { Parent1FirstName = "A", Parent1LastName = "B", ChapterId = chapter.Id };
        var fam2 = new Family { Parent1FirstName = "C", Parent1LastName = "D", ChapterId = chapter.Id };
        db.Families.AddRange(fam1, fam2);
        await db.SaveChangesAsync();
        db.Requests.AddRange(
            new PackageRequest { FamilyId = fam1.Id, ChapterId = chapter.Id, AssignedToId = mine.Id, Status = CaseStatus.InProgress },
            new PackageRequest { FamilyId = fam2.Id, ChapterId = chapter.Id, AssignedToId = other.Id, Status = CaseStatus.InProgress });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/public/v1/volunteers/{mine.Id}/assignments", Json);
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(fam1.FullName, list![0].GetProperty("familyName").GetString());
    }

    [Fact]
    public async Task Available_IsScopedToTheVolunteersOwnChapter_AndExcludesAlreadyAssigned()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapterA = new Chapter { Name = $"ChapA {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var chapterB = new Chapter { Name = $"ChapB {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.AddRange(chapterA, chapterB);
        await db.SaveChangesAsync();
        var vol = new Volunteer { FirstName = "V", LastName = "A", Email = "va@test.com", Role = VolunteerRole.PackageAssembler, ChapterId = chapterA.Id, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        db.Volunteers.Add(vol);
        await db.SaveChangesAsync();
        var famA1 = new Family { Parent1FirstName = "A1", Parent1LastName = "F", ChapterId = chapterA.Id };
        var famA2 = new Family { Parent1FirstName = "A2", Parent1LastName = "F", ChapterId = chapterA.Id };
        var famB  = new Family { Parent1FirstName = "B1", Parent1LastName = "F", ChapterId = chapterB.Id };
        db.Families.AddRange(famA1, famA2, famB);
        await db.SaveChangesAsync();
        db.Requests.AddRange(
            new PackageRequest { FamilyId = famA1.Id, ChapterId = chapterA.Id, Status = CaseStatus.New, WantsPackage = true },                 // available
            new PackageRequest { FamilyId = famA2.Id, ChapterId = chapterA.Id, Status = CaseStatus.New, WantsPackage = true, AssignedToId = 999 }, // already assigned
            new PackageRequest { FamilyId = famB.Id,  ChapterId = chapterB.Id, Status = CaseStatus.New, WantsPackage = true });                 // other chapter
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/public/v1/volunteers/{vol.Id}/available", Json);
        Assert.NotNull(list);
        Assert.Single(list!);
    }

    [Fact]
    public async Task Claim_AssignsAnUnassignedRequest_AndASecondClaimIsRefused()
    {
        var (chapterId, vol, _) = await SeedVolunteerLoginAsync(VolunteerRole.PackageAssembler);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family { Parent1FirstName = "F", Parent1LastName = "L", ChapterId = chapterId };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var req = new PackageRequest { FamilyId = family.Id, ChapterId = chapterId, Status = CaseStatus.New, WantsPackage = true };
        db.Requests.Add(req);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var claim1 = await client.PostAsync($"/api/public/v1/volunteers/{vol.Id}/assignments/{req.Id}/claim", null);
        Assert.Equal(HttpStatusCode.OK, claim1.StatusCode);

        var second = new Volunteer { FirstName = "S", LastName = "econd", Email = "second@test.com", Role = VolunteerRole.PackageAssembler, ChapterId = chapterId, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        db.Volunteers.Add(second);
        await db.SaveChangesAsync();
        var claim2 = await client.PostAsync($"/api/public/v1/volunteers/{second.Id}/assignments/{req.Id}/claim", null);
        Assert.Equal(HttpStatusCode.Conflict, claim2.StatusCode);
    }

    [Fact]
    public async Task Status_IsRefused_WhenTheCaseIsntAssignedToTheCaller()
    {
        var (chapterId, vol, _) = await SeedVolunteerLoginAsync(VolunteerRole.PackageAssembler);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family { Parent1FirstName = "F", Parent1LastName = "L", ChapterId = chapterId };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var req = new PackageRequest { FamilyId = family.Id, ChapterId = chapterId, Status = CaseStatus.InProgress, AssignedToId = 999999 };
        db.Requests.Add(req);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync($"/api/public/v1/volunteers/{vol.Id}/assignments/{req.Id}/status", new { Status = "AwaitingShipment" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Status_MovesAnOwnedCaseForward_ButRejectsAnInvalidTransition()
    {
        var (chapterId, vol, _) = await SeedVolunteerLoginAsync(VolunteerRole.PackageAssembler);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family { Parent1FirstName = "F", Parent1LastName = "L", ChapterId = chapterId };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var req = new PackageRequest { FamilyId = family.Id, ChapterId = chapterId, Status = CaseStatus.InProgress, AssignedToId = vol.Id };
        db.Requests.Add(req);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var ok = await client.PostAsJsonAsync($"/api/public/v1/volunteers/{vol.Id}/assignments/{req.Id}/status", new { Status = "AwaitingShipment" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // AwaitingShipment -> New isn't a valid forward move.
        var bad = await client.PostAsJsonAsync($"/api/public/v1/volunteers/{vol.Id}/assignments/{req.Id}/status", new { Status = "New" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
