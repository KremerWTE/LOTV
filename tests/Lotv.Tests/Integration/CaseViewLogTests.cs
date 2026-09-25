using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Api.Services;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>Every look at a case is recorded in its activity log: who, and when. One entry per person per 10 minutes.</summary>
[Collection("Integration")]
public class CaseViewLogTests
{
    private readonly LotvApiFactory _factory;

    public CaseViewLogTests(LotvApiFactory factory) => _factory = factory;

    private async Task<int> NewRequestAsync()
    {
        int chapter;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            chapter = await db.Chapters.Select(c => c.Id).FirstOrDefaultAsync();
            if (chapter == 0)
            {
                db.Chapters.Add(new Chapter { Id = 9800, Name = "View log", City = "T", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
                await db.SaveChangesAsync();
                chapter = 9800;
            }
        }
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new { Parent1FirstName = "View", Parent1LastName = $"Logged{tag}", Email = $"viewlog-{tag}@test.example.com", StreetAddress = "1 St", City = "Town", State = "IL", Zip = "60601", Reason = "Infertility", ChapterId = chapter },
            ForSelf = true,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();
    }

    private async Task<(HttpClient Client, string Name)> UserAsync(string role, string first, string last, string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"viewer-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Viewer!", FirstName = first, LastName = last, Role = role, ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Viewer!" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return (client, $"{first} {last}");
    }

    private async Task<List<RequestActivity>> ViewsAsync(int requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.RequestActivities.AsNoTracking().Where(a => a.RequestId == requestId && a.ActivityType == ActivityType.Viewed).OrderBy(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task OpeningACase_IsLoggedWithWhoAndWhen_AndRefreshingDoesNotRepeatIt()
    {
        var request = await NewRequestAsync();
        var (chris, name) = await UserAsync("HQAdmin", "Chris", "Viewer");
        var before = DateTime.UtcNow.AddSeconds(-2);

        Assert.Equal(HttpStatusCode.OK, (await chris.GetAsync($"/api/v1/requests/{request}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await chris.GetAsync($"/api/v1/requests/{request}")).StatusCode);   // a refresh
        Assert.Equal(HttpStatusCode.OK, (await chris.GetAsync($"/api/v1/requests/{request}")).StatusCode);

        var views = await ViewsAsync(request);
        var view = Assert.Single(views);
        Assert.Equal(name, view.ActorName);
        Assert.InRange(view.Timestamp, before, DateTime.UtcNow.AddSeconds(2));

        // It appears in the case's activity log the page shows.
        var log = await chris.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{request}/activity");
        Assert.Contains(log.EnumerateArray(), e => e.GetProperty("actorName").GetString() == name && e.GetProperty("activityType").GetString() == "Viewed");
    }

    [Fact]
    public async Task EachPersonWhoLooks_GetsTheirOwnEntry_AndALookAfterTheWindowIsRecordedAgain()
    {
        var request = await NewRequestAsync();
        var (chris, chrisName) = await UserAsync("HQAdmin", "Chris", "Viewer");
        var (eric, ericName) = await UserAsync("ChapterStaff", "Eric", "Viewer");

        await chris.GetAsync($"/api/v1/requests/{request}");
        await eric.GetAsync($"/api/v1/requests/{request}");
        Assert.Equal([chrisName, ericName], (await ViewsAsync(request)).Select(v => v.ActorName));

        // Push Chris's entry outside the window: his next look is a new entry.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var old = await db.RequestActivities.SingleAsync(a => a.RequestId == request && a.ActivityType == ActivityType.Viewed && a.ActorName == chrisName);
            old.Timestamp = DateTime.UtcNow - CaseAudit.Window - TimeSpan.FromMinutes(1);
            await db.SaveChangesAsync();
        }
        await chris.GetAsync($"/api/v1/requests/{request}");
        Assert.Equal(3, (await ViewsAsync(request)).Count);
    }

    [Fact]
    public async Task ACaseThatDoesNotExist_OrCannotBeSeen_LeavesNoEntry()
    {
        var (chris, _) = await UserAsync("HQAdmin", "Chris", "Viewer");
        Assert.Equal(HttpStatusCode.NotFound, (await chris.GetAsync("/api/v1/requests/987654")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<LotvDbContext>().RequestActivities.AnyAsync(a => a.RequestId == 987654));
    }

    [Fact]
    public async Task AVolunteerOpeningTheirAssignment_IsLoggedToo()
    {
        var request = await NewRequestAsync();
        var email = $"vol-view-{Guid.NewGuid():N}@test.com";
        int volunteerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var chapter = (await db.Requests.AsNoTracking().SingleAsync(r => r.Id == request)).ChapterId;
            var v = new Volunteer { FirstName = "Vera", LastName = "Viewer", Email = email, Role = VolunteerRole.Driver, Status = VolunteerStatus.Active, ChapterId = chapter, JoinedDate = DateTime.UtcNow };
            db.Volunteers.Add(v);
            await db.SaveChangesAsync();
            volunteerId = v.Id;
        }
        var (admin, _) = await UserAsync("HQAdmin", "Ada", "Assigner");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteerId })).StatusCode);
        var (volunteer, name) = await UserAsync("Volunteer", "Vera", "Viewer", email);

        Assert.Equal(HttpStatusCode.OK, (await volunteer.GetAsync($"/api/v1/my-assignments/{request}")).StatusCode);

        Assert.Contains(await ViewsAsync(request), v => v.ActorName == name);
    }
}
