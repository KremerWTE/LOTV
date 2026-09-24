using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Api.Services;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lotv.Tests.Integration;

/// <summary>
/// Day-to-day operations: bereavement reminders, "email the family" for details that look wrong, the grief support list,
/// the optional CRM column, "create my volunteer record", and the startup repair of request stages.
/// </summary>
[Collection("Integration")]
public class OperationsTests
{
    private static int _nextChapterId = 9500;
    private readonly LotvApiFactory _factory;

    public OperationsTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // ── Email content ─────────────────────────────────────────────────────────

    [Fact]
    public void TheEmails_EncodeNamesSoTheyCantInjectMarkup()
    {
        var evil = "<script>alert(1)</script>";
        var assigned = OperationsEmails.VolunteerAssigned("Claire", new(1, evil, "Chicago, IL", "https://example.org/c/1", DateTime.UtcNow));
        var removed = OperationsEmails.VolunteerUnassigned("Claire", evil);
        var due = OperationsEmails.BereavementDue([new(evil, "3 Weeks", DateTime.UtcNow, false)], "https://example.org/f");
        var details = OperationsEmails.DetailsRequest(evil, ["Your mailing address"], [("Name", evil)]);

        foreach (var email in new[] { assigned, removed, due, details })
        {
            Assert.DoesNotContain("<script>", email.Html);
            Assert.Contains("&lt;script&gt;", email.Html);
        }
    }

    [Fact]
    public void FriendlyFields_TurnStaffIssuesIntoPlainRequests()
    {
        var f = new Family { Parent1FirstName = "Tom", Parent1LastName = "Jrny6b6228d8", Email = "bad", StreetAddress = "", City = "X", State = "IL", Zip = "60601" };
        var fields = OperationsEmails.FriendlyFields(FamilyDataQuality.Check(f).Issues);
        Assert.Contains("The spelling of your name", fields);
        Assert.Contains("Your email address", fields);
        Assert.Contains("Your mailing address", fields);
        Assert.Equal(fields.Count, fields.Distinct().Count());
    }

    // ── Bereavement reminders ─────────────────────────────────────────────────

