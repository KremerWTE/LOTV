using Lotv.Api.Data;
using Lotv.Core.Models;

namespace Lotv.Api.Services;

/// <summary>
/// A volunteer's answer to an assignment, shared by the volunteer's own page and the staff endpoints that answer for them.
/// Accepting moves the case from Assigned to Confirmed ("Volunteer Accepted"). Declining sends it back to the unassigned
/// queue for staff to reassign — it is deliberately not handed to the next volunteer automatically.
/// </summary>
public static class AssignmentResponses
{
    public static async Task AcceptAsync(LotvDbContext db, PackageRequest request, RequestAssignment assignment, string? byId, string? byName)
    {
        assignment.Status = AssignmentStatus.Accepted;
        assignment.AcceptedAt = DateTime.UtcNow;

        if (request.ProcessStage is ProcessStage.Assigned or ProcessStage.Unassigned)
        {
            var from = request.ProcessStage;
            request.ProcessStage = ProcessStage.Confirmed;
            request.UpdatedAt = DateTime.UtcNow;
            db.RequestActivities.Add(new RequestActivity
            {
                RequestId = request.Id, ActorId = byId ?? "", ActorName = byName ?? "",
                ActivityType = ActivityType.ProcessStageChanged, OldValue = from.ToString(), NewValue = ProcessStage.Confirmed.ToString(),
                Details = "Volunteer accepted the assignment.", Timestamp = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public static async Task DeclineAsync(LotvDbContext db, PackageRequest request, RequestAssignment assignment, string? reason, string? byId, string? byName)
    {
        assignment.Status = AssignmentStatus.Declined;
        assignment.DeclinedAt = DateTime.UtcNow;
        assignment.DeclineReason = reason;

        var previousName = request.AssignedTo;
        var previousId = request.AssignedToId;
        request.AssignedToId = null;
        request.AssignedTo = null;
        request.Status = CaseStatus.New;
        request.ProcessStage = ProcessStage.Unassigned;
        request.UpdatedAt = DateTime.UtcNow;
        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = request.Id, ActorId = byId ?? "", ActorName = byName ?? "",
            ActivityType = ActivityType.Unassigned, OldValue = previousName,
            Details = string.IsNullOrWhiteSpace(reason) ? "Volunteer declined the assignment." : $"Volunteer declined the assignment: {reason.Trim()}",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await VolunteerWorkload.RecomputeAsync(db, previousId);
    }
}
