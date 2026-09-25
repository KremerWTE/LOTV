using Lotv.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// A parish must belong to a diocese. This links any existing parish whose diocese id doesn't point at a real diocese, by the
/// diocese name it carries (or its city and state) when that is certain, and reports the ones that still have none. Safe to run
/// on every startup; it never guesses.
/// </summary>
public static class ParishDioceseRepair
{
    public static async Task<(int Linked, int StillWithoutDiocese)> RunAsync(LotvDbContext db)
    {
        var dioceses = await db.Dioceses.AsNoTracking().ToListAsync();
        var known = dioceses.Select(d => d.Id).ToList();
        var orphans = await db.Parishes.Where(p => !known.Contains(p.DioceseId)).ToListAsync();
        if (orphans.Count == 0) return (0, 0);

        int linked = 0, still = 0;
        var touched = new HashSet<int>();
        foreach (var p in orphans)
        {
            var match = DioceseMatcher.Find(dioceses, p.DioceseName, p.City, p.State, null, Lotv.Api.Services.DioceseGeography.Instance);
            if (match.Diocese is null) { still++; continue; }
            p.DioceseId = match.Diocese.Id;
            p.DioceseName = match.Diocese.Name;
            p.ChapterId = match.Diocese.ChapterId;
            touched.Add(match.Diocese.Id);
            linked++;
        }
        if (linked > 0)
        {
            await db.SaveChangesAsync();
            await Lotv.Api.Services.ParishEndpoints.RecomputeDioceseCountsAsync(db, touched);
        }
        return (linked, still);
    }
}
