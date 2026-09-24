using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Api.Services;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// The emails a prayer care request sends as it progresses: who gets what (the family or the person
/// who referred them, and Whitney's team), at which step, and what they must not contain.
/// One host (with a capturing mail sender) is shared by all tests here; each test finds its own emails
/// by its unique family / referrer addresses and its request's case link.
/// </summary>
[Collection("Integration")]
public class RequestEmailTests
{
    private const string TeamA = "whitney@team.test";
    private const string TeamB = "helper@team.test";
    private static int _nextChapterId = 8000;

    private readonly LotvApiFactory _baseFactory;

    public RequestEmailTests(LotvApiFactory factory)
    {
        _baseFactory = factory;
        (_factory, _mail) = SharedHost.GetOrCreate(factory);
    }

    private readonly WebApplicationFactory<Program> _factory;
    private readonly CapturingNotifications _mail;

    private sealed class CapturingNotifications : INotificationService
    {
        public readonly ConcurrentQueue<(string To, string Subject, string Html)> Sent = new();
        public Task<Result> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        { Sent.Enqueue((toEmail, subject, htmlBody)); return Task.FromResult(Result.Ok()); }
        public Task<Result> SendEmailTemplateAsync(string toEmail, string toName, string templateId, object templateData) => Task.FromResult(Result.Ok());
        public Task<Result> SendSmsAsync(string toPhone, string message) => Task.FromResult(Result.Ok());
        public Task QueueNotificationAsync(string userId, string type, string message, object? payload = null) => Task.CompletedTask;
    }

    private static class SharedHost
    {
        private static (WebApplicationFactory<Program>, CapturingNotifications)? _shared;
        private static readonly object Gate = new();

