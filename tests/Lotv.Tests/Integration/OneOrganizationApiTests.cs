using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Api.Auth;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// The API as it runs in production: ONE organization (Chapters:Enabled=false). Nobody carries a chapter scope, so a staff member or
/// volunteer whose account says "chapter 1" must still be able to work a case that was filed under another chapter id. The rest of
/// the integration suite runs with chapters ON, which is why this lives in its own non-parallel collection with its own host.
/// </summary>
[Collection("ChapterMode")]
public class OneOrganizationApiTests : IClassFixture<LotvApiFactory>
{
    private const string Password = "TestPass1OneOrg!";
    private readonly LotvApiFactory _baseFactory;
    public OneOrganizationApiTests(LotvApiFactory factory) => _baseFactory = factory;

    private WebApplicationFactoryWrapper OneOrg() => new(_baseFactory);

    // Wraps a host with chapters off and puts the shared switch back afterwards.
    private sealed class WebApplicationFactoryWrapper : IDisposable
    {
        private readonly bool _before = ChapterMode.Enabled;
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory { get; }
        public WebApplicationFactoryWrapper(LotvApiFactory baseFactory) =>
            Factory = baseFactory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?> { ["Chapters:Enabled"] = "false" })));
        public void Dispose() => ChapterMode.Enabled = _before;
    }

    private static async Task<HttpClient> ClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> f, string role, int? chapterId)
    {
        var client = f.CreateClient();
        var email = $"oneorg-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password, FirstName = "One", LastName = "Org", Role = role, ChapterId = chapterId });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        return client;
    }

    private static async Task<int> CaseAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> f, int chapterId, int? assignedToVolunteerId = null)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = $"Ily{Guid.NewGuid():N}", ChapterId = chapterId };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var req = new PackageRequest { FamilyId = family.Id, ChapterId = chapterId, Status = CaseStatus.InProgress, AssignedToId = assignedToVolunteerId };
        db.Requests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    [Fact]
    public async Task AStaffMember_CanWorkACaseFiledUnderAnotherChapter()
    {
        using var w = OneOrg();
        var staff = await ClientAsync(w.Factory, "ChapterStaff", chapterId: 1);
        var caseInChapter9 = await CaseAsync(w.Factory, chapterId: 9);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/requests/{caseInChapter9}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PutAsJsonAsync($"/api/v1/requests/{caseInChapter9}/status", new { Status = "OnHold" })).StatusCode);

        var list = await staff.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/v1/requests");
        Assert.Contains(list!, r => r.GetProperty("id").GetInt32() == caseInChapter9);
    }

    [Fact]
    public async Task AVolunteer_CanSaveTrackingOnTheirOwnCase_EvenIfTheirAccountSaysAnotherChapter()
    {
        using var w = OneOrg();
        var vol = await ClientAsync(w.Factory, "Volunteer", chapterId: 1);
        var email = vol.DefaultRequestHeaders.GetValues("X-Test-Email").First();
        int volId;
        using (var scope = w.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var v = new Volunteer { FirstName = "One", LastName = "Org", Email = email, Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ChapterId = 9 };
            db.Volunteers.Add(v);
            await db.SaveChangesAsync();
            volId = v.Id;
        }
        var mine = await CaseAsync(w.Factory, chapterId: 9, assignedToVolunteerId: volId);

        var save = await vol.PatchAsJsonAsync($"/api/v1/requests/{mine}", new { TrackingNumber = "1Z999AA10123456784" });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await vol.PutAsJsonAsync($"/api/v1/requests/{mine}/status", new { Status = "AwaitingShipment" })).StatusCode);
    }
}
