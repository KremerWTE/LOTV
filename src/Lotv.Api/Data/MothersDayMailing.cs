using System.Text;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Data;

/// <summary>
/// Keeps the annual Mother's Day card mailing list up to date: new requests add their family
/// automatically, and staff can bulk-add from a CSV.
/// </summary>
public static class MothersDayMailing
{
    public const string DuplicateNotePrefix = "Possible duplicate family";
    public const string MissingAddressNote = "Address is incomplete";
    public const string NameCheckNote = "A parent's name looks wrong";

    // ── Automatic entries from new requests ──────────────────────────────────

    /// <summary>
    /// Adds the family to the current mailing cycle unless it is already on it. Entries that need a
    /// human look (possible duplicate family, incomplete address) are added flagged for review with a note.
    /// </summary>
    public static async Task<MailingListEntry> EnsureEntryAsync(
        LotvDbContext db, Family family, bool possibleDuplicate, DateTime? now = null)
    {
        var when = now ?? DateTime.UtcNow;
        var mothers = await EnsureKindEntryAsync(db, family, MailingKind.MothersDay, possibleDuplicate, when);
        // Only families with a father on record go on the Father's Day list.
        if (FamilyParents.DadFullName(family) is not null)
            await EnsureKindEntryAsync(db, family, MailingKind.FathersDay, possibleDuplicate, when);
        return mothers;
    }

