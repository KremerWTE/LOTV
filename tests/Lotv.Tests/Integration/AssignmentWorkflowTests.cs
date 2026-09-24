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
/// Routing rules, case counts that stay correct, accept -> Confirmed, My Work Queue by account,
/// and the per-family request filter.
/// Each test uses its own chapter so volunteers and requests can't leak between tests.
/// </summary>
[Collection("Integration")]
public class AssignmentWorkflowTests
{
    private static int _nextChapterId = 9000;
    private readonly LotvApiFactory _factory;

    public AssignmentWorkflowTests(LotvApiFactory factory) => _factory = factory;

    // ── Routing rules ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ARule_SendsMatchingRequestsToItsVolunteer_AndOthersAreAssignedAutomatically()
    {
        var chapter = await NewChapterAsync();
        var auto = await AddVolunteerAsync(chapter, "Auto", "Picked");
        // A driver is never picked by automatic assignment, so a request landing on them can only be the rule.
        var specialist = await AddVolunteerAsync(chapter, "Sam", "Specialist", role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await CreateRuleAsync(admin, "Losses go to Sam", new { Reasons = "Stillbirth,InfantLoss", AssignToVolunteerIds = specialist.ToString(), ChapterId = chapter });

        var (_, stillbirth) = await ApplyAsync(chapter, "Stillbirth");
        var (_, infertility) = await ApplyAsync(chapter, "Infertility");

        var routed = await RequestAsync(stillbirth);
        Assert.Equal(specialist, routed.AssignedToId);
        var assignment = await LatestAssignmentAsync(stillbirth);
        Assert.Equal("Rule: Losses go to Sam", assignment.AssignedByName);

        Assert.Equal(auto, (await RequestAsync(infertility)).AssignedToId);
    }

    [Fact]
    public async Task Rules_AreTriedInOrder_AndConditionsMustAllMatch()
    {
        var chapter = await NewChapterAsync();
        var first = await AddVolunteerAsync(chapter, "First", "Rule", role: VolunteerRole.Driver);
        var second = await AddVolunteerAsync(chapter, "Second", "Rule", role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await CreateRuleAsync(admin, "Wisconsin only", new { Priority = 1, State = "WI", AssignToVolunteerIds = first.ToString(), ChapterId = chapter });
        await CreateRuleAsync(admin, "Illinois", new { Priority = 2, State = "IL", AssignToVolunteerIds = second.ToString(), ChapterId = chapter });
        await CreateRuleAsync(admin, "Illinois losses", new { Priority = 3, State = "IL", Reasons = "Stillbirth", AssignToVolunteerIds = first.ToString(), ChapterId = chapter });

        var (_, request) = await ApplyAsync(chapter, "Stillbirth");   // family lives in IL

        Assert.Equal(second, (await RequestAsync(request)).AssignedToId);   // rule 1 doesn't match, rule 2 does and comes before rule 3
    }

    [Fact]
    public async Task ARuleForATeam_SendsEachRequestToTheLeastBusyEligibleMember()
    {
        var chapter = await NewChapterAsync();
        var busy = await AddVolunteerAsync(chapter, "Busy", "Member", activeCases: 3, role: VolunteerRole.Driver);
        var free = await AddVolunteerAsync(chapter, "Free", "Member", activeCases: 0, role: VolunteerRole.Driver);
        var full = await AddVolunteerAsync(chapter, "Full", "Member", activeCases: 99, role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await CreateRuleAsync(admin, "Team rule", new { Reasons = "Infertility", AssignToVolunteerIds = $"{busy},{free},{full}", ChapterId = chapter });

        var (_, first) = await ApplyAsync(chapter, "Infertility");
        Assert.Equal(free, (await RequestAsync(first)).AssignedToId);

        // The team member who took the first one now has a case, but is still the least busy of the eligible members.
        var (_, second) = await ApplyAsync(chapter, "Infertility");
        Assert.Equal(free, (await RequestAsync(second)).AssignedToId);
    }

    [Fact]
    public async Task ARuleWhoseVolunteerIsAtTheLimit_IsSkipped()
    {
        var chapter = await NewChapterAsync();
        var full = await AddVolunteerAsync(chapter, "Very", "Busy", activeCases: 99, role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await CreateRuleAsync(admin, "Everything to the busy one", new { Reasons = "Infertility", AssignToVolunteerIds = full.ToString(), ChapterId = chapter });

        var (_, request) = await ApplyAsync(chapter, "Infertility");

        Assert.Null((await RequestAsync(request)).AssignedToId);   // no other volunteer, so it waits in the queue
    }

    [Fact]
    public async Task ApplyingTheRules_AssignsRequestsAlreadyWaitingInTheQueue()
    {
        var chapter = await NewChapterAsync();
        var (_, waiting) = await ApplyAsync(chapter, "Stillbirth");
        Assert.Null((await RequestAsync(waiting)).AssignedToId);   // no volunteers yet

        var specialist = await AddVolunteerAsync(chapter, "Late", "Rule", role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await CreateRuleAsync(admin, "Late rule", new { Reasons = "Stillbirth", AssignToVolunteerIds = specialist.ToString(), ChapterId = chapter });

        var resp = await admin.PostAsJsonAsync("/api/v1/assignment-rules/apply", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.Equal(specialist, (await RequestAsync(waiting)).AssignedToId);
    }

    [Fact]
    public async Task RuleValidation_AndPermissions()
    {
        var chapter = await NewChapterAsync();
        var volunteer = await AddVolunteerAsync(chapter, "Val", "Idate");
        var admin = await AdminClientAsync();

        var noCondition = await admin.PostAsJsonAsync("/api/v1/assignment-rules", new { Name = "Nothing", AssignToVolunteerIds = volunteer.ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, noCondition.StatusCode);
        var noName = await admin.PostAsJsonAsync("/api/v1/assignment-rules", new { Name = "", State = "IL", AssignToVolunteerIds = volunteer.ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        var badReason = await admin.PostAsJsonAsync("/api/v1/assignment-rules", new { Name = "x", Reasons = "Nonsense", AssignToVolunteerIds = volunteer.ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);
        var noVolunteer = await admin.PostAsJsonAsync("/api/v1/assignment-rules", new { Name = "x", State = "IL", AssignToVolunteerIds = "99999999" });
        Assert.Equal(HttpStatusCode.BadRequest, noVolunteer.StatusCode);

        var staff = await AdminClientAsync("ChapterStaff");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/assignment-rules")).StatusCode);
        var create = await staff.PostAsJsonAsync("/api/v1/assignment-rules", new { Name = "Nope", State = "IL", AssignToVolunteerIds = volunteer.ToString() });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    // ── Case counts ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ActiveCases_FollowAssigningUnassigningAndFulfilling()
    {
        var chapter = await NewChapterAsync();
        var volunteer = await AddVolunteerAsync(chapter, "Count", "Er", role: VolunteerRole.Driver);
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteer });
        Assert.Equal(1, (await VolunteerAsync(volunteer)).ActiveCases);

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/unassign", new { });
        Assert.Equal(0, (await VolunteerAsync(volunteer)).ActiveCases);

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteer });
        Assert.Equal(1, (await VolunteerAsync(volunteer)).ActiveCases);

        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/fulfill", new { Notes = "done" });
        var afterFulfill = await VolunteerAsync(volunteer);
        Assert.Equal(0, afterFulfill.ActiveCases);
        Assert.Equal(1, afterFulfill.TotalCasesFulfilled);

        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/fulfill", new { Notes = "again" });   // pressing it twice must not count twice
        Assert.Equal(1, (await VolunteerAsync(volunteer)).TotalCasesFulfilled);
    }

    [Fact]
    public async Task AssigningACaseThatIsAlreadyFurtherAlong_DoesNotSendItBackToInProgress()
    {
        var chapter = await NewChapterAsync();
        var volunteer = await AddVolunteerAsync(chapter, "Late", "Assign", role: VolunteerRole.Driver);
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var r = await db.Requests.SingleAsync(x => x.Id == request);
            r.Status = CaseStatus.Shipped; r.ProcessStage = ProcessStage.Shipping; r.TrackingNumber = "9400 1111";
            await db.SaveChangesAsync();
        }
        var admin = await AdminClientAsync();

        var resp = await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteer });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var after = await RequestAsync(request);
        Assert.Equal(volunteer, after.AssignedToId);
        Assert.Equal(CaseStatus.Shipped, after.Status);
        Assert.Equal(ProcessStage.Shipping, after.ProcessStage);
    }

    [Fact]
    public async Task ReassigningACase_MovesItFromOneVolunteersCountToTheOthers()
    {
        var chapter = await NewChapterAsync();
        var a = await AddVolunteerAsync(chapter, "Aaa", "One", role: VolunteerRole.Driver);
        var b = await AddVolunteerAsync(chapter, "Bbb", "Two", role: VolunteerRole.Driver);
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = a });
        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = b });

        Assert.Equal(0, (await VolunteerAsync(a)).ActiveCases);
        Assert.Equal(1, (await VolunteerAsync(b)).ActiveCases);
    }

