using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Where a public prayer care request ends up: the Kanban board, the unassigned queue or
/// a volunteer's assigned work, the duplicate-review hold, bereavement follow-up, and (what does
/// NOT happen) the Mother's Day mailing list.
/// Each test uses its own chapter so volunteers and requests can't leak between tests.
/// </summary>
[Collection("Integration")]
public class RequestRoutingTests
{
    private static int _nextChapterId = 6000;
    private readonly LotvApiFactory _factory;

    public RequestRoutingTests(LotvApiFactory factory) => _factory = factory;

    // ── The board ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NewRequest_AppearsOnTheBoard_InTheNewColumn_AndInTheUnassignedQueue_WhenNoVolunteerQualifies()
    {
        var chapter = await NewChapterAsync();
        var (_, requestId) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        var board = await GetAsync(admin, "/api/v1/requests");
        var card = board.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == requestId);
        Assert.Equal("New", card.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, card.GetProperty("assignedToId").ValueKind);

        var queue = await GetAsync(admin, "/api/v1/requests/queue");
        Assert.Contains(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == requestId);
    }

    [Fact]
    public async Task NewRequest_IsCreatedWithTheReasonChosenOnTheForm_InTheRightChapter()
    {
        var chapter = await NewChapterAsync();
        var (familyId, requestId) = await ApplyAsync(chapter, "PostnatalMedical");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var req = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        Assert.Equal(familyId, req.FamilyId);
        Assert.Equal(chapter, req.ChapterId);
        Assert.Equal(PackageReason.PostnatalMedical, req.Reason);
    }

    // ── Assigned vs unassigned work ───────────────────────────────────────────

    [Fact]
    public async Task NewRequest_IsAutoAssigned_ToAnActiveVolunteer_AndLeavesTheUnassignedQueue()
    {
        var chapter = await NewChapterAsync();
        var volunteerId = await AddVolunteerAsync(chapter, "Alice", "Assembler");
        var (_, requestId) = await ApplyAsync(chapter, "Miscarriage");
        var admin = await AdminClientAsync();

        var req = await GetAsync(admin, $"/api/v1/requests/{requestId}");
        Assert.Equal("InProgress", req.GetProperty("status").GetString());
        Assert.Equal(volunteerId, req.GetProperty("assignedToId").GetInt32());
        // The My Work Queue page matches the signed-in person's name against this text.
        Assert.Equal("Alice Assembler", req.GetProperty("assignedTo").GetString());

        var queue = await GetAsync(admin, "/api/v1/requests/queue");
        Assert.DoesNotContain(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == requestId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var assignment = await db.RequestAssignments.AsNoTracking().SingleAsync(a => a.RequestId == requestId);
        Assert.Equal(volunteerId, assignment.AssignedToId);
        Assert.Equal(AssignmentStatus.Pending, assignment.Status);
        Assert.True(await db.RequestActivities.AnyAsync(a => a.RequestId == requestId && a.ActivityType == ActivityType.Assigned));
    }

    [Fact]
    public async Task WhenSeveralVolunteersQualify_TheLeastBusyOneGetsTheRequest()
    {
        var chapter = await NewChapterAsync();
        await AddVolunteerAsync(chapter, "Busy", "Bee", activeCases: 4);
        var freeId = await AddVolunteerAsync(chapter, "Free", "Bird", activeCases: 0);
        var (_, requestId) = await ApplyAsync(chapter, "Stillbirth");

        var req = await GetAsync(await AdminClientAsync(), $"/api/v1/requests/{requestId}");
        Assert.Equal(freeId, req.GetProperty("assignedToId").GetInt32());
    }

    [Theory]
    [InlineData("full")]
    [InlineData("inactive")]
    [InlineData("wrong-role")]
    [InlineData("other-chapter")]
    public async Task VolunteersWhoDontQualify_AreSkipped_AndTheRequestStaysUnassigned(string why)
    {
        var chapter = await NewChapterAsync();
        var other = await NewChapterAsync();
        switch (why)
        {
            case "full":          await AddVolunteerAsync(chapter, "Full", "Load", activeCases: 6); break;   // chapter max is 6
            case "inactive":      await AddVolunteerAsync(chapter, "Gone", "Away", status: VolunteerStatus.Inactive); break;
            case "wrong-role":    await AddVolunteerAsync(chapter, "Only", "Driver", role: VolunteerRole.Driver); break;
            case "other-chapter": await AddVolunteerAsync(other, "Far", "Away"); break;
        }
        var (_, requestId) = await ApplyAsync(chapter, "InfantLoss");
        var admin = await AdminClientAsync();

        var req = await GetAsync(admin, $"/api/v1/requests/{requestId}");
        Assert.Equal("New", req.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, req.GetProperty("assignedToId").ValueKind);
        var queue = await GetAsync(admin, "/api/v1/requests/queue");
        Assert.Contains(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == requestId);
    }

    // ── Possible duplicates ───────────────────────────────────────────────────

    [Fact]
    public async Task ASecondRequestFromTheSameEmail_IsHeldForReview_NotAssignedAndNotInTheQueue_ButStillOnTheBoard()
    {
        var chapter = await NewChapterAsync();
        await AddVolunteerAsync(chapter, "Alice", "Assembler", activeCases: 0);
        var email = $"dup-{Guid.NewGuid():N}@test.example.com";
        await ApplyAsync(chapter, "Infertility", email: email);           // first one: assigned
        var (_, dupRequestId) = await ApplyAsync(chapter, "Infertility", email: email);
        var admin = await AdminClientAsync();

        var req = await GetAsync(admin, $"/api/v1/requests/{dupRequestId}");
        Assert.True(req.GetProperty("needsDuplicateReview").GetBoolean());
        Assert.Equal("New", req.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, req.GetProperty("assignedToId").ValueKind);

        var queue = await GetAsync(admin, "/api/v1/requests/queue");
        Assert.DoesNotContain(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == dupRequestId);

        var board = await GetAsync(admin, "/api/v1/requests");
        Assert.Contains(board.EnumerateArray(), r => r.GetProperty("id").GetInt32() == dupRequestId);

        var review = await GetAsync(admin, "/api/v1/families/duplicate-review");
        Assert.Contains(review.EnumerateArray(), r => r.GetProperty("id").GetInt32() == dupRequestId);
    }

    // ── Historical families ───────────────────────────────────────────────────

    [Fact]
    public async Task HistoricalFamilies_AreKeptOffTheBoard()
    {
        var chapter = await NewChapterAsync();
        var (familyId, requestId) = await ApplyAsync(chapter, "PastLoss");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            (await db.Families.SingleAsync(f => f.Id == familyId)).IsHistorical = true;
            await db.SaveChangesAsync();
        }

        var board = await GetAsync(await AdminClientAsync(), "/api/v1/requests");

        Assert.DoesNotContain(board.EnumerateArray(), r => r.GetProperty("id").GetInt32() == requestId);
    }

    // ── Bereavement follow-up ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Miscarriage")]
    [InlineData("Stillbirth")]
    [InlineData("InfantLoss")]
    public async Task ALoss_WithADate_CreatesAFollowUpTracker_WithTheFourMilestones(string reason)
    {
        var chapter = await NewChapterAsync();
        var loss = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var (familyId, _) = await ApplyAsync(chapter, reason, dateOfLoss: loss);

        var trackers = await GetAsync(await AdminClientAsync(), "/api/v1/follow-up-trackers");
        var tracker = trackers.EnumerateArray().Single(t => t.GetProperty("familyId").GetInt32() == familyId);

        Assert.Equal(reason, tracker.GetProperty("reason").GetString());
        var due = tracker.GetProperty("milestones").EnumerateArray()
            .ToDictionary(m => m.GetProperty("type").GetString()!, m => m.GetProperty("dueDate").GetDateTime().Date);
        Assert.Equal(4, due.Count);
        Assert.Equal(loss.AddDays(21).Date,   due["ThreeWeeks"]);
        Assert.Equal(loss.AddMonths(3).Date,  due["ThreeMonths"]);
        Assert.Equal(loss.AddMonths(6).Date,  due["SixMonths"]);
        Assert.Equal(loss.AddMonths(11).Date, due["ElevenMonths"]);
    }

    [Theory]
    [InlineData("Infertility")]
    [InlineData("PrenatalDiagnosis")]
    [InlineData("PostnatalMedical")]
    public async Task NoDateOfLoss_MeansNoFollowUpTracker(string reason)
    {
        var chapter = await NewChapterAsync();
        var (familyId, _) = await ApplyAsync(chapter, reason);

        var trackers = await GetAsync(await AdminClientAsync(), "/api/v1/follow-up-trackers");

        Assert.DoesNotContain(trackers.EnumerateArray(), t => t.GetProperty("familyId").GetInt32() == familyId);
    }

    // ── Mother's Day mailing list ─────────────────────────────────────────────

    private static int CurrentCycle => MothersDayCycle.YearFor(DateTime.UtcNow);

    private static async Task<List<JsonElement>> MailingEntriesForAsync(HttpClient admin, int familyId) =>
        (await GetAsync(admin, $"/api/v1/mailing-list?year={CurrentCycle}")).EnumerateArray()
            .Where(m => m.GetProperty("familyId").ValueKind == JsonValueKind.Number && m.GetProperty("familyId").GetInt32() == familyId)
            .ToList();

    [Fact]
    public async Task NewRequest_JoinsTheMothersDayMailing_ForTheCurrentCycle_WithTheMotherAndFather()
    {
        var chapter = await NewChapterAsync();
        var (familyId, _) = await ApplyAsync(chapter, "Miscarriage", dateOfLoss: DateTime.UtcNow.AddDays(-30));

        var entry = Assert.Single(await MailingEntriesForAsync(await AdminClientAsync(), familyId));

        // The intake form records the wife as the second parent - she is the mother on the card.
        Assert.StartsWith("Ann Route", entry.GetProperty("motherName").GetString());
        Assert.StartsWith("Tom Route", entry.GetProperty("fatherName").GetString());
        Assert.Equal("Testville", entry.GetProperty("city").GetString());
        Assert.Equal(CurrentCycle, entry.GetProperty("year").GetInt32());
        Assert.False(entry.GetProperty("flaggedForReview").GetBoolean());
        Assert.False(entry.GetProperty("sent").GetBoolean());
    }

    [Fact]
    public async Task ASingleParentRequest_UsesThatParentAsTheMother_WithNoFather()
    {
        var chapter = await NewChapterAsync();
        var (familyId, _) = await ApplyAsync(chapter, "Infertility", withSecondParent: false);

        var entry = Assert.Single(await MailingEntriesForAsync(await AdminClientAsync(), familyId));

        Assert.StartsWith("Tom Route", entry.GetProperty("motherName").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fatherName").ValueKind);
    }

    [Fact]
    public async Task AnotherRequestForTheSameFamily_DoesNotAddASecondEntry()
    {
        var chapter = await NewChapterAsync();
        var (familyId, _) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        var staffRequest = await admin.PostAsJsonAsync("/api/v1/requests", new
        {
            FamilyId = familyId, ChapterId = chapter, Reason = "Infertility", Category = "ResourceProvision",
        });
        Assert.Equal(HttpStatusCode.Created, staffRequest.StatusCode);

        Assert.Single(await MailingEntriesForAsync(admin, familyId));
    }

    [Fact]
    public async Task AnIncompleteAddress_IsFlaggedForReview()
    {
        var chapter = await NewChapterAsync();
        var (familyId, _) = await ApplyAsync(chapter, "Infertility", street: "");

        var entry = Assert.Single(await MailingEntriesForAsync(await AdminClientAsync(), familyId));

        Assert.True(entry.GetProperty("flaggedForReview").GetBoolean());
        Assert.Contains("Address is incomplete", entry.GetProperty("reviewNote").GetString());
    }

    [Fact]
    public async Task APossibleDuplicate_IsFlagged_ThenUnflaggedWhenStaffConfirmItIsANewFamily()
    {
        var chapter = await NewChapterAsync();
        var email = $"dup-{Guid.NewGuid():N}@test.example.com";
        await ApplyAsync(chapter, "Infertility", email: email);
        var (dupFamilyId, dupRequestId) = await ApplyAsync(chapter, "Infertility", email: email);
        var admin = await AdminClientAsync();

        var flagged = Assert.Single(await MailingEntriesForAsync(admin, dupFamilyId));
        Assert.True(flagged.GetProperty("flaggedForReview").GetBoolean());
        Assert.Contains(MothersDayMailing.DuplicateNotePrefix, flagged.GetProperty("reviewNote").GetString());

        var resolve = await admin.PostAsJsonAsync($"/api/v1/families/duplicate-review/{dupRequestId}/resolve", new { Action = "confirm-new" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        var cleared = Assert.Single(await MailingEntriesForAsync(admin, dupFamilyId));
        Assert.False(cleared.GetProperty("flaggedForReview").GetBoolean());
    }

    [Fact]
    public async Task APossibleDuplicate_MergedIntoTheExistingFamily_LosesItsExtraEntry()
    {
        var chapter = await NewChapterAsync();
        var email = $"dup-{Guid.NewGuid():N}@test.example.com";
        var (firstFamilyId, _) = await ApplyAsync(chapter, "Infertility", email: email);
        var (dupFamilyId, dupRequestId) = await ApplyAsync(chapter, "Infertility", email: email);
        var admin = await AdminClientAsync();

        var resolve = await admin.PostAsJsonAsync($"/api/v1/families/duplicate-review/{dupRequestId}/resolve", new { Action = "merge" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        Assert.Empty(await MailingEntriesForAsync(admin, dupFamilyId));
        Assert.Single(await MailingEntriesForAsync(admin, firstFamilyId));      // the original family keeps its card
    }

    [Theory]
    [InlineData("2026-01-15", 2026)]   // before Mother's Day 2026 (May 10)
    [InlineData("2026-05-10", 2026)]   // on the day
    [InlineData("2026-05-11", 2027)]   // the day after starts the next cycle
    [InlineData("2026-09-23", 2027)]
    [InlineData("2027-05-08", 2027)]   // Mother's Day 2027 is May 9
    public void MothersDayCycle_RunsFromOneMothersDayToTheNext(string date, int expectedYear)
    {
        Assert.Equal(expectedYear, MothersDayCycle.YearFor(DateTime.Parse(date)));
    }

    [Fact]
    public async Task MailingList_ListsEntries_ForTheYear_AndStaffCanFlagAndMarkThemSent()
    {
        var year = 3000 + Random.Shared.Next(1, 900);                  // a year no other test uses
        int entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var entry = new MailingListEntry
            {
                Year = year, MotherName = "Mary Mailing", FatherName = "Dan Mailing", StreetAddress = "1 Card St",
                City = "Chicago", State = "IL", Zip = "60601", Country = "United States",
            };
            db.MailingListEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }
        var admin = await AdminClientAsync();

        var listed = await GetAsync(admin, $"/api/v1/mailing-list?year={year}");
        Assert.Equal("Mary Mailing", listed.EnumerateArray().Single().GetProperty("motherName").GetString());

        var flagged = await admin.PutAsJsonAsync($"/api/v1/mailing-list/{entryId}/flag", new { Flagged = true, Note = "check address" });
        Assert.Equal(HttpStatusCode.OK, flagged.StatusCode);
        var onlyFlagged = await GetAsync(admin, $"/api/v1/mailing-list?year={year}&flagged=true");
        Assert.Single(onlyFlagged.EnumerateArray());

        var sent = await admin.PutAsJsonAsync($"/api/v1/mailing-list/{entryId}/sent", new { Sent = true });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var notYetSent = await GetAsync(admin, $"/api/v1/mailing-list?year={year}&sent=false");
        Assert.Empty(notYetSent.EnumerateArray());
    }

    [Fact]
    public async Task MailingListAndFollowUpTrackers_RequireStaffSignIn()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/mailing-list")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/follow-up-trackers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/requests/queue")).StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // ── Unassigning ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Unassign_ReturnsTheRequestToNew_AndBackIntoTheUnassignedQueue()
    {
        var chapter = await NewChapterAsync();
        var (_, requestId) = await ApplyAsync(chapter, "Miscarriage");
        var volunteerId = await AddVolunteerAsync(chapter, "Una", "Ssign");
        var admin = await AdminClientAsync();

        var assign = await admin.PutAsJsonAsync($"/api/v1/requests/{requestId}/assign", new { VolunteerId = volunteerId });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var resp = await admin.PutAsJsonAsync($"/api/v1/requests/{requestId}/unassign", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var req = await db.Requests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        Assert.Null(req.AssignedToId);
        Assert.Null(req.AssignedTo);
        Assert.Equal(CaseStatus.New, req.Status);
        Assert.Equal(ProcessStage.Unassigned, req.ProcessStage);
        Assert.Contains(await db.RequestActivities.Where(a => a.RequestId == requestId).ToListAsync(),
            a => a.ActivityType == ActivityType.Unassigned && a.OldValue == "Una Ssign");

        var queue = await GetAsync(admin, "/api/v1/requests/queue");
        Assert.Contains(queue.EnumerateArray(), r => r.GetProperty("id").GetInt32() == requestId);
    }

    [Fact]
    public async Task Unassign_IsRejected_WhenNobodyIsAssigned()
    {
        var chapter = await NewChapterAsync();
        var (_, requestId) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        var resp = await admin.PutAsJsonAsync($"/api/v1/requests/{requestId}/unassign", new { });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task<int> NewChapterAsync()
    {
        var id = Interlocked.Increment(ref _nextChapterId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Chapters.Add(new Chapter
        {
            Id = id, Name = $"Test Chapter {id}", City = "Testville", State = "IL",
            ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> AddVolunteerAsync(int chapterId, string first, string last, int activeCases = 0,
        VolunteerStatus status = VolunteerStatus.Active, VolunteerRole role = VolunteerRole.PackageAssembler)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var v = new Volunteer
        {
            FirstName = first, LastName = last, Email = $"{first}.{last}.{Guid.NewGuid():N}@test.example.com".ToLowerInvariant(),
            Role = role, Status = status, ChapterId = chapterId, ActiveCases = activeCases, JoinedDate = DateTime.UtcNow,
        };
        db.Volunteers.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    /// <summary>Submits the public form's payload and returns the new family and request ids.</summary>
    private async Task<(int FamilyId, int RequestId)> ApplyAsync(int chapterId, string reason,
        string? email = null, DateTime? dateOfLoss = null, bool withSecondParent = true, string? street = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Route{tag}",
                Parent2FirstName = withSecondParent ? "Ann" : null, Parent2LastName = withSecondParent ? $"Route{tag}" : null,
                Email = email ?? $"route-{tag}@test.example.com", Phone = "",
                StreetAddress = street ?? $"{Random.Shared.Next(100, 999)} Test St", City = "Testville", State = "IL",
                Zip = $"6{Random.Shared.Next(1000, 9999)}", Reason = reason, ChapterId = chapterId,
                DateOfLoss = dateOfLoss,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("familyId").GetInt32(), body.GetProperty("requestId").GetInt32());
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        var resp = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"routing-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Route!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Routing", LastName = "Tester", Role = "HQAdmin", ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
