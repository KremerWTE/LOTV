using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Keeps Volunteer.ActiveCases equal to the number of open requests actually assigned to them, so the
/// assignment limit and the auto-assignment scores use real numbers. Call after any change to who a request
/// is assigned to or whether it is still open.
/// </summary>
public static class VolunteerWorkload
{
    public static Task RecomputeAsync(LotvDbContext db, params int?[] volunteerIds) =>
        RecomputeAsync(db, volunteerIds.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList());

    public static async Task RecomputeAsync(LotvDbContext db, List<int> ids)
    {
        if (ids.Count == 0) return;
        var counts = await db.Requests
            .Where(r => r.AssignedToId != null && ids.Contains(r.AssignedToId.Value)
                        && r.Status != CaseStatus.Fulfilled && r.Status != CaseStatus.Cancelled)
            .GroupBy(r => r.AssignedToId!.Value)
            .Select(g => new { Id = g.Key, N = g.Count() })
            .ToListAsync();
        var volunteers = await db.Volunteers.Where(v => ids.Contains(v.Id)).ToListAsync();
        var changed = false;
        foreach (var v in volunteers)
        {
            var n = counts.FirstOrDefault(c => c.Id == v.Id)?.N ?? 0;
            if (v.ActiveCases != n) { v.ActiveCases = n; changed = true; }
        }
        if (changed) await db.SaveChangesAsync();
    }

    public static async Task RecomputeAllAsync(LotvDbContext db) =>
        await RecomputeAsync(db, await db.Volunteers.Select(v => v.Id).ToListAsync());
}
