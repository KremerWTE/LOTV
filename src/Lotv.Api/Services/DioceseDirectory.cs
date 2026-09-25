using System.Reflection;
using Lotv.Api.Data;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public record DioceseDirectoryRequest(int? ChapterId, bool DryRun = true);
public record DioceseDirectoryResult(bool DryRun, int InList, int Added, int AlreadyThere, int ChapterId);

/// <summary>
/// The list of US dioceses (Name, City, State), so every parish can belong to one. It ships with the app as
/// Data/Reference/us-dioceses.csv. Source: the current territorial dioceses on Wikipedia's "List of Catholic dioceses in the
/// United States" (CC BY-SA 4.0), each with its seat city and state looked up from Wikidata (CC0) and checked by hand
/// (cathedral city where the chancery is elsewhere, territories). Eastern-rite eparchies and the military archdiocese are not in it.
/// Loaded dioceses are marked directory-only, so they are never counted in "dioceses reached".
/// </summary>
public static class DioceseDirectory
{
    public static List<(string Name, string City, string State)> Reference()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames().Single(n => n.EndsWith("us-dioceses.csv", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(asm.GetManifestResourceStream(resource)!);
        return CsvReader.Parse(reader.ReadToEnd())
            .Where(r => r.Count >= 3 && !string.IsNullOrWhiteSpace(r[0]))
            .Skip(1)   // header
            .Select(r => (r[0].Trim(), r[1].Trim(), r[2].Trim()))
            .ToList();
    }

    public static void MapDioceseDirectoryEndpoints(this WebApplication app)
    {
        // Adds every US diocese that isn't already there (matched by name and state, or by seat city and state).
        app.MapPost("/api/v1/dioceses/load-us-directory", async (DioceseDirectoryRequest body, LotvDbContext db) =>
        {
            var chapterId = body.ChapterId ?? await db.Chapters.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (chapterId is null) return Results.BadRequest(new { error = "There is no chapter to attach the dioceses to. Create a chapter first." });
            if (!await db.Chapters.AnyAsync(c => c.Id == chapterId)) return Results.BadRequest(new { error = "That chapter doesn't exist." });

            var existing = await db.Dioceses.AsNoTracking().ToListAsync();
            bool Has((string Name, string City, string State) r) => existing.Any(d =>
                UsStates.ToCode(d.State) == r.State &&
                (DioceseMatcher.Normalize(d.Name) == DioceseMatcher.Normalize(r.Name) ||
                 DioceseMatcher.NormalizePlace(d.City) == DioceseMatcher.NormalizePlace(r.City)));

            var reference = Reference();
            var toAdd = reference.Where(r => !Has(r)).ToList();
            if (!body.DryRun && toAdd.Count > 0)
            {
                db.Dioceses.AddRange(toAdd.Select(r => new Diocese { Name = r.Name, City = r.City, State = r.State, ChapterId = chapterId.Value, IsDirectoryOnly = true }));
                await db.SaveChangesAsync();
            }
            return Results.Ok(new DioceseDirectoryResult(body.DryRun, reference.Count, toAdd.Count, reference.Count - toAdd.Count, chapterId.Value));
        }).WithTags("Dioceses").RequireAuthorization("HQAdmin");
    }
}
