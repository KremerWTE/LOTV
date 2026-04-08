using Lotv.Api.Data;
using Lotv.Api.Hubs;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Reporting;
using Lotv.Core.Services.Interfaces;
using Lotv.Core.ValueObjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public class AutoAssignmentService : IAutoAssignmentService
{
    private readonly LotvDbContext _db;
    private readonly IHubContext<RequestsHub> _hub;
    private readonly ILogger<AutoAssignmentService> _logger;

    // Scoring weights (per ADR and auto-assignment-algorithm.md)
    private const double ProximityWeight = 0.45;
    private const double WorkloadWeight  = 0.35;
    private const double LoyaltyWeight   = 0.20;
    private const double CompositeThreshold = 30.0;  // minimum score to auto-assign

    public AutoAssignmentService(LotvDbContext db, IHubContext<RequestsHub> hub, ILogger<AutoAssignmentService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task<List<VolunteerScoreResult>> GetScoresAsync(int requestId)
    {
        var request = await _db.Requests.FindAsync(requestId);
        if (request is null) return [];

        var candidates = await GetCandidatesAsync(request);
        return candidates.Select(v => ScoreVolunteer(v, request)).OrderByDescending(s => s.CompositeScore).ToList();
    }

    public async Task<Result> TryAutoAssignAsync(int requestId)
    {
        var request = await _db.Requests.FindAsync(requestId);
        if (request is null) return Result.Fail("Request not found");
        if (request.AssignedToId.HasValue) return Result.Fail("Request is already assigned");

        var scores = (await GetScoresAsync(requestId)).OrderByDescending(s => s.CompositeScore).ToList();

        if (!scores.Any() || scores[0].CompositeScore < CompositeThreshold)
        {
            _logger.LogInformation("No qualifying volunteer for request {Id} — routing to unassigned queue", requestId);
            return Result.Ok(); // stays in queue
        }

        var top = scores[0];
        var volunteer = await _db.Volunteers.FindAsync(top.VolunteerId);
        if (volunteer is null) return Result.Fail("Volunteer not found");

        return await AssignAsync(request, volunteer, "system", attempt: 1);
    }

    public async Task<Result> HandleDeclineAsync(int requestId, int volunteerId, string reason)
    {
        var request = await _db.Requests.FindAsync(requestId);
        if (request is null) return Result.Fail("Request not found");

        // Count prior attempts
        var priorAttempts = await _db.RequestAssignments
            .Where(a => a.RequestId == requestId)
            .CountAsync();

        var chapter = await _db.Chapters.FindAsync(request.ChapterId);
        int maxAttempts = chapter?.MaxReassignmentAttempts ?? 3;

        if (priorAttempts >= maxAttempts)
        {
            // Escalate to staff
            request.Status = CaseStatus.OnHold;
            request.AssignedToId = null;
            request.AssignedTo = null;
            await _db.SaveChangesAsync();
            await _hub.Clients.Group($"chapter-{request.ChapterId}")
                .SendAsync("CaseEscalated", request.Id, "No volunteer accepted after maximum reassignment attempts");
            return Result.Ok();
        }

        // Find next best candidate excluding previously declined volunteers
        var declined = await _db.RequestAssignments
            .Where(a => a.RequestId == requestId && a.Status == AssignmentStatus.Declined)
            .Select(a => a.AssignedToId)
            .ToListAsync();

        var scores = (await GetScoresAsync(requestId))
            .Where(s => !declined.Contains(s.VolunteerId))
            .OrderByDescending(s => s.CompositeScore)
            .ToList();

        if (!scores.Any() || scores[0].CompositeScore < CompositeThreshold)
        {
            request.Status = CaseStatus.OnHold;
            request.AssignedToId = null;
            request.AssignedTo = null;
            await _db.SaveChangesAsync();
            return Result.Ok();
        }

        var nextVolunteer = await _db.Volunteers.FindAsync(scores[0].VolunteerId);
        if (nextVolunteer is null) return Result.Fail("Next volunteer not found");

        return await AssignAsync(request, nextVolunteer, "system", attempt: priorAttempts + 1);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<List<Volunteer>> GetCandidatesAsync(PackageRequest request)
    {
        var chapter = await _db.Chapters.FindAsync(request.ChapterId);
        int maxCases = chapter?.MaxActiveCasesPerVolunteer ?? 6;

        return await _db.Volunteers
            .Where(v => v.ChapterId == request.ChapterId
                     && v.Status == VolunteerStatus.Active
                     && (v.Role == VolunteerRole.PackageAssembler || v.Role == VolunteerRole.Admin)
                     && v.ActiveCases < maxCases)
            .ToListAsync();
    }

    private static VolunteerScoreResult ScoreVolunteer(Volunteer v, PackageRequest request)
    {
        double proximityScore = 100.0; // default when no geo data
        double distanceMiles = 0;

        if (v.Latitude.HasValue && v.Longitude.HasValue
            && request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var volunteerLoc = new GeoLocation(v.Latitude.Value, v.Longitude.Value);
            var requestLoc   = new GeoLocation(request.Latitude.Value, request.Longitude.Value);
            distanceMiles    = volunteerLoc.DistanceMilesTo(requestLoc);
            // Score: 100 at 0 miles, 0 at 50+ miles (linear decay)
            proximityScore   = Math.Max(0, 100.0 - (distanceMiles / 50.0) * 100.0);
        }

        // WorkloadScore: lower workload = higher score
        double workloadScore = Math.Max(0, 100.0 - (v.ActiveCases / (double)6) * 100.0);

        // LoyaltyScore: capped at 100
        double loyaltyScore = Math.Min(100.0, v.TotalCasesFulfilled * 4.0);

        double composite = (proximityScore * ProximityWeight)
                         + (workloadScore  * WorkloadWeight)
                         + (loyaltyScore   * LoyaltyWeight);

        return new VolunteerScoreResult(
            VolunteerId: v.Id,
            Name: v.FullName,
            ProximityScore: Math.Round(proximityScore, 1),
            WorkloadScore: Math.Round(workloadScore, 1),
            LoyaltyScore: Math.Round(loyaltyScore, 1),
            CompositeScore: Math.Round(composite, 1),
            Recommended: composite >= CompositeThreshold,
            DistanceMiles: Math.Round(distanceMiles, 1)
        );
    }

    private async Task<Result> AssignAsync(PackageRequest request, Volunteer volunteer, string assignedById, int attempt)
    {
        var chapter = await _db.Chapters.FindAsync(request.ChapterId);
        int windowHours = request.Priority == RequestPriority.Urgent
            ? (chapter?.UrgentAcceptanceWindowHours ?? 4)
            : (chapter?.AcceptanceWindowHours ?? 24);

        request.AssignedToId = volunteer.Id;
        request.AssignedTo = volunteer.FullName;
        request.Status = CaseStatus.InProgress;
        request.UpdatedAt = DateTime.UtcNow;

        _db.RequestAssignments.Add(new RequestAssignment
        {
            RequestId = request.Id,
            AssignedToId = volunteer.Id,
            AssignedToName = volunteer.FullName,
            AssignedById = assignedById,
            AssignedByName = assignedById == "system" ? "Auto-Assignment" : assignedById,
            Status = AssignmentStatus.Pending,
            AssignedAt = DateTime.UtcNow,
            AcceptanceDeadline = DateTime.UtcNow.AddHours(windowHours),
            AttemptNumber = attempt
        });

        _db.RequestActivities.Add(new RequestActivity
        {
            RequestId = request.Id,
            ActorId = assignedById,
            ActorName = "Auto-Assignment",
            ActivityType = ActivityType.Assigned,
            NewValue = volunteer.FullName,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"chapter-{request.ChapterId}")
            .SendAsync("CaseAssigned", request.Id, volunteer.Id, volunteer.FullName);

        return Result.Ok();
    }
}