    // ── Accepting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptingAnAssignment_MovesTheCaseToConfirmed()
    {
        var chapter = await NewChapterAsync();
        var volunteer = await AddVolunteerAsync(chapter, "Ac", "Cept");
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        Assert.Equal(volunteer, (await RequestAsync(request)).AssignedToId);
        Assert.Equal(ProcessStage.Assigned, (await RequestAsync(request)).ProcessStage);
        var admin = await AdminClientAsync();

        var accept = await admin.PostAsJsonAsync($"/api/v1/requests/{request}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var after = await RequestAsync(request);
        Assert.Equal(ProcessStage.Confirmed, after.ProcessStage);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        Assert.Contains(await db.RequestActivities.Where(a => a.RequestId == request).ToListAsync(),
            a => a.ActivityType == ActivityType.ProcessStageChanged && a.NewValue == "Confirmed");
    }

    // ── Assigning by hand gives the volunteer something to accept ─────────────

    private async Task<List<RequestAssignment>> AssignmentsAsync(int requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.RequestAssignments.AsNoTracking().Where(a => a.RequestId == requestId).OrderBy(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task AssigningByHand_CreatesAPendingAssignment_SoTheVolunteerCanAccept()
    {
        var chapter = await NewChapterAsync();
        await AddVolunteerAsync(chapter, "Auto", "Picked");
        var chosen = await AddVolunteerAsync(chapter, "Chosen", "ByStaff", role: VolunteerRole.Driver);   // never auto-picked
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = chosen })).StatusCode);