    [Fact]
    public async Task Reminders_GoOutOnceForTouchpointsDueSoon_AndSkipTheRest()
    {
        var now = DateTime.UtcNow;
        var tag = new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)('g' + (c <= '9' ? c - '0' : c - 'a' + 10) % 20)).ToArray());
        var surname = $"Remind{tag}";
        int soonId, laterId, staleId, sentId;
        var trackedFamilyId = await ApplyAsync();   // a real family, since other tests read every tracker's family id
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var tracker = new FollowUpTracker
            {
                FamilyId = trackedFamilyId, Parent1Name = $"Tom {surname}", Parent2Name = $"Ann {surname}", DateOfLoss = now.AddDays(-20),
                Milestones =
                [
                    new() { Type = FollowUpMilestoneType.ThreeWeeks,   DueDate = now.AddDays(3) },
                    new() { Type = FollowUpMilestoneType.ThreeMonths,  DueDate = now.AddDays(40) },
                    new() { Type = FollowUpMilestoneType.SixMonths,    DueDate = now.AddDays(-60) },
                    new() { Type = FollowUpMilestoneType.ElevenMonths, DueDate = now.AddDays(2), BookSent = true },
                ],
            };
            db.FollowUpTrackers.Add(tracker);
            await db.SaveChangesAsync();
            soonId = tracker.Milestones[0].Id; laterId = tracker.Milestones[1].Id; staleId = tracker.Milestones[2].Id; sentId = tracker.Milestones[3].Id;
        }

        var sent = new List<(string To, string Subject, string Html)>();
        var notify = new Mock<INotificationService>();
        notify.Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Callback<string, string, string, string>((to, _, subject, html) => { lock (sent) sent.Add((to, subject, html)); })
              .ReturnsAsync(Result.Ok());
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:IntakeTeamEmails"] = "whitney@test.example.com,team@test.example.com",
        }).Build();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            await BereavementReminders.SendDueAsync(db, notify.Object, cfg, now);

            var milestones = await db.FollowUpMilestones.AsNoTracking().Where(m => new[] { soonId, laterId, staleId, sentId }.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
            Assert.NotNull(milestones[soonId].ReminderSentAt);
            Assert.Null(milestones[laterId].ReminderSentAt);    // too far away
            Assert.Null(milestones[staleId].ReminderSentAt);    // long overdue: left alone
            Assert.Null(milestones[sentId].ReminderSentAt);     // already handled
        }

        var mine = sent.Where(e => e.Html.Contains(surname)).ToList();
        Assert.Equal(new[] { "team@test.example.com", "whitney@test.example.com" }, mine.Select(e => e.To).Order().ToArray());
        Assert.All(mine, e => Assert.Contains("3 Weeks", e.Html));

        // A second check doesn't repeat it.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            await BereavementReminders.SendDueAsync(db, notify.Object, cfg, now);
        }
        Assert.Equal(2, sent.Count(e => e.Html.Contains(surname)));
    }

    // ── Email the family ──────────────────────────────────────────────────────

    [Fact]
    public async Task EmailTheFamily_SendsWhenDetailsLookWrong_AndLeavesANoteOnTheProfile()
    {
        var familyId = await ApplyAsync(zip: "606");
        var admin = await ClientForAsync("HQAdmin");

        var resp = await admin.PostAsJsonAsync($"/api/v1/families/{familyId}/request-details", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var note = await db.FamilyNotes.AsNoTracking().SingleAsync(n => n.FamilyId == familyId);
        Assert.Contains("your mailing address", note.Content);
    }

    [Fact]
    public async Task EmailTheFamily_IsRefused_WhenNothingLooksWrong_OrThereIsNoValidEmail()
    {
        var fine = await ApplyAsync();
        var noEmail = await ApplyAsync(zip: "606", email: "not-an-email");
        var admin = await ClientForAsync("HQAdmin");

        var nothing = await admin.PostAsJsonAsync($"/api/v1/families/{fine}/request-details", new { });
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);

        var bad = await admin.PostAsJsonAsync($"/api/v1/families/{noEmail}/request-details", new { });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("valid email", await bad.Content.ReadAsStringAsync());
    }

    // ── Grief support list and CRM column ─────────────────────────────────────

    [Fact]
    public async Task TheGriefSupportList_HasOnlyFamiliesWhoSaidYes()
    {
        var yes = await ApplyAsync(reason: "Stillbirth", grief: true);
        var no = await ApplyAsync(reason: "Stillbirth", grief: false);
        var unasked = await ApplyAsync(reason: "Infertility", grief: true);
        var admin = await ClientForAsync("HQAdmin");

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/families/grief-support");
        var ids = list.EnumerateArray().Select(f => f.GetProperty("id").GetInt32()).ToHashSet();

        Assert.Contains(yes, ids);
        Assert.DoesNotContain(no, ids);
        Assert.DoesNotContain(unasked, ids);
    }

    [Fact]
    public async Task TheCrmExport_AddsTheGriefColumnOnlyWhenAsked()
    {
        var yes = await ApplyAsync(reason: "InfantLoss", grief: true);
        var admin = await ClientForAsync("HQAdmin");

        var plain = await admin.GetStringAsync("/api/v1/export/families-crm");
        var header = plain.Split("\r\n")[0];
        Assert.Equal("Family name,Moms first name,Moms last name,Email address,Phone number,Street address,City,State,Zip,Country", header);

        var withGrief = await admin.GetStringAsync("/api/v1/export/families-crm?includeGrief=true");
        var lines = withGrief.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith(",Quarterly grief support", lines[0]);
        Assert.Contains(lines.Skip(1), l => l.EndsWith(",Yes"));
        Assert.True(yes > 0);
    }

    // ── My volunteer record ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateMyVolunteerRecord_MakesOne_OnlyOnce_AndItThenMatchesMyCases()
    {
        var email = $"newvol-{Guid.NewGuid():N}@test.com";
        var me = await ClientForAsync("ChapterStaff", email, await NewChapterAsync());   // its own chapter, so other tests' requests are not auto-assigned to this volunteer

        Assert.Equal(HttpStatusCode.NotFound, (await me.GetAsync("/api/v1/volunteers/me")).StatusCode);

        var created = await me.PostAsJsonAsync("/api/v1/volunteers/me", new { });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();

        var again = await me.PostAsJsonAsync("/api/v1/volunteers/me", new { });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first.GetProperty("id").GetInt32(), (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());

        var found = await me.GetFromJsonAsync<JsonElement>("/api/v1/volunteers/me");
        Assert.Equal(email, found.GetProperty("email").GetString());
    }

    // ── Startup repair ────────────────────────────────────────────────────────

    [Fact]
    public async Task Repair_MovesAssignedInProgressRequestsOutOfTheUnassignedStage()
    {
        var familyId = await ApplyAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var request = await db.Requests.SingleAsync(r => r.FamilyId == familyId);
        request.AssignedToId = 12345; request.AssignedTo = "Somebody"; request.Status = CaseStatus.InProgress; request.ProcessStage = ProcessStage.Unassigned;
        await db.SaveChangesAsync();

        Assert.True(await WorkflowDataRepair.RunAsync(db) >= 1);
        db.ChangeTracker.Clear();
        Assert.Equal(ProcessStage.Assigned, (await db.Requests.AsNoTracking().SingleAsync(r => r.Id == request.Id)).ProcessStage);
        Assert.Equal(0, await WorkflowDataRepair.RunAsync(db));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> ApplyAsync(string reason = "Infertility", bool? grief = null, string zip = "60601", string? email = null)
    {
        var chapter = Interlocked.Increment(ref _nextChapterId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            db.Chapters.Add(new Chapter { Id = chapter, Name = $"Ops Chapter {chapter}", City = "Testville", State = "IL",
                ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true });
            await db.SaveChangesAsync();
        }
        var tag = new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)('g' + (c <= '9' ? c - '0' : c - 'a' + 10) % 20)).ToArray());
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Ops{tag}", Parent2FirstName = "Ann", Parent2LastName = $"Ops{tag}",
                Email = email ?? $"ops-{tag}@test.example.com", Phone = "", StreetAddress = "1 Test St", City = "Testville", State = "IL",
                Zip = zip, Reason = reason, ChapterId = chapter, GriefSupportRequested = grief,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("familyId").GetInt32();
    }

    private async Task<int> NewChapterAsync()
    {
        var chapter = Interlocked.Increment(ref _nextChapterId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Chapters.Add(new Chapter { Id = chapter, Name = $"Ops Chapter {chapter}", City = "Testville", State = "IL",
            ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true });
        await db.SaveChangesAsync();
        return chapter;
    }

    private async Task<HttpClient> ClientForAsync(string role, string? email = null, int chapterId = 1)
    {
        email ??= $"ops-{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        const string password = "TestPass1Operations!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Ops", LastName = "Tester", Role = role, ChapterId = chapterId
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
