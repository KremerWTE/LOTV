using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public static class AssignmentRuleMatcher
{
    /// <summary>
    /// The first active rule (lowest Priority first) that matches the request and whose volunteer can take it
    /// (active, and under the chapter's case limit); null when none applies.
    /// </summary>
    public static async Task<(AssignmentRule Rule, Volunteer Volunteer)?> FindAsync(LotvDbContext db, PackageRequest request)
    {
        var family = request.Family ?? await db.Families.AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FamilyId);
        var rules = await db.AssignmentRules.AsNoTracking().Where(r => r.IsActive)
            .OrderBy(r => r.Priority).ThenBy(r => r.Id).ToListAsync();
        if (rules.Count == 0) return null;

        var chapter = await db.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ChapterId);
        var maxCases = chapter?.MaxActiveCasesPerVolunteer ?? 6;

        foreach (var rule in rules)
        {
            if (!rule.Matches(request, family)) continue;
            var volunteer = await db.Volunteers.FirstOrDefaultAsync(v => v.Id == rule.AssignToVolunteerId);
            if (volunteer is null || volunteer.Status != VolunteerStatus.Active || volunteer.ActiveCases >= maxCases) continue;
            return (rule, volunteer);
        }
        return null;
    }
}