    private static async Task<MailingListEntry> EnsureKindEntryAsync(
        LotvDbContext db, Family family, MailingKind kind, bool possibleDuplicate, DateTime when)
    {
        var year = MailingCycle.YearFor(kind, when);

        var existing = await db.MailingListEntries.FirstOrDefaultAsync(m => m.FamilyId == family.Id && m.Year == year && m.Kind == kind);
        if (existing is not null) return existing;

        var notes = new List<string>();
        if (possibleDuplicate) notes.Add($"{DuplicateNotePrefix} — check the duplicate review before mailing.");
        if (string.IsNullOrWhiteSpace(family.StreetAddress) || string.IsNullOrWhiteSpace(family.City) || string.IsNullOrWhiteSpace(family.Zip))
            notes.Add(MissingAddressNote + ".");

        if (FamilyDataQuality.Check(family).Issues.Any(i => i.Severity == DataIssueSeverity.Problem
                && (i.Field.Contains("name", StringComparison.OrdinalIgnoreCase))))
            notes.Add(NameCheckNote + " — confirm the spelling before mailing.");

        var entry = new MailingListEntry
        {
            FamilyId = family.Id,
            Year = year,
            Kind = kind,
            MotherName = FamilyParents.MomFullName(family),
            FatherName = FamilyParents.DadFullName(family),
            StreetAddress = family.StreetAddress,
            Apt = family.Apt,
            City = family.City,
            State = family.State,
            Zip = family.Zip,
            FlaggedForReview = notes.Count > 0,
            ReviewNote = notes.Count > 0 ? string.Join(" ", notes) : null,
        };
        db.MailingListEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    /// <summary>
    /// Adds every family with a request in the <paramref name="year"/> cycle (since the previous holiday) that
    /// isn't already on that list. One entry per family; Father's Day skips families with no father on record.
    /// </summary>
    public static async Task<MailingBuildResultDto> BuildFromRequestsAsync(LotvDbContext db, MailingKind kind, int year)
    {
        var (after, before) = MailingCycle.Window(kind, year);
        var requests = await db.Requests.Include(r => r.Family)
            .Where(r => r.CreatedAt >= after && r.CreatedAt < before && r.Family != null
                        && !r.Family.IsHistorical && r.Family.Status != FamilyStatus.Closed)
            .ToListAsync();

        var onList = (await db.MailingListEntries.Where(m => m.Kind == kind && m.Year == year && m.FamilyId != null)
            .Select(m => m.FamilyId!.Value).ToListAsync()).ToHashSet();

        int created = 0, already = 0, noFather = 0;
        foreach (var g in requests.GroupBy(r => r.FamilyId))
        {
            var family = g.First().Family!;
            if (onList.Contains(family.Id)) { already++; continue; }
            if (kind == MailingKind.FathersDay && FamilyParents.DadFullName(family) is null) { noFather++; continue; }
            var possibleDuplicate = g.Any(r => r.NeedsDuplicateReview);
            // Build the entry in the chosen cycle year (not "now") by anchoring the date inside the window.
            await EnsureKindEntryAsync(db, family, kind, possibleDuplicate, after);
            created++;
        }
        return new MailingBuildResultDto(year, created, already, noFather);
    }

    /// <summary>Staff confirmed the family is not a duplicate: clear the flag we set for that reason.</summary>
    public static async Task ClearDuplicateFlagAsync(LotvDbContext db, int familyId)
    {
        var entries = await db.MailingListEntries
            .Where(m => m.FamilyId == familyId && m.FlaggedForReview && m.ReviewNote != null && m.ReviewNote.StartsWith(DuplicateNotePrefix))
            .ToListAsync();
        foreach (var m in entries)
        {
            var dupNote = $"{DuplicateNotePrefix} — check the duplicate review before mailing.";
            var rest = m.ReviewNote!.Replace(dupNote, "").Trim();
            m.FlaggedForReview = rest.Length > 0;
            m.ReviewNote = rest.Length > 0 ? rest : null;
        }
        if (entries.Count > 0) await db.SaveChangesAsync();
    }

    /// <summary>The family was merged into an existing one, which already has its own entry: drop the extra one.</summary>
    public static async Task RemoveUnsentEntriesAsync(LotvDbContext db, int familyId)
    {
        var entries = await db.MailingListEntries.Where(m => m.FamilyId == familyId && !m.Sent).ToListAsync();
        if (entries.Count == 0) return;
        db.MailingListEntries.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    // ── CSV import ───────────────────────────────────────────────────────────

    public const int MaxCsvChars = 2_000_000;
    public const int MaxRows = 5_000;

    public static string[] TemplateHeaders(MailingKind kind) => kind == MailingKind.FathersDay
        ? ["Father Name", "Mother Name", "Street Address", "Apt", "City", "State", "Zip", "Country"]
        : ["Mother Name", "Street Address", "Apt", "City", "State", "Zip", "Country", "Mothers Day Only"];

    private static readonly Dictionary<string, string[]> HeaderAliases = new()
    {
        ["mother"]   = ["mothername", "mothersname", "momname", "momsname", "mothersfullname", "mother", "mom"],
        ["momFirst"] = ["momsfirstname", "momfirstname", "mothersfirstname", "motherfirstname"],
        ["momLast"]  = ["momslastname", "momlastname", "motherslastname", "motherlastname"],
        ["father"]   = ["fathername", "fathersname", "dadname", "dadsname", "father", "dad"],
        ["dadFirst"] = ["dadsfirstname", "dadfirstname", "fathersfirstname", "fatherfirstname"],
        ["dadLast"]  = ["dadslastname", "dadlastname", "fatherslastname", "fatherlastname"],
        ["street"]   = ["streetaddress", "street", "address", "address1", "addressline1"],
        ["apt"]      = ["apt", "apartment", "unit", "suite", "aptsuite", "address2", "addressline2"],
        ["city"]     = ["city", "town"],
        ["state"]    = ["state", "province", "stateprovince"],
        ["zip"]      = ["zip", "zipcode", "postalcode", "postcode"],
        ["country"]  = ["country"],
        ["mdOnly"]   = ["mothersdayonly", "onlymothersday", "mothersday"],
    };

    private static string Normalize(string? s) =>
        new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string DedupeKey(string mother, string street, string zip) =>
        $"{Normalize(mother)}|{Normalize(street)}|{Normalize(zip)}";

    public record ImportError(int Row, string Problem);

    public record ImportResult(int Year, bool DryRun, int TotalRows, int Created, int SkippedDuplicates, List<ImportError> Errors)
    {
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>Parses the CSV, reports what would happen, and (unless <paramref name="dryRun"/>) adds the new rows.</summary>
    public static async Task<(ImportResult? Result, string? Error)> ImportAsync(
        LotvDbContext db, string csv, int year, bool dryRun, MailingKind kind = MailingKind.MothersDay)
    {
        if (string.IsNullOrWhiteSpace(csv)) return (null, "The file is empty.");
        if (csv.Length > MaxCsvChars) return (null, "The file is too large (2 MB max).");
        if (year is < 2000 or > 2100) return (null, "Choose a valid mailing year.");

        var rows = CsvReader.Parse(csv);
        if (rows.Count < 2) return (null, "The file needs a header row and at least one recipient.");
        if (rows.Count - 1 > MaxRows) return (null, $"Too many rows (max {MaxRows:N0} per import).");

        // Map columns by header, ignoring case, spaces, punctuation and apostrophes ("Mom's first name" = "momsfirstname").
        var header = rows[0].Select(Normalize).ToList();
        int Col(string key) => header.FindIndex(h => HeaderAliases[key].Contains(h));
        var cMother = Col("mother"); var cFirst = Col("momFirst"); var cLast = Col("momLast");
        var cFather = Col("father"); var cStreet = Col("street"); var cApt = Col("apt");
        var cCity = Col("city"); var cState = Col("state"); var cZip = Col("zip");
        var cCountry = Col("country"); var cMdOnly = Col("mdOnly");

        var cDadFirst = Col("dadFirst"); var cDadLast = Col("dadLast");
        if (kind == MailingKind.FathersDay)
        {
            if (cFather < 0 && (cDadFirst < 0 || cDadLast < 0))
                return (null, "Couldn't find a father's name column. Use \"Father Name\" (or \"Dads first name\" and \"Dads last name\").");
        }
        else if (cMother < 0 && (cFirst < 0 || cLast < 0))
            return (null, "Couldn't find a mother's name column. Use \"Mother Name\" (or \"Moms first name\" and \"Moms last name\").");
        if (cStreet < 0 || cCity < 0 || cZip < 0)
            return (null, "Couldn't find the address columns. The file needs \"Street Address\", \"City\" and \"Zip\".");

        string Cell(List<string> r, int c) => c >= 0 && c < r.Count ? r[c].Trim() : "";

        var seen = (await db.MailingListEntries.AsNoTracking().Where(m => m.Year == year && m.Kind == kind)
                .Select(m => new { m.MotherName, m.FatherName, m.StreetAddress, m.Zip }).ToListAsync())
            .Select(m => DedupeKey(kind == MailingKind.FathersDay ? m.FatherName ?? "" : m.MotherName, m.StreetAddress, m.Zip)).ToHashSet();

        var errors = new List<ImportError>();
        var toAdd = new List<MailingListEntry>();
        var skipped = 0;
        var total = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.All(string.IsNullOrWhiteSpace)) continue;          // blank line
            total++;
            var line = i + 1;                                         // 1-based, header is line 1

            var mother = cMother >= 0 ? Cell(r, cMother) : $"{Cell(r, cFirst)} {Cell(r, cLast)}".Trim();
            var father = cFather >= 0 ? Cell(r, cFather) : $"{Cell(r, cDadFirst)} {Cell(r, cDadLast)}".Trim();
            var recipient = kind == MailingKind.FathersDay ? father : mother;
            var street = Cell(r, cStreet); var city = Cell(r, cCity); var zip = Cell(r, cZip);

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(recipient)) missing.Add(kind == MailingKind.FathersDay ? "father's name" : "mother's name");
            if (string.IsNullOrWhiteSpace(street)) missing.Add("street address");
            if (string.IsNullOrWhiteSpace(city))   missing.Add("city");
            if (string.IsNullOrWhiteSpace(zip))    missing.Add("zip");
            if (missing.Count > 0) { errors.Add(new ImportError(line, "Missing " + string.Join(", ", missing))); continue; }
            if (recipient.Length > 200 || street.Length > 300) { errors.Add(new ImportError(line, "A value is too long")); continue; }

            if (!seen.Add(DedupeKey(recipient, street, zip))) { skipped++; continue; }   // already on this year's list (or repeated in the file)

            toAdd.Add(new MailingListEntry
            {
                Year = year,
                Kind = kind,
                MotherName = mother,
                FatherName = NullIfBlank(father),
                StreetAddress = street,
                Apt = NullIfBlank(Cell(r, cApt)),
                City = city,
                State = Cell(r, cState),
                Zip = zip,
                Country = NullIfBlank(Cell(r, cCountry)),
                MothersDayOnly = IsYes(Cell(r, cMdOnly)),
            });
        }

        if (!dryRun && toAdd.Count > 0)
        {
            db.MailingListEntries.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
        return (new ImportResult(year, dryRun, total, toAdd.Count, skipped, errors), null);
    }

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static bool IsYes(string s) => Normalize(s) is "yes" or "y" or "true" or "1" or "x";
}

/// <summary>Minimal RFC 4180 CSV reader: quoted fields, doubled quotes, commas and line breaks inside quotes.</summary>
public static class CsvReader
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n': row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
