using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Gives an assignment made by staff the same accept / decline record an automatic one gets. Without it the volunteer's
/// Accept has nothing to act on (it looks for a pending assignment), so a hand-assigned case could never reach the
/// "Volunteer Accepted" stage except by dragging its card.
/// </summary>
public static class ManualAssignment
{
    /// <summary>
    /// Call before the request's AssignedToId is changed. Retires any open assignment to someone else, adds a pending one
    /// for <paramref name="volunteer"/> (unless they already hold this case), and returns the time they have to accept.
    /// </summary>
    public static async Task<DateTime> RecordAsync(LotvDbContext db, PackageRequest request, Volunteer volunteer, string? byId, string? byName)
    {
        var open = await db.RequestAssignments
            .Where(a => a.RequestId == request.Id && (a.Status == AssignmentStatus.Pending || a.Status == AssignmentStatus.Accepted))
            .ToListAsync();

        // Choosing the person who already has it changes nothing (and must not undo an acceptance).
        var already = open.FirstOrDefault(a => a.AssignedToId == volunteer.Id);
        if (already is not null && request.AssignedToId == volunteer.Id) return already.AcceptanceDeadline;

        foreach (var a in open) a.Status = AssignmentStatus.Reassigned;

        var chapter = await db.Chapters.FindAsync(request.ChapterId);
        var windowHours = request.Priority == RequestPriority.Urgent
            ? chapter?.UrgentAcceptanceWindowHours ?? 4
            : chapter?.AcceptanceWindowHours ?? 24;
        var attempt = await db.RequestAssignments.CountAsync(a => a.RequestId == request.Id) + 1;
        var deadline = DateTime.UtcNow.AddHours(windowHours);

        db.RequestAssignments.Add(new RequestAssignment
        {
            RequestId = request.Id,
            AssignedToId = volunteer.Id,
            AssignedToName = volunteer.FullName,
            AssignedById = byId ?? "",
            AssignedByName = byName ?? "",
            Status = AssignmentStatus.Pending,
            AssignedAt = DateTime.UtcNow,
            AcceptanceDeadline = deadline,
            AttemptNumber = attempt
        });
        return deadline;
    }
}