        var current = (await AssignmentsAsync(request)).Single(a => a.Status == AssignmentStatus.Pending);
        Assert.Equal(chosen, current.AssignedToId);
        Assert.True(current.AcceptanceDeadline > DateTime.UtcNow);
        Assert.Equal(ProcessStage.Assigned, (await RequestAsync(request)).ProcessStage);

        // ...so Accept now works for a hand-assigned case and lands it in the Volunteer Accepted stage.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/requests/{request}/accept", new { })).StatusCode);
        Assert.Equal(ProcessStage.Confirmed, (await RequestAsync(request)).ProcessStage);
    }

    [Fact]
    public async Task ChoosingTheSameVolunteerAgain_ChangesNothing_AndKeepsTheirAcceptance()
    {
        var chapter = await NewChapterAsync();
        var volunteer = await AddVolunteerAsync(chapter, "Same", "Person", role: VolunteerRole.Driver);
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();
        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteer });
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/accept", new { });
        var before = (await AssignmentsAsync(request)).Count;

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = volunteer });

        var after = await AssignmentsAsync(request);
        Assert.Equal(before, after.Count);
        Assert.Equal(AssignmentStatus.Accepted, after.Last(a => a.AssignedToId == volunteer).Status);
        Assert.Equal(ProcessStage.Confirmed, (await RequestAsync(request)).ProcessStage);
    }

    [Fact]
    public async Task HandingTheCaseToSomeoneElse_RetiresTheFirstAssignment_AndAsksTheNewVolunteerToAccept()
    {
        var chapter = await NewChapterAsync();
        var first = await AddVolunteerAsync(chapter, "First", "Holder", role: VolunteerRole.Driver);
        var second = await AddVolunteerAsync(chapter, "Second", "Holder", role: VolunteerRole.Driver);
        var (_, request) = await ApplyAsync(chapter, "Infertility");
        var admin = await AdminClientAsync();
        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = first });
        await admin.PostAsJsonAsync($"/api/v1/requests/{request}/accept", new { });
        Assert.Equal(ProcessStage.Confirmed, (await RequestAsync(request)).ProcessStage);

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = second });

        var all = await AssignmentsAsync(request);
        Assert.Equal(AssignmentStatus.Reassigned, all.Last(a => a.AssignedToId == first).Status);
        var pending = Assert.Single(all, a => a.Status == AssignmentStatus.Pending);
        Assert.Equal(second, pending.AssignedToId);
        Assert.Equal(all.Count, pending.AttemptNumber);
        // The new volunteer hasn't accepted, so the case is back at Assigned rather than staying "Volunteer Accepted".
        Assert.Equal(ProcessStage.Assigned, (await RequestAsync(request)).ProcessStage);
    }

    // ── My Work Queue ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MyCases_AreFoundThroughTheVolunteerRecordWithTheSameEmail_NotJustTheName()
    {
        var chapter = await NewChapterAsync();
        var email = $"mine-{Guid.NewGuid():N}@test.com";
        var volunteer = await AddVolunteerAsync(chapter, "Different", "Displayname", email: email, role: VolunteerRole.Driver);
        var (_, mine) = await ApplyAsync(chapter, "Infertility");
        var (_, someoneElses) = await ApplyAsync(chapter, "Infertility");
        var other = await AddVolunteerAsync(chapter, "Some", "Other", role: VolunteerRole.Driver);
        var admin = await AdminClientAsync();
        await admin.PutAsJsonAsync($"/api/v1/requests/{mine}/assign", new { VolunteerId = volunteer });
        await admin.PutAsJsonAsync($"/api/v1/requests/{someoneElses}/assign", new { VolunteerId = other });

        var me = await ClientForAsync(email, "ChapterStaff");
        var resp = await me.GetAsync("/api/v1/requests/mine");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var ids = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).ToList();

        Assert.Equal(new[] { mine }, ids);
    }

    [Fact]
    public async Task MyCases_IsEmpty_WhenThereIsNoVolunteerRecordForTheUser()
    {
        var me = await ClientForAsync($"nobody-{Guid.NewGuid():N}@test.com", "ChapterStaff");
        var body = await me.GetFromJsonAsync<JsonElement>("/api/v1/requests/mine");
        Assert.Equal(0, body.GetArrayLength());
    }

    // ── One family's requests ─────────────────────────────────────────────────

    [Fact]
    public async Task TheRequestsList_CanBeFilteredToOneFamily()
    {
        var chapter = await NewChapterAsync();
        var (familyId, requestId) = await ApplyAsync(chapter, "Infertility");
        await ApplyAsync(chapter, "Infertility");   // a different family
        var admin = await AdminClientAsync();

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/requests?familyId={familyId}");

        Assert.Equal(new[] { requestId }, list.EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).ToArray());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> NewChapterAsync()
    {
        var id = Interlocked.Increment(ref _nextChapterId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Chapters.Add(new Chapter
        {
            Id = id, Name = $"Workflow Chapter {id}", City = "Testville", State = "IL",
            ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> AddVolunteerAsync(int chapterId, string first, string last, int activeCases = 0,
        VolunteerRole role = VolunteerRole.PackageAssembler, string? email = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var v = new Volunteer
        {
            FirstName = first, LastName = last, Email = email ?? $"{first}.{last}.{Guid.NewGuid():N}@test.example.com".ToLowerInvariant(),
            Role = role, Status = VolunteerStatus.Active, ChapterId = chapterId, ActiveCases = activeCases, JoinedDate = DateTime.UtcNow,
        };
        db.Volunteers.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private async Task<(int FamilyId, int RequestId)> ApplyAsync(int chapterId, string reason)
    {
        var tag = new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)('g' + (c <= '9' ? c - '0' : c - 'a' + 10) % 20)).ToArray());
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Flow{tag}", Parent2FirstName = "Ann", Parent2LastName = $"Flow{tag}",
                Email = $"flow-{tag}@test.example.com", Phone = "", StreetAddress = $"{Random.Shared.Next(100, 999)} Test St",
                City = "Testville", State = "IL", Zip = $"6{Random.Shared.Next(1000, 9999)}", Reason = reason, ChapterId = chapterId,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("familyId").GetInt32(), body.GetProperty("requestId").GetInt32());
    }

    private async Task<PackageRequest> RequestAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.Requests.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<RequestAssignment> LatestAssignmentAsync(int requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.RequestAssignments.AsNoTracking().Where(a => a.RequestId == requestId).OrderByDescending(a => a.Id).FirstAsync();
    }

    private async Task<Volunteer> VolunteerAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.Volunteers.AsNoTracking().SingleAsync(v => v.Id == id);
    }

    private static async Task CreateRuleAsync(HttpClient admin, string name, object fields)
    {
        var json = JsonSerializer.SerializeToElement(fields);
        var body = new Dictionary<string, object?> { ["Name"] = name };
        foreach (var p in json.EnumerateObject()) body[p.Name] = p.Value.Clone();
        var resp = await admin.PostAsJsonAsync("/api/v1/assignment-rules", body);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    private Task<HttpClient> AdminClientAsync(string role = "HQAdmin") =>
        ClientForAsync($"wf-{Guid.NewGuid():N}@test.com", role);

    private async Task<HttpClient> ClientForAsync(string email, string role)
    {
        var client = _factory.CreateClient();
        const string password = "TestPass1Workflow!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Work", LastName = "Flow", Role = role, ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