        public static (WebApplicationFactory<Program> Factory, CapturingNotifications Mail) GetOrCreate(LotvApiFactory baseFactory)
        {
            lock (Gate)
            {
                if (_shared is { } s) return s;
                var mail = new CapturingNotifications();
                var factory = baseFactory.WithWebHostBuilder(b =>
                {
                    b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Notifications:IntakeTeamEmails"] = $"{TeamA}; {TeamB}",
                        ["App:WebBaseUrl"] = "https://portal.test/",
                    }));
                    b.ConfigureServices(services =>
                    {
                        foreach (var d in services.Where(d => d.ServiceType == typeof(INotificationService)).ToList()) services.Remove(d);
                        services.AddSingleton<INotificationService>(mail);
                    });
                });
                _shared = (factory, mail);
                return _shared.Value;
            }
        }
    }

    /// <summary>Emails to one address (the family / referrer addresses are unique per test).</summary>
    private List<(string To, string Subject, string Html)> To(string address) => _mail.Sent.Where(s => s.To == address).ToList();

    /// <summary>Team emails about one request, identified by its case link.</summary>
    private List<(string To, string Subject, string Html)> Team(int requestId, string member = TeamA) =>
        _mail.Sent.Where(s => s.To == member && s.Html.Contains($"/admin/cases/{requestId}\"")).ToList();

    // ── When a request comes in ───────────────────────────────────────────────

    [Fact]
    public async Task NewRequest_ConfirmsToTheFamily_AndAlertsEveryoneOnTheTeam_WithAnAbsoluteLink()
    {
        var chapter = await NewChapterAsync();
        var familyEmail = $"family-{Guid.NewGuid():N}@example.com";

        var (_, requestId) = await ApplyAsync(chapter, familyEmail: familyEmail, reason: "Stillbirth", story: "A private story about our loss");

        var confirmation = Assert.Single(To(familyEmail));
        Assert.Equal("Your Prayer Care Package Request Has Been Received", confirmation.Subject);
        Assert.Contains("Dear Tom,", confirmation.Html);
        Assert.Contains("held in our prayers", confirmation.Html);

        foreach (var member in new[] { TeamA, TeamB })
        {
            var alert = Assert.Single(Team(requestId, member));
            Assert.Equal("New Prayer Care Package Request", alert.Subject);
            Assert.Contains($"href=\"https://portal.test/admin/cases/{requestId}\"", alert.Html);   // absolute, single slash
            Assert.Contains("Testville, IL", alert.Html);
            // Sensitive details stay out of the team's inbox
            Assert.DoesNotContain("Stillbirth", alert.Html);
            Assert.DoesNotContain("private story", alert.Html);
        }
    }

    [Fact]
    public async Task ARequestNobodyCanTakeYet_TellsTheTeamItIsUnassigned()
    {
        var chapter = await NewChapterAsync();          // no volunteers in this chapter

        var (_, requestId) = await ApplyAsync(chapter);

        var alert = Assert.Single(Team(requestId));
        Assert.Contains("Not assigned yet", alert.Html);
        Assert.Contains("Unassigned Queue", alert.Html);
    }

    [Fact]
    public async Task AnAutoAssignedRequest_TellsTheTeamWhoGotIt()
    {
        var chapter = await NewChapterAsync();
        await AddVolunteerAsync(chapter, "Alice", "Assembler");

        var (_, requestId) = await ApplyAsync(chapter);

        var alert = Assert.Single(Team(requestId));
        Assert.Contains("Assigned to <strong>Alice Assembler</strong>", alert.Html);
        Assert.DoesNotContain("Not assigned yet", alert.Html);
    }

    [Fact]
    public async Task AReferredRequest_ConfirmsToTheReferrer_NotToTheFamily()
    {
        var chapter = await NewChapterAsync();
        var familyEmail = $"family-{Guid.NewGuid():N}@example.com";
        var referrerEmail = $"referrer-{Guid.NewGuid():N}@example.com";

        var (_, requestId) = await ApplyAsync(chapter, familyEmail: familyEmail, forSelf: false, referrerEmail: referrerEmail);

        Assert.Empty(To(familyEmail));                                     // they may not know a package is coming
        var confirmation = Assert.Single(To(referrerEmail));
        Assert.Contains("Dear Rita,", confirmation.Html);
        Assert.Contains("Thank you for thinking of", confirmation.Html);
        Assert.Contains("Referred by Rita Referrer", Assert.Single(Team(requestId)).Html);
    }

    [Fact]
    public async Task APossibleDuplicate_IsMarkedAsSuchInTheTeamEmail()
    {
        var chapter = await NewChapterAsync();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await ApplyAsync(chapter, familyEmail: email);

        var (_, duplicateRequestId) = await ApplyAsync(chapter, familyEmail: email);

        var alert = Assert.Single(Team(duplicateRequestId));
        Assert.Equal("New Prayer Care Package Request — Possible Duplicate", alert.Subject);
        Assert.Contains("Possible duplicate:", alert.Html);
    }

    // ── Shipped ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenShipped_TheFamilyGetsTheTrackingNumber_AndTheTeamIsTold()
    {
        var chapter = await NewChapterAsync();
        var familyEmail = $"family-{Guid.NewGuid():N}@example.com";
        var (_, requestId) = await ApplyAsync(chapter, familyEmail: familyEmail);
        await SetTrackingAsync(requestId, "TRK-9000-42");
        var admin = await AdminClientAsync();

        await MoveAsync(admin, requestId, "InProgress", "AwaitingShipment", "Shipped");

        var toFamily = Assert.Single(To(familyEmail).Where(m => m.Subject == "Your Prayer Care Package Is On Its Way"));
        Assert.Contains("TRK-9000-42", toFamily.Html);
        var toTeam = Assert.Single(Team(requestId).Where(m => m.Subject.StartsWith("Package shipped")));
        Assert.Contains("TRK-9000-42", toTeam.Html);
        Assert.Contains($"https://portal.test/admin/cases/{requestId}", toTeam.Html);
        Assert.Single(Team(requestId, TeamB).Where(m => m.Subject.StartsWith("Package shipped")));
    }

    // ── Completed ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenCompleted_TheFamilyIsToldItArrived_AndTheTeamIsToldItIsDone()
    {
        var chapter = await NewChapterAsync();
        var familyEmail = $"family-{Guid.NewGuid():N}@example.com";
        var (_, requestId) = await ApplyAsync(chapter, familyEmail: familyEmail);
        await SetTrackingAsync(requestId, "TRK-1");
        var admin = await AdminClientAsync();

        await MoveAsync(admin, requestId, "InProgress", "AwaitingShipment", "Shipped", "Fulfilled");

        var delivered = Assert.Single(To(familyEmail).Where(m => m.Subject == "Your Prayer Care Package Has Been Delivered"));
        Assert.Contains("not forgotten", delivered.Html);
        var toTeam = Assert.Single(Team(requestId).Where(m => m.Subject.StartsWith("Package completed")));
        Assert.Contains("days from request to delivery", toTeam.Html);
        Assert.Single(Team(requestId, TeamB).Where(m => m.Subject.StartsWith("Package completed")));
    }

    [Fact]
    public async Task WhenAReferredRequestIsCompleted_TheReferrerIsThankedToo()
    {
        var chapter = await NewChapterAsync();
        var referrerEmail = $"referrer-{Guid.NewGuid():N}@example.com";
        var (_, requestId) = await ApplyAsync(chapter, forSelf: false, referrerEmail: referrerEmail);
        await SetTrackingAsync(requestId, "TRK-2");
        var admin = await AdminClientAsync();

        await MoveAsync(admin, requestId, "InProgress", "AwaitingShipment", "Fulfilled");   // hand-delivered, never "Shipped"

        var thanks = Assert.Single(To(referrerEmail).Where(m => m.Subject == "The Prayer Care Package You Requested Has Been Delivered"));
        Assert.Contains("Dear Rita,", thanks.Html);
        Assert.Contains("referred them has been thanked", Assert.Single(Team(requestId).Where(m => m.Subject.StartsWith("Package completed"))).Html);
    }

    [Fact]
    public async Task ResavingTheSameCompletedStatus_DoesNotResendAnything()
    {
        var chapter = await NewChapterAsync();
        var (_, requestId) = await ApplyAsync(chapter);
        var admin = await AdminClientAsync();
        await MoveAsync(admin, requestId, "InProgress", "Fulfilled");
        var before = Team(requestId).Count;

        await MoveAsync(admin, requestId, "Fulfilled");                                          // same status again

        Assert.Equal(before, Team(requestId).Count);
        Assert.Single(Team(requestId).Where(m => m.Subject.StartsWith("Package completed")));
    }

    [Fact]
    public async Task TheVolunteerFulfillButton_SendsTheCompletedEmails_Once()
    {
        var chapter = await NewChapterAsync();
        var familyEmail = $"family-{Guid.NewGuid():N}@example.com";
        var (_, requestId) = await ApplyAsync(chapter, familyEmail: familyEmail);
        var admin = await AdminClientAsync();

        for (var i = 0; i < 2; i++)                                                               // pressed twice
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/requests/{requestId}/fulfill", new { Notes = "Delivered by hand" })).StatusCode);

        Assert.Single(To(familyEmail).Where(m => m.Subject == "Your Prayer Care Package Has Been Delivered"));
        Assert.Single(Team(requestId).Where(m => m.Subject.StartsWith("Package completed")));
    }

    // ── Rendering and the preview page ───────────────────────────────────────

    [Fact]
    public void EveryValueInTheEmails_IsHtmlEncoded()
    {
        const string hostile = "<script>alert(1)</script>";
        var team = new RequestEmails.TeamRequest(1, hostile, hostile, hostile, hostile, "https://portal.test/x\"><script>", hostile);

        var html = string.Concat(
            RequestEmails.TeamNewRequest(team).Html, RequestEmails.TeamShipped(team).Html, RequestEmails.TeamCompleted(team).Html,
            RequestEmails.Received(hostile, true, hostile).Html, RequestEmails.Shipped(hostile, hostile).Html,
            RequestEmails.Completed(hostile).Html, RequestEmails.ReferrerCompleted(hostile, hostile).Html);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Preview_ListsEveryEmail_ForTheFamilyAndForTheTeam_AndWhoTheTeamListGoesTo()
    {
        var admin = await AdminClientAsync();

        var resp = await admin.GetAsync("/api/v1/email-previews");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(new[] { TeamA, TeamB }, body.GetProperty("teamRecipients").EnumerateArray().Select(x => x.GetString()).ToArray());
        var emails = body.GetProperty("emails").EnumerateArray().ToList();
        Assert.Equal(9, emails.Count);
        Assert.Equal(5, emails.Count(e => e.GetProperty("audience").GetString() == "Family"));
        Assert.Equal(4, emails.Count(e => e.GetProperty("audience").GetString() == "Whitney & the team"));
        Assert.All(emails, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("subject").GetString()));
            Assert.Contains("Lily of the Valley Ministry", e.GetProperty("html").GetString());
        });
    }

    [Fact]
    public async Task Preview_RequiresSignIn()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/v1/email-previews")).StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> NewChapterAsync()
    {
        var id = Interlocked.Increment(ref _nextChapterId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Chapters.Add(new Chapter { Id = id, Name = $"Mail Chapter {id}", City = "Testville", State = "IL", ContactName = "T", ContactEmail = "c@test.example.com" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task AddVolunteerAsync(int chapterId, string first, string last)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Volunteers.Add(new Volunteer
        {
            FirstName = first, LastName = last, Email = $"{first}.{Guid.NewGuid():N}@test.example.com".ToLowerInvariant(),
            Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ChapterId = chapterId, JoinedDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SetTrackingAsync(int requestId, string tracking)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        (await db.Requests.SingleAsync(r => r.Id == requestId)).TrackingNumber = tracking;
        await db.SaveChangesAsync();
    }

    private async Task<(int FamilyId, int RequestId)> ApplyAsync(int chapterId,
        string? familyEmail = null, string reason = "Infertility", string? story = null, bool forSelf = true, string? referrerEmail = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Mail{tag}", Parent2FirstName = "Ann", Parent2LastName = $"Mail{tag}",
                Email = familyEmail ?? $"mail-{tag}@example.com", StreetAddress = "1 Test St", City = "Testville", State = "IL", Zip = "60601",
                Reason = reason, Story = story, ChapterId = chapterId,
            },
            ForSelf = forSelf, PackageType = "Comfort",
            ReferrerFirstName = forSelf ? null : "Rita", ReferrerLastName = forSelf ? null : "Referrer", ReferrerEmail = forSelf ? null : referrerEmail,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("familyId").GetInt32(), body.GetProperty("requestId").GetInt32());
    }

    private static async Task MoveAsync(HttpClient admin, int requestId, params string[] statuses)
    {
        foreach (var status in statuses)
        {
            var resp = await admin.PutAsJsonAsync($"/api/v1/requests/{requestId}/status", new { Status = status });
            Assert.True(resp.IsSuccessStatusCode, $"moving to {status}: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        }
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"mailtest-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Mail!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = password, FirstName = "Mail", LastName = "Tester", Role = "HQAdmin", ChapterId = 1 });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
