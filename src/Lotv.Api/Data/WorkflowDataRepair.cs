using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Startup repair: automatic assignment used to leave the process stage at Unassigned, so an assigned,
/// in-progress request could still sit in the "Unassigned" stage. Moves those to Assigned. Idempotent.
/// </summary>
public static class WorkflowDataRepair
{
    public static async Task<int> RunAsync(LotvDbContext db)
    {
        var stuck = await db.Requests
            .Where(r => r.AssignedToId != null && r.ProcessStage == ProcessStage.Unassigned && r.Status == CaseStatus.InProgress)
            .ToListAsync();
        foreach (var r in stuck) r.ProcessStage = ProcessStage.Assigned;
        if (stuck.Count > 0) await db.SaveChangesAsync();
        return stuck.Count;
    }
}
