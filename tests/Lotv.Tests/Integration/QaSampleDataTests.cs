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
/// The QA sample data: what it adds, that it can be loaded once and removed completely without touching real data,
/// and that it can never email, mail or export a sample family. Each test removes what it loads, since the
/// sample rows are recognised by their reserved .invalid addresses across the whole database.
/// </summary>
[Collection("Integration")]
public class QaSampleDataTests
{
    private static int _nextChapterId = 9800;
    private readonly LotvApiFactory _factory;

    public QaSampleDataTests(LotvApiFactory factory) => _factory = factory;

    private async Task<T> WithSampleAsync<T>(Func<LotvDbContext, Task<T>> body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        await QaSampleData.RemoveAsync(db);
        await EnsureAChapterAsync(db);
        try
        {
            var load = await QaSampleData.LoadAsync(db);
            Assert.True(load.Loaded, load.Message);
            return await body(db);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await QaSampleData.RemoveAsync(db);
        }
    }

    private static async Task EnsureAChapterAsync(LotvDbContext db)
    {
        if (await db.Chapters.AnyAsync()) return;
        db.Chapters.Add(new Chapter { Id = Interlocked.Increment(ref _nextChapterId), Name = "QA Chapter", City = "Testville", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
        await db.SaveChangesAsync();
    }

    // ── What it adds ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Loading_AddsEveryScenarioWorthChecking()
    {
        await WithSampleAsync(async db =>
        {
            var families = await db.Families.AsNoTracking().Where(f => f.Email.EndsWith(".invalid")).ToListAsync();
            var familyIds = families.Select(f => f.Id).ToList();
            var requests = await db.Requests.AsNoTracking().Where(r => familyIds.Contains(r.FamilyId)).ToListAsync();

            Assert.All(families, f => Assert.EndsWith("@" + QaSampleData.Domain, f.Email));
            Assert.All(families, f => Assert.Contains("QA sample", f.ContactNotes));

            // Every status and the stages in between
            foreach (var status in new[] { CaseStatus.New, CaseStatus.InProgress, CaseStatus.AwaitingShipment, CaseStatus.Shipped, CaseStatus.Fulfilled, CaseStatus.OnHold })
                Assert.Contains(requests, r => r.Status == status);
            Assert.Contains(requests, r => r.ProcessStage == ProcessStage.Confirmed);
            Assert.Contains(requests, r => r.ProcessStage == ProcessStage.Packing);
            Assert.Contains(requests, r => r.Status == CaseStatus.New && r.AssignedToId == null && !r.NeedsDuplicateReview);   // in the queue
            Assert.Contains(requests, r => !r.IsForSelf && r.ReferrerName != null);                                            // referred

            // The possible duplicate points at the family it may duplicate
            var dup = Assert.Single(requests, r => r.NeedsDuplicateReview);
            Assert.Contains(familyIds, id => id == dup.PossibleDuplicateFamilyId);

            // Grief support both ways, a bad zip that trips the data check, and a single parent
            Assert.Contains(families, f => f.GriefSupportRequested == true);
            Assert.Contains(families, f => f.GriefSupportRequested == false);
            Assert.Contains(families, f => FamilyDataQuality.Check(f).NeedsAttention);
            var single = Assert.Single(families, f => string.IsNullOrWhiteSpace(f.Parent2FirstName));

            // Follow-up trackers for the losses, and card lists that are flagged "do not mail"
            Assert.NotEmpty(await db.FollowUpTrackers.Where(t => familyIds.Contains(t.FamilyId!.Value)).ToListAsync());
            var entries = await db.MailingListEntries.AsNoTracking().Where(m => familyIds.Contains(m.FamilyId!.Value)).ToListAsync();
            Assert.All(entries, m => { Assert.True(m.FlaggedForReview); Assert.Contains("QA sample: do not mail", m.ReviewNote); });
            Assert.Contains(entries, m => m.FamilyId == single.Id && m.Kind == MailingKind.MothersDay);
            Assert.DoesNotContain(entries, m => m.FamilyId == single.Id && m.Kind == MailingKind.FathersDay);

            // Volunteers carry real workloads
            var volunteers = await db.Volunteers.AsNoTracking().Where(v => v.Email.EndsWith(".invalid")).ToListAsync();
            Assert.Equal(5, volunteers.Count);
            Assert.All(volunteers, v => Assert.Equal(requests.Count(r => r.AssignedToId == v.Id && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled), v.ActiveCases));
            return 0;
        });
    }

    [Fact]
    public async Task WhenSusanIsAVolunteer_ThreeSampleCasesLandInHerQueue_AndRemovingThemClearsIt()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        await QaSampleData.RemoveAsync(db);
        await EnsureAChapterAsync(db);
        db.Volunteers.RemoveRange(db.Volunteers.Where(v => v.Email == "susan@wte.net"));
        await db.SaveChangesAsync();
        Assert.Equal(1, await StaffAccountProvisioning.EnsureVolunteerRecordsAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance));
        var susan = await db.Volunteers.AsNoTracking().SingleAsync(v => v.Email == "susan@wte.net");
        try
        {
            Assert.True((await QaSampleData.LoadAsync(db)).Loaded);
            db.ChangeTracker.Clear();

            var hers = await db.Requests.AsNoTracking().Where(r => r.AssignedToId == susan.Id).ToListAsync();
            Assert.Equal(3, hers.Count);
            Assert.Contains(hers, r => r.ProcessStage == ProcessStage.Assigned);
            Assert.Contains(hers, r => r.ProcessStage == ProcessStage.Confirmed);
            Assert.Contains(hers, r => r.ProcessStage == ProcessStage.Packing);
            Assert.Equal(3, (await db.Volunteers.AsNoTracking().SingleAsync(v => v.Id == susan.Id)).ActiveCases);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await QaSampleData.RemoveAsync(db);
        }
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.Volunteers.AsNoTracking().SingleAsync(v => v.Id == susan.Id)).ActiveCases);
        Assert.Equal(0, await db.Requests.CountAsync(r => r.AssignedToId == susan.Id));
    }

    [Fact]
    public async Task EveryKindOfRequest_HasOneWaitingOneBeingWorkedAndOneFinished()
    {
        await WithSampleAsync(async db =>
        {
            var familyIds = await db.Families.AsNoTracking().Where(f => f.Email.EndsWith(".invalid")).Select(f => f.Id).ToListAsync();
            var requests = await db.Requests.AsNoTracking().Where(r => familyIds.Contains(r.FamilyId)).ToListAsync();

            foreach (var reason in Enum.GetValues<PackageReason>())
            {
                var ofKind = requests.Where(r => r.Reason == reason && !r.NeedsDuplicateReview).ToList();
                Assert.True(ofKind.Count >= 3, $"{reason} has only {ofKind.Count} sample requests");
                Assert.Contains(ofKind, r => r.Status == CaseStatus.New && r.AssignedToId == null);                                                   // waiting in the queue
                Assert.Contains(ofKind, r => r.Status is CaseStatus.InProgress or CaseStatus.AwaitingShipment && r.AssignedToId != null);           // being worked
                Assert.Contains(ofKind, r => r.Status is CaseStatus.Shipped or CaseStatus.Fulfilled);                                               // finished
            }

            // Every stage of the process shows up somewhere
            foreach (var stage in Enum.GetValues<ProcessStage>())
                Assert.Contains(requests, r => r.ProcessStage == stage);

            // Losses have bereavement follow-ups; the urgent kind is flagged urgent
            Assert.Contains(requests, r => r.Reason == PackageReason.PrenatalLifeLimitingDiagnosis && r.Priority == RequestPriority.Urgent);
            var lossFamilies = await db.Families.AsNoTracking().Where(f => familyIds.Contains(f.Id) && f.DateOfLoss != null).Select(f => f.Id).ToListAsync();
            var trackerFamilies = await db.FollowUpTrackers.AsNoTracking().Where(t => t.FamilyId != null && familyIds.Contains(t.FamilyId.Value)).Select(t => t.FamilyId!.Value).ToListAsync();
            Assert.All(lossFamilies, id => Assert.Contains(id, trackerFamilies));

            // No sample family trips the data check by accident: only the deliberate bad-zip one does
            var flagged = await db.Families.AsNoTracking().Where(f => familyIds.Contains(f.Id)).ToListAsync();
            Assert.Single(flagged, f => FamilyDataQuality.Check(f).NeedsAttention);
            return 0;
        });
    }

    [Fact]
    public async Task EnsureLoaded_LoadsOnlyWhenNothingIsLoaded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        await QaSampleData.RemoveAsync(db);
        await EnsureAChapterAsync(db);
        try
        {
            Assert.True(await QaSampleData.EnsureLoadedAsync(db));    // empty: loads
            var status = await QaSampleData.GetStatusAsync(db);
            Assert.False(await QaSampleData.EnsureLoadedAsync(db));   // already there: nothing happens
            Assert.Equal(status, await QaSampleData.GetStatusAsync(db));
        }
        finally { db.ChangeTracker.Clear(); await QaSampleData.RemoveAsync(db); }
    }

    [Fact]
    public async Task SampleVolunteers_SayTheyAreSamples()
    {
        await WithSampleAsync(async db =>
        {
            var names = await db.Volunteers.AsNoTracking().Where(v => v.Email.EndsWith(".invalid")).Select(v => v.LastName).ToListAsync();
            Assert.Equal(5, names.Count);
            Assert.All(names, n => Assert.EndsWith("(sample)", n));
            return 0;
        });
    }

    [Fact]
    public async Task LoadingTwice_DoesNothing()
    {
        await WithSampleAsync(async db =>
        {
            var before = await QaSampleData.GetStatusAsync(db);
            var second = await QaSampleData.LoadAsync(db);
            Assert.False(second.Loaded);
            Assert.Equal(before, await QaSampleData.GetStatusAsync(db));
            return 0;
        });
    }

    // ── Removing it ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Removing_LeavesNothingBehind_AndNeverTouchesRealData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        await QaSampleData.RemoveAsync(db);
        await EnsureAChapterAsync(db);

        var chapter = await db.Chapters.OrderBy(c => c.Id).FirstAsync();
        var real = new Family { Parent1FirstName = "Realish", Parent1LastName = "Person", Email = "real.person@test.example.com", StreetAddress = "1 Real St", City = "Chicago", State = "IL", Zip = "60601", ChapterId = chapter.Id };
        db.Families.Add(real);
        await db.SaveChangesAsync();
        var realRequest = new PackageRequest { FamilyId = real.Id, ChapterId = chapter.Id, Reason = PackageReason.Infertility, Status = CaseStatus.New };
        db.Requests.Add(realRequest);
        await db.SaveChangesAsync();
        var realVolunteer = new Volunteer { FirstName = "Real", LastName = "Volunteer", Email = "real.volunteer@test.example.com", ChapterId = chapter.Id, JoinedDate = DateTime.UtcNow, Role = VolunteerRole.PackageAssembler };
        db.Volunteers.Add(realVolunteer);
        await db.SaveChangesAsync();

        Assert.True((await QaSampleData.LoadAsync(db)).Loaded);

        // Trouble: a real request was held as a possible duplicate of a sample family, and another was handed to a sample volunteer.
        var sampleFamily = await db.Families.FirstAsync(f => f.Email.EndsWith(".invalid"));
        var sampleVolunteer = await db.Volunteers.FirstAsync(v => v.Email.EndsWith(".invalid"));
        var heldRequest = new PackageRequest { FamilyId = real.Id, ChapterId = chapter.Id, Reason = PackageReason.Miscarriage, Status = CaseStatus.New, NeedsDuplicateReview = true, PossibleDuplicateFamilyId = sampleFamily.Id };
        var handedOver = new PackageRequest { FamilyId = real.Id, ChapterId = chapter.Id, Reason = PackageReason.Stillbirth, Status = CaseStatus.InProgress, AssignedToId = sampleVolunteer.Id, AssignedTo = "Sample" };
        db.Requests.AddRange(heldRequest, handedOver);
        await db.SaveChangesAsync();

        var after = await QaSampleData.RemoveAsync(db);
        db.ChangeTracker.Clear();

        Assert.Equal(new QaSampleData.Status(false, 0, 0, 0), after);
        Assert.Equal(0, await db.Families.CountAsync(f => f.Email.EndsWith(".invalid")));
        Assert.Equal(0, await db.Volunteers.CountAsync(v => v.Email.EndsWith(".invalid")));
        Assert.Equal(0, await db.MailingListEntries.CountAsync(m => m.ReviewNote != null && m.ReviewNote.StartsWith("QA sample")));
        Assert.Equal(0, await db.FollowUpTrackers.CountAsync(t => t.Email != null && t.Email.EndsWith(".invalid")));
        Assert.Equal(0, await db.RequestActivities.CountAsync(a => a.ActorId == "qa-sample"));
        Assert.Equal(0, await db.RequestNotes.CountAsync(n => n.AuthorId == "qa-sample"));
        Assert.Equal(0, await db.RequestAssignments.CountAsync(a => a.AssignedById == "qa-sample"));

        // The real family, volunteer and requests are all still there.
        Assert.NotNull(await db.Families.FindAsync(real.Id));
        Assert.NotNull(await db.Volunteers.FindAsync(realVolunteer.Id));
        Assert.NotNull(await db.Requests.FindAsync(realRequest.Id));

        var released = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == heldRequest.Id);
        Assert.False(released.NeedsDuplicateReview);
        Assert.Null(released.PossibleDuplicateFamilyId);

        var back = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == handedOver.Id);
        Assert.Null(back.AssignedToId);
        Assert.Equal(CaseStatus.New, back.Status);
    }

    // ── It can never reach a real person ──────────────────────────────────────

    [Fact]
    public async Task TheCrmExport_LeavesSampleFamiliesOut()
    {
        await WithSampleAsync(async db =>
        {
            var admin = await ClientForAsync("HQAdmin");
            var csv = await admin.GetStringAsync("/api/v1/export/families-crm");
            Assert.DoesNotContain(".invalid", csv);
            Assert.DoesNotContain("QA sample", csv);
            return 0;
        });
    }

    [Fact]
    public async Task NoEmailIsSentToAnInvalidAddress()
    {
        var handler = new CountingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SocketLabs:ServerId"] = "12345", ["SocketLabs:ApiKey"] = "k",
        }).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        var service = new NotificationService(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationService>.Instance, factory.Object);

        var result = await service.SendEmailAsync($"someone@{QaSampleData.Domain}", "Someone", "Hello", "<p>Hi</p>");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TheTeamAndFamilyAreNotEmailedAboutASampleRequest()
    {
        await WithSampleAsync(async db =>
        {
            var request = await db.Requests.Include(r => r.Family).FirstAsync(r => r.Family!.Email.EndsWith(".invalid"));
            var notify = new Mock<INotificationService>(MockBehavior.Strict);   // any call fails the test
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Notifications:IntakeTeamEmails"] = "whitney@test.example.com" }).Build();

            RequestNotifier.Shipped(notify.Object, cfg, request);
            RequestNotifier.Completed(notify.Object, cfg, request);
            RequestNotifier.NewRequest(notify.Object, cfg, request, request.Family!, null, null, null);
            return 0;
        });
    }

    [Fact]
    public async Task TheBereavementReminderLeavesSampleFamiliesOut()
    {
        await WithSampleAsync(async db =>
        {
            // Make a sample touchpoint due right now
            var milestone = await db.FollowUpMilestones.Include(m => m.FollowUpTracker)
                .FirstAsync(m => m.FollowUpTracker!.Email!.EndsWith(".invalid") && !m.BookSent);
            milestone.DueDate = DateTime.UtcNow.AddDays(2);
            await db.SaveChangesAsync();

            var subjects = new List<string>();
            var htmls = new List<string>();
            var notify = new Mock<INotificationService>();
            notify.Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Callback<string, string, string, string>((_, _, s, h) => { subjects.Add(s); htmls.Add(h); }).ReturnsAsync(Result.Ok());
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Notifications:IntakeTeamEmails"] = "whitney@test.example.com" }).Build();

            await BereavementReminders.SendDueAsync(db, notify.Object, cfg, DateTime.UtcNow);

            Assert.DoesNotContain(htmls, h => h.Contains(milestone.FollowUpTracker!.Parent1Name));
            db.ChangeTracker.Clear();
            Assert.Null((await db.FollowUpMilestones.AsNoTracking().SingleAsync(m => m.Id == milestone.Id)).ReminderSentAt);
            return 0;
        });
    }

    // ── The endpoints ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TheEndpoints_LoadAndRemove_ForHqAdminsOnly()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            await QaSampleData.RemoveAsync(db);
            await EnsureAChapterAsync(db);
        }
        var admin = await ClientForAsync("HQAdmin");
        var staff = await ClientForAsync("ChapterStaff");
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/qa-sample-data")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync("/api/v1/qa-sample-data", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync("/api/v1/qa-sample-data")).StatusCode);

            var empty = await admin.GetFromJsonAsync<JsonElement>("/api/v1/qa-sample-data");
            Assert.False(empty.GetProperty("loaded").GetBoolean());

            var load = await admin.PostAsync("/api/v1/qa-sample-data", null);
            Assert.Equal(HttpStatusCode.OK, load.StatusCode);
            var loaded = await admin.GetFromJsonAsync<JsonElement>("/api/v1/qa-sample-data");
            Assert.True(loaded.GetProperty("loaded").GetBoolean());
            Assert.Equal(39, loaded.GetProperty("families").GetInt32());

            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/v1/qa-sample-data", null)).StatusCode);   // already loaded
        }
        finally
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync("/api/v1/qa-sample-data")).StatusCode);
        }
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/v1/qa-sample-data")).GetProperty("loaded").GetBoolean());
    }

    // ── Cases badge ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SidebarOpenCasesCount_MatchesTheCasesPage_AndSkipsRequestsHeldForDuplicateReview()
    {
        await WithSampleAsync(async db =>
        {
            var expected = await db.Requests.CountAsync(r => !r.NeedsDuplicateReview
                && (r.Status == CaseStatus.New || r.Status == CaseStatus.InProgress || r.Status == CaseStatus.AwaitingShipment));
            var held = await db.Requests.CountAsync(r => r.NeedsDuplicateReview && r.Status == CaseStatus.New);
            Assert.True(held > 0, "the sample data should include a request held for duplicate review");

            var admin = await ClientForAsync("HQAdmin", chapterId: null);   // an HQ admin isn't tied to one chapter
            var stats = await admin.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/stats");
            Assert.Equal(expected, stats.GetProperty("openCases").GetInt32());

            // The Unassigned Queue badge equals what the queue page lists.
            var queue = await admin.GetFromJsonAsync<JsonElement>("/api/v1/requests/queue");
            Assert.True(queue.GetArrayLength() > 0, "the sample data should leave requests waiting in the queue");
            Assert.Equal(queue.GetArrayLength(), stats.GetProperty("unassignedQueue").GetInt32());
            return 0;
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ErrorCode\":\"Success\"}") });
        }
    }

    private async Task<HttpClient> ClientForAsync(string role, int? chapterId = 1)
    {
        var client = _factory.CreateClient();
        var email = $"qa-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Sample!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = password, FirstName = "Qa", LastName = "Tester", Role = role, ChapterId = chapterId });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
