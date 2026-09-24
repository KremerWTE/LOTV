using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Bereavement follow-up keeps one tracker per family. Earlier intake created a tracker for every
/// submission, so a family that resubmitted (or was merged during duplicate review) could be on the
/// tracker twice. Runs at startup, is idempotent, and never removes a tracker that has a book marked sent
/// unless another tracker for the same family already carries the record.
/// </summary>
public static class FollowUpTrackerDedupe
{
    public static async Task<int> RunAsync(LotvDbContext db)
    {
        var trackers = await db.FollowUpTrackers.Include(t => t.Milestones).Where(t => t.FamilyId != null).ToListAsync();
        var mergedFamilyIds = (await db.Families
                .Where(f => f.Status == FamilyStatus.Closed && f.ContactNotes != null && f.ContactNotes.StartsWith("Merged into Family #"))
                .Select(f => f.Id).ToListAsync()).ToHashSet();

        var remove = new List<FollowUpTracker>();

        foreach (var group in trackers.GroupBy(t => t.FamilyId))
        {
            if (group.Count() < 2) continue;
            // Keep the tracker with the most progress, then the oldest.
            var keep = group.OrderByDescending(t => t.Milestones.Count(m => m.BookSent)).ThenBy(t => t.Id).First();
            remove.AddRange(group.Where(t => t.Id != keep.Id));
        }

        // A merged-away family record shouldn't have its own tracker when nothing was sent from it.
        remove.AddRange(trackers.Where(t => t.FamilyId is int id && mergedFamilyIds.Contains(id)
                                            && !t.Milestones.Any(m => m.BookSent)
                                            && !remove.Contains(t)));

        if (remove.Count == 0) return 0;
        db.FollowUpMilestones.RemoveRange(remove.SelectMany(t => t.Milestones));
        db.FollowUpTrackers.RemoveRange(remove);
        await db.SaveChangesAsync();
        return remove.Count;
    }
}
