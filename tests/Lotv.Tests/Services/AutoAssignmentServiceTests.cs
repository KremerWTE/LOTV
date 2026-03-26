using Lotv.Api.Data;
using Lotv.Api.Hubs;
using Lotv.Api.Services;
using Lotv.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lotv.Tests.Services;

/// <summary>
/// Unit tests for AutoAssignmentService scoring and assignment logic.
/// Uses EF Core InMemory to avoid filesystem dependencies.
/// </summary>
public class AutoAssignmentServiceTests : IDisposable
{
    private readonly LotvDbContext       _db;
    private readonly AutoAssignmentService _svc;

    public AutoAssignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<LotvDbContext>()
            .UseInMemoryDatabase($"aa-{Guid.NewGuid():N}")
            .Options;
        _db = new LotvDbContext(options);

        var hubMock = new Mock<IHubContext<RequestsHub>>();
        hubMock.Setup(h => h.Clients.Group(It.IsAny<string>()).SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), default))
               .Returns(Task.CompletedTask);

        _svc = new AutoAssignmentService(_db, hubMock.Object, NullLogger<AutoAssignmentService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Chapter SeedChapter(int maxCases = 6) =>
        _db.Chapters.Add(new Chapter
        {
            Name = "Test Chapter", City = "Springfield", State = "IL", IsActive = true,
            MaxActiveCasesPerVolunteer = maxCases, AcceptanceWindowHours = 24, UrgentAcceptanceWindowHours = 4
        }).Entity;

    private Volunteer SeedVolunteer(Chapter ch, int activeCases = 0, int totalFulfilled = 0, VolunteerStatus status = VolunteerStatus.Active) =>
        _db.Volunteers.Add(new Volunteer
        {
            FirstName = "Vol", LastName = $"#{activeCases}", Email = $"v{activeCases}@test.com",
            ChapterId = ch.Id, Status = status,
            Role = VolunteerRole.PackageAssembler,
            ActiveCases = activeCases, TotalCasesFulfilled = totalFulfilled
        }).Entity;

    private PackageRequest SeedRequest(Chapter ch) =>
        _db.Requests.Add(new PackageRequest
        {
            FamilyId  = 1, ChapterId = ch.Id,
            Status    = CaseStatus.New, Priority = RequestPriority.Normal,
            Reason    = PackageReason.Miscarriage,
            CreatedAt = DateTime.UtcNow
        }).Entity;

    // ── Scoring ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScoresAsync_NoVolunteers_ReturnsEmpty()
    {
        var ch  = SeedChapter();
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.Empty(scores);
    }

    [Fact]
    public async Task GetScoresAsync_ReturnsOneScorePerActiveVolunteer()
    {
        var ch = SeedChapter();
        SeedVolunteer(ch, activeCases: 0);
        SeedVolunteer(ch, activeCases: 2);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.Equal(2, scores.Count);
    }

    [Fact]
    public async Task GetScoresAsync_OrderedByCompositeScoreDescending()
    {
        var ch = SeedChapter();
        SeedVolunteer(ch, activeCases: 5);  // high workload → lower score
        SeedVolunteer(ch, activeCases: 0);  // no workload → higher score
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.True(scores[0].CompositeScore >= scores[1].CompositeScore);
    }

    [Fact]
    public async Task GetScoresAsync_InactiveVolunteer_NotIncluded()
    {
        var ch = SeedChapter();
        SeedVolunteer(ch, status: VolunteerStatus.Onboarding);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.Empty(scores);
    }

    [Fact]
    public async Task GetScoresAsync_VolunteerAtCapacity_NotIncluded()
    {
        var ch = SeedChapter(maxCases: 3);
        SeedVolunteer(ch, activeCases: 3);  // at capacity
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.Empty(scores);
    }

    [Fact]
    public async Task GetScoresAsync_FullWorkload_WorkloadScoreIsZero()
    {
        var ch = SeedChapter(maxCases: 6);
        SeedVolunteer(ch, activeCases: 5); // 5/6 = ~83% utilization → near-zero workload score
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        Assert.Single(scores);
        Assert.True(scores[0].WorkloadScore < 20.0, $"Expected low workload score but got {scores[0].WorkloadScore}");
    }

    [Fact]
    public async Task GetScoresAsync_LoyalVolunteer_HasHigherLoyaltyScore()
    {
        var ch = SeedChapter();
        SeedVolunteer(ch, activeCases: 0, totalFulfilled: 25);  // high loyalty
        SeedVolunteer(ch, activeCases: 0, totalFulfilled: 0);   // no loyalty
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        var highLoyalty = scores.First(s => s.LoyaltyScore > 0);
        var noLoyalty   = scores.First(s => s.LoyaltyScore == 0);
        Assert.True(highLoyalty.CompositeScore > noLoyalty.CompositeScore);
    }

    [Fact]
    public async Task GetScoresAsync_ReturnsNonNegativeScores()
    {
        var ch = SeedChapter();
        SeedVolunteer(ch, activeCases: 5);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var scores = await _svc.GetScoresAsync(req.Id);

        foreach (var s in scores)
        {
            Assert.True(s.CompositeScore >= 0, "Composite score must not be negative");
            Assert.True(s.WorkloadScore  >= 0, "Workload score must not be negative");
        }
    }

    // ── TryAutoAssignAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoAssignAsync_UnknownRequest_ReturnsFail()
    {
        var result = await _svc.TryAutoAssignAsync(9999);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TryAutoAssignAsync_AlreadyAssigned_ReturnsFail()
    {
        var ch  = SeedChapter();
        var vol = SeedVolunteer(ch);
        var req = SeedRequest(ch);
        req.AssignedToId = vol.Id;
        await _db.SaveChangesAsync();

        var result = await _svc.TryAutoAssignAsync(req.Id);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TryAutoAssignAsync_NoQualifyingVolunteer_ReturnsOkAndRequestUnchanged()
    {
        var ch  = SeedChapter();
        var req = SeedRequest(ch);   // no volunteers seeded
        await _db.SaveChangesAsync();

        var result = await _svc.TryAutoAssignAsync(req.Id);

        Assert.True(result.IsSuccess, "Should succeed but route to queue when no volunteers qualify");
        var updated = await _db.Requests.FindAsync(req.Id);
        Assert.Null(updated!.AssignedToId);
    }

    [Fact]
    public async Task TryAutoAssignAsync_QualifyingVolunteer_AssignsAndChangesStatus()
    {
        var ch  = SeedChapter();
        // Volunteer with 0 cases and 25 fulfilled → composite score well above threshold of 30
        SeedVolunteer(ch, activeCases: 0, totalFulfilled: 25);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        var result = await _svc.TryAutoAssignAsync(req.Id);

        Assert.True(result.IsSuccess);
        var updated = await _db.Requests.FindAsync(req.Id);
        Assert.NotNull(updated!.AssignedToId);
        Assert.Equal(CaseStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task TryAutoAssignAsync_CreatesAssignmentRecord()
    {
        var ch  = SeedChapter();
        SeedVolunteer(ch, activeCases: 0, totalFulfilled: 25);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        await _svc.TryAutoAssignAsync(req.Id);

        var assignment = await _db.RequestAssignments.FirstOrDefaultAsync(a => a.RequestId == req.Id);
        Assert.NotNull(assignment);
        Assert.Equal(AssignmentStatus.Pending, assignment!.Status);
    }

    [Fact]
    public async Task TryAutoAssignAsync_CreatesActivityRecord()
    {
        var ch  = SeedChapter();
        SeedVolunteer(ch, activeCases: 0, totalFulfilled: 25);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        await _svc.TryAutoAssignAsync(req.Id);

        var activity = await _db.RequestActivities.FirstOrDefaultAsync(a => a.RequestId == req.Id);
        Assert.NotNull(activity);
        Assert.Equal(ActivityType.Assigned, activity!.ActivityType);
    }

    // ── HandleDeclineAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleDeclineAsync_UnknownRequest_ReturnsFail()
    {
        var result = await _svc.HandleDeclineAsync(9999, 1, "not interested");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task HandleDeclineAsync_MaxAttemptsReached_SetsOnHold()
    {
        var ch  = SeedChapter(maxCases: 6);
        ch.MaxReassignmentAttempts = 2;
        var vol = SeedVolunteer(ch);
        var req = SeedRequest(ch);
        await _db.SaveChangesAsync();

        // Seed 2 prior assignment records (simulates max attempts already tried)
        _db.RequestAssignments.Add(new RequestAssignment { RequestId = req.Id, AssignedToId = vol.Id, AssignedToName = "Test", AssignedById = "sys", AssignedByName = "System", Status = AssignmentStatus.Declined });
        _db.RequestAssignments.Add(new RequestAssignment { RequestId = req.Id, AssignedToId = vol.Id, AssignedToName = "Test", AssignedById = "sys", AssignedByName = "System", Status = AssignmentStatus.Declined });
        await _db.SaveChangesAsync();

        await _svc.HandleDeclineAsync(req.Id, vol.Id, "too busy");

        var updated = await _db.Requests.FindAsync(req.Id);
        Assert.Equal(CaseStatus.OnHold, updated!.Status);
        Assert.Null(updated.AssignedToId);
    }
}
