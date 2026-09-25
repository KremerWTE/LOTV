using Lotv.Api.Data;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public record ParishInput(string? Name, int? DioceseId, string? DioceseName, string? City, string? State,
    string? LiaisonName, string? LiaisonEmail, ParishStatus? Status, CertificationLevel? CertificationLevel);

public record ParishImportRequest(string? Csv, bool DryRun = true);

/// <summary>One row of an import that was not added, and why.</summary>
public record ParishImportRow(int Row, string Name, string? City, string? State, string? Diocese, string Outcome, string Reason, List<string>? Candidates);

public record ParishImportResult(bool DryRun, int Rows, int Created, int Duplicates, int NeedsReview, int Rejected,
    List<ParishImportRow> Problems, bool ProblemsTruncated);

/// <summary>
/// The parish directory. Every parish belongs to a diocese: a create or edit without one is refused, and an import only adds
/// a parish when its diocese is named in the list or can be worked out with certainty from its city and state (see
/// <see cref="DioceseMatcher"/>). Parishes that can't be placed are reported for a person to decide, never guessed.
/// </summary>
public static class ParishEndpoints
{
    private const int MaxProblemRowsReturned = 2000;
    private const int MaxImportRows = 50_000;

    public static void MapParishEndpoints(this WebApplication app)
    {
        var parishes = app.MapGroup("/api/v1/parishes").WithTags("Parishes").RequireAuthorization("Staff");

        parishes.MapGet("/", async (LotvDbContext db, HttpContext http, int? dioceseId, string? state, string? q, ParishStatus? status, bool? withActiveCases, int? skip, int? take) =>
        {
            var query = db.Parishes.AsNoTracking().AsQueryable();
            if (dioceseId is int d) query = query.Where(p => p.DioceseId == d);
            if (status is ParishStatus s) query = query.Where(p => p.Status == s);
            if (withActiveCases == true) query = query.Where(p => p.ActiveCases > 0);
            if (UsStates.ToCode(state) is { } code) query = query.Where(p => p.State == code);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => p.Name.Contains(term) || (p.City != null && p.City.Contains(term)) || p.DioceseName.Contains(term));
            }
            http.Response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();
            query = query.OrderBy(p => p.DioceseName).ThenBy(p => p.Name);
            if (skip is > 0) query = query.Skip(skip.Value);
            if (take is > 0) query = query.Take(Math.Min(take.Value, 5000));
            return Results.Ok(await query.ToListAsync());
        });

        parishes.MapGet("/{id:int}", async (int id, LotvDbContext db) =>
            await db.Parishes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(p) : Results.NotFound());

        parishes.MapPost("/", async (ParishInput body, LotvDbContext db) =>
        {
            var (diocese, error) = await ResolveDioceseAsync(db, body);
            if (diocese is null) return error!;
            var parish = new Parish { EnrolledDate = DateTime.UtcNow };
            if (Apply(parish, body, diocese) is { } bad) return bad;
            if (await IsDuplicateAsync(db, parish, null)) return Results.Conflict(new { error = $"\"{parish.Name}\" is already in {diocese.Name}." });
            db.Parishes.Add(parish);
            await db.SaveChangesAsync();
            await RecomputeDioceseCountsAsync(db, [diocese.Id]);
            return Results.Created($"/api/v1/parishes/{parish.Id}", parish);
        }).RequireAuthorization("ChapterAdmin");

        parishes.MapPut("/{id:int}", async (int id, ParishInput body, LotvDbContext db) =>
        {
            var parish = await db.Parishes.FirstOrDefaultAsync(p => p.Id == id);
            if (parish is null) return Results.NotFound();
            var previousDiocese = parish.DioceseId;
            // An edit that doesn't mention a diocese keeps the one it has, so a parish can never be left without one.
            var input = body with { DioceseId = body.DioceseId ?? (string.IsNullOrWhiteSpace(body.DioceseName) ? previousDiocese : null) };
            var (diocese, error) = await ResolveDioceseAsync(db, input);
            if (diocese is null) return error!;
            if (Apply(parish, input, diocese) is { } bad) return bad;
            if (await IsDuplicateAsync(db, parish, parish.Id)) return Results.Conflict(new { error = $"\"{parish.Name}\" is already in {diocese.Name}." });
            await db.SaveChangesAsync();
            await RecomputeDioceseCountsAsync(db, [previousDiocese, diocese.Id]);
            return Results.Ok(parish);
        }).RequireAuthorization("ChapterAdmin");

        parishes.MapPost("/import", async (ParishImportRequest body, LotvDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(body.Csv)) return Results.BadRequest(new { error = "There is no file content to import." });
            // Blank lines are skipped (the reader keeps them as rows with one empty cell).
            var rows = CsvReader.Parse(body.Csv).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
            if (rows.Count < 2) return Results.BadRequest(new { error = "The file needs a header row and at least one parish." });
            if (rows.Count - 1 > MaxImportRows) return Results.BadRequest(new { error = $"Too many rows at once (limit {MaxImportRows}). Split the file." });
            var columns = ReadHeader(rows[0]);
            if (columns.Name < 0) return Results.BadRequest(new { error = "The first row must include a Parish (or Name) column. Other columns: Diocese, City, State." });
            return Results.Ok(await ImportAsync(db, rows, columns, body.DryRun));
        }).RequireAuthorization("ChapterAdmin");
    }

    // ── One parish ───────────────────────────────────────────────────────────

    private static async Task<(Diocese? Diocese, IResult? Error)> ResolveDioceseAsync(LotvDbContext db, ParishInput body)
    {
        if (body.DioceseId is int id)
        {
            var found = await db.Dioceses.FirstOrDefaultAsync(d => d.Id == id);
            return found is null ? (null, Results.BadRequest(new { error = "That diocese doesn't exist." })) : (found, null);
        }
        var all = await db.Dioceses.AsNoTracking().ToListAsync();
        var match = DioceseMatcher.Find(all, body.DioceseName, body.City, body.State);
        if (match.Diocese is null)
            return (null, Results.BadRequest(new
            {
                error = "A parish must belong to a diocese. " + Capitalise(match.How) + ".",
                candidates = match.Candidates.Select(c => new { c.Id, c.Name, c.City, c.State }),
            }));
        return (await db.Dioceses.FirstAsync(d => d.Id == match.Diocese.Id), null);
    }

    private static IResult? Apply(Parish parish, ParishInput body, Diocese diocese)
    {
        var name = (body.Name ?? parish.Name).Trim();
        if (name.Length == 0) return Results.BadRequest(new { error = "The parish needs a name." });
        parish.Name = name;
        parish.DioceseId = diocese.Id;
        parish.DioceseName = diocese.Name;
        parish.ChapterId = diocese.ChapterId;
        parish.City = string.IsNullOrWhiteSpace(body.City) ? parish.City : body.City.Trim();
        if (!string.IsNullOrWhiteSpace(body.State)) parish.State = UsStates.ToCode(body.State) ?? body.State.Trim();
        parish.LiaisonName = body.LiaisonName ?? parish.LiaisonName;
        parish.LiaisonEmail = body.LiaisonEmail ?? parish.LiaisonEmail;
        if (body.Status is { } s) parish.Status = s;
        if (body.CertificationLevel is { } c) parish.CertificationLevel = c;
        return null;
    }

    private static async Task<bool> IsDuplicateAsync(LotvDbContext db, Parish parish, int? excludeId)
    {
        var key = DioceseMatcher.NormalizePlace(parish.Name);
        var city = DioceseMatcher.NormalizePlace(parish.City);
        var same = await db.Parishes.AsNoTracking().Where(p => p.DioceseId == parish.DioceseId && (excludeId == null || p.Id != excludeId)).Select(p => new { p.Name, p.City }).ToListAsync();
        return same.Any(p => DioceseMatcher.NormalizePlace(p.Name) == key && DioceseMatcher.NormalizePlace(p.City) == city);
    }

    // ── Import ───────────────────────────────────────────────────────────────

    private record Columns(int Name, int Diocese, int City, int State);

    private static Columns ReadHeader(List<string> header)
    {
        int Find(params string[] names) => header.FindIndex(h => names.Contains(h.Trim().ToLowerInvariant()));
        return new Columns(
            Find("parish", "name", "parish name", "church", "church name"),
            Find("diocese", "archdiocese", "diocese name"),
            Find("city", "town", "parish city"),
            Find("state", "st", "state code", "parish state"));
    }

    private static string? Cell(List<string> row, int index) => index >= 0 && index < row.Count && !string.IsNullOrWhiteSpace(row[index]) ? row[index].Trim() : null;

    private static async Task<ParishImportResult> ImportAsync(LotvDbContext db, List<List<string>> rows, Columns col, bool dryRun)
    {
        var dioceses = await db.Dioceses.AsNoTracking().ToListAsync();
        // What is already there (and what this file adds as it goes), so a repeated line is a duplicate, not a second parish.
        var seen = (await db.Parishes.AsNoTracking().Select(p => new { p.DioceseId, p.Name, p.City }).ToListAsync())
            .Select(p => Key(p.DioceseId, p.Name, p.City)).ToHashSet();

        int created = 0, duplicates = 0, needsReview = 0, rejected = 0;
        var problems = new List<ParishImportRow>();
        var truncated = false;
        void Problem(ParishImportRow r) { if (problems.Count < MaxProblemRowsReturned) problems.Add(r); else truncated = true; }

        var toAdd = new List<Parish>();
        var touched = new HashSet<int>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var name = Cell(row, col.Name);
            var dioceseText = Cell(row, col.Diocese);
            var city = Cell(row, col.City);
            var state = Cell(row, col.State);
            if (name is null) { rejected++; Problem(new(i + 1, "", city, state, dioceseText, "rejected", "The row has no parish name.", null)); continue; }

            var match = DioceseMatcher.Find(dioceses, dioceseText, city, state);
            if (match.Diocese is null)
            {
                // Named but unknown, or unsure: not added, so no parish is ever left without a diocese.
                var outcome = match.Candidates.Count > 0 ? "needs-review" : "rejected";
                if (outcome == "needs-review") needsReview++; else rejected++;
                Problem(new(i + 1, name, city, state, dioceseText, outcome, Capitalise(match.How) + ".",
                    match.Candidates.Count > 0 ? match.Candidates.Select(c => $"{c.Name} ({c.State})").ToList() : null));
                continue;
            }

            var key = Key(match.Diocese.Id, name, city);
            if (!seen.Add(key)) { duplicates++; continue; }

            created++;
            if (dryRun) continue;
            toAdd.Add(new Parish
            {
                Name = name, DioceseId = match.Diocese.Id, DioceseName = match.Diocese.Name, ChapterId = match.Diocese.ChapterId,
                City = city, State = UsStates.ToCode(state) ?? state, Status = ParishStatus.Active, EnrolledDate = DateTime.UtcNow,
            });
            touched.Add(match.Diocese.Id);
            if (toAdd.Count >= 500) { db.Parishes.AddRange(toAdd); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); toAdd.Clear(); }
        }
        if (toAdd.Count > 0) { db.Parishes.AddRange(toAdd); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
        if (!dryRun && touched.Count > 0) await RecomputeDioceseCountsAsync(db, touched);

        return new ParishImportResult(dryRun, rows.Count - 1, created, duplicates, needsReview, rejected, problems, truncated);
    }

    private static string Key(int dioceseId, string? name, string? city) =>
        $"{dioceseId}|{DioceseMatcher.NormalizePlace(name)}|{DioceseMatcher.NormalizePlace(city)}";

    /// <summary>The diocese's parish counts follow its parish records.</summary>
    public static async Task RecomputeDioceseCountsAsync(LotvDbContext db, IEnumerable<int> dioceseIds)
    {
        var ids = dioceseIds.Distinct().ToList();
        var counts = await db.Parishes.AsNoTracking().Where(p => ids.Contains(p.DioceseId))
            .GroupBy(p => p.DioceseId)
            .Select(g => new { Id = g.Key, Total = g.Count(), Active = g.Count(p => p.Status == ParishStatus.Active) }).ToListAsync();
        foreach (var d in await db.Dioceses.Where(d => ids.Contains(d.Id)).ToListAsync())
        {
            var c = counts.FirstOrDefault(x => x.Id == d.Id);
            d.TotalParishes = c?.Total ?? 0;
            d.ActiveParishes = c?.Active ?? 0;
        }
        await db.SaveChangesAsync();
    }

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
