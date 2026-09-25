using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// A request submitted through the public form waits in the Unassigned Queue for staff to assign, even when a routing rule
/// matches or a volunteer would be a good fit. (The other suites switch Intake:AutoAssign on to exercise automatic assignment.)
/// </summary>
[Collection("Integration")]
public class IntakeQueueTests
{
    private static int _nextChapterId = 9600;
    private readonly LotvApiFactory _factory;

    public IntakeQueueTests(LotvApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> Host(bool autoAssign) => _factory.WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Intake:AutoAssign"] = autoAssign ? "true" : "false",
        })));

    private async Task<(int Chapter, int Volunteer, int Request)> SubmitAsync(WebApplicationFactory<Program> host)
    {
        int chapter, volunteer;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            chapter = Interlocked.Increment(ref _nextChapterId);
            db.Chapters.Add(new Chapter { Id = chapter, Name = $"Queue {chapter}", City = "Testville", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
            // A perfectly good volunteer: the kind automatic assignment would pick straight away.
            var v = new Volunteer { FirstName = "Eager", LastName = $"Helper{chapter}", Email = $"eager{chapter}@test.example.com", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ChapterId = chapter, JoinedDate = DateTime.UtcNow };
            db.Volunteers.Add(v);
            await db.SaveChangesAsync();
            volunteer = v.Id;
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await host.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Queue", Parent1LastName = $"Waiter{tag}", Email = $"waiter-{tag}@test.example.com", Phone = "",
                StreetAddress = "1 Test St", City = "Testville", State = "IL", Zip = "60601", Reason = "Infertility", ChapterId = chapter,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var request = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();
        return (chapter, volunteer, request);
    }

    [Fact]
    public async Task ANewRequest_WaitsInTheUnassignedQueue_EvenWhenAGoodVolunteerExists()
    {
        using var host = Host(autoAssign: false);
        var (_, _, request) = await SubmitAsync(host);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var saved = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == request);
        Assert.Null(saved.AssignedToId);
        Assert.Equal(CaseStatus.New, saved.Status);
        Assert.Equal(ProcessStage.Unassigned, saved.ProcessStage);
        Assert.Empty(await db.RequestAssignments.Where(a => a.RequestId == request).ToListAsync());
        Assert.DoesNotContain(await db.RequestActivities.Where(a => a.RequestId == request).ToListAsync(), a => a.ActivityType == ActivityType.Assigned);

        // ...and staff see it in the queue.
        var admin = host.CreateClient();
        var email = $"queue-admin-{Guid.NewGuid():N}@test.com";
        await admin.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Queue!", FirstName = "Q", LastName = "Admin", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Queue!" });
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        var queue = await admin.GetFromJsonAsync<JsonElement>("/api/v1/requests/queue");
        Assert.Contains(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == request);
    }

    private async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> host)
    {
        var admin = host.CreateClient();
        var email = $"queue-admin-{Guid.NewGuid():N}@test.com";
        await admin.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Queue!", FirstName = "Q", LastName = "Admin", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Queue!" });
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return admin;
    }

    [Fact]
    public async Task ACaseCreatedByStaff_AlsoWaitsInTheQueue()
    {
        using var host = Host(autoAssign: false);
        var (chapter, _, first) = await SubmitAsync(host);
        int familyId;
        using (var scope = host.Services.CreateScope())
            familyId = (await scope.ServiceProvider.GetRequiredService<LotvDbContext>().Requests.AsNoTracking().SingleAsync(r => r.Id == first)).FamilyId;

        var admin = await AdminAsync(host);
        var created = await admin.PostAsJsonAsync("/api/v1/requests", new { familyId, chapterId = chapter, reason = 0 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var check = host.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<LotvDbContext>().Requests.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Null(saved.AssignedToId);
        Assert.Equal(ProcessStage.Unassigned, saved.ProcessStage);
    }

    [Fact]
    public async Task ARequestClearedFromDuplicateReview_LandsInTheQueue_NotWithAVolunteer()
    {
        using var host = Host(autoAssign: false);
        var (_, _, request) = await SubmitAsync(host);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var r = await db.Requests.SingleAsync(x => x.Id == request);
            r.NeedsDuplicateReview = true;
            await db.SaveChangesAsync();
        }

        var admin = await AdminAsync(host);
        var resolved = await admin.PostAsJsonAsync($"/api/v1/families/duplicate-review/{request}/resolve", new { Action = "confirm-new" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        using var check = host.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<LotvDbContext>().Requests.AsNoTracking().SingleAsync(r => r.Id == request);
        Assert.False(saved.NeedsDuplicateReview);
        Assert.Null(saved.AssignedToId);
        Assert.Equal(ProcessStage.Unassigned, saved.ProcessStage);
    }

    [Fact]
    public async Task WithAutoAssignSwitchedOn_TheSameRequestIsAssignedOnSubmit()
    {
        using var host = Host(autoAssign: true);
        var (_, volunteer, request) = await SubmitAsync(host);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.Equal(volunteer, (await db.Requests.AsNoTracking().SingleAsync(r => r.Id == request)).AssignedToId);
    }
}
