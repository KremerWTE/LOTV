using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

// One-time import of the historical "Prayer Care Package Request Database.xlsx"
// spreadsheet into the real Family/PackageRequest/MailingListEntry tables.
//
// Usage:
//   dotnet run --project tools/LegacyImport -- <path-to-legacy_import.json> <sqlite-connection-string>
//
// Re-runnable: deletes any previously imported rows (tagged to the "Legacy Import"
// chapter / mailing-list Year<=2026) before re-inserting, so it's safe to run again
// after fixing the extraction script.

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/LegacyImport -- <json-path> <sqlite-connection-string>");
    return 1;
}

var jsonPath = args[0];
var connectionString = args[1];

var options = new DbContextOptionsBuilder<LotvDbContext>()
    .UseSqlite(connectionString)
    .Options;

await using var db = new LotvDbContext(options);
await db.Database.EnsureCreatedAsync();

var json = await File.ReadAllTextAsync(jsonPath);
using var doc = JsonDocument.Parse(json);
var root = doc.RootElement;

var chapter = await db.Chapters.FirstOrDefaultAsync(c => c.Name == "Legacy Import");
if (chapter is null)
{
    chapter = new Chapter
    {
        Name = "Legacy Import",
        City = "National",
        State = "US",
        ContactName = "Prayer Care Package Ministry",
        ContactEmail = "info@lotvministry.org",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
    db.Chapters.Add(chapter);
    await db.SaveChangesAsync();
}

// Idempotent re-run: wipe prior import output for this chapter before re-inserting.
var oldRequests = db.Requests.Where(r => r.ChapterId == chapter.Id);
db.Requests.RemoveRange(oldRequests);
var oldFamilies = db.Families.Where(f => f.ChapterId == chapter.Id);
db.Families.RemoveRange(oldFamilies);
db.MailingListEntries.RemoveRange(db.MailingListEntries.Where(m => m.Year == 2026));
await db.SaveChangesAsync();

int caseCount = 0, skipped = 0;
foreach (var c in root.GetProperty("cases").EnumerateArray())
{
    string? S(string name) => c.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    bool B(string name) => c.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    int year = c.GetProperty("year").GetInt32();
    bool isHistorical = B("isHistorical");

    var reasonStr = S("reason") ?? "Other";
    if (!Enum.TryParse<PackageReason>(reasonStr, out var reason))
        reason = PackageReason.Other;

    var dateReceived = DateTime.TryParse(S("dateReceived"), out var dr) ? dr : new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    var dateOfLoss = DateTime.TryParse(S("dateOfLoss"), out var dl) ? dl : (DateTime?)null;
    var completedNote = S("completedNote");
    var isFulfilled = !string.IsNullOrWhiteSpace(completedNote);

    var family = new Family
    {
        Parent1FirstName = S("parent1First") ?? "Unknown",
        Parent1LastName  = S("parent1Last") ?? "",
        Parent2FirstName = S("parent2First"),
        Parent2LastName  = S("parent2Last"),
        Email            = S("email") ?? "",
        Phone            = S("phone"),
        StreetAddress    = S("street") ?? "",
        Apt              = S("apt"),
        City             = S("city") ?? "",
        State            = S("state") ?? "",
        Zip              = S("zip") ?? "",
        Reason           = reason,
        FaithTradition   = S("faith"),
        ChildrenInitials = S("childrenInitials"),
        Story            = S("story"),
        HowHeard         = S("howHeard"),
        ChapterId        = chapter.Id,
        CreatedAt        = DateTime.SpecifyKind(dateReceived, DateTimeKind.Utc),
        Status           = isFulfilled || isHistorical ? FamilyStatus.Closed : FamilyStatus.Active,
        PrivacyPreference = PrivacyPreference.Private,
        DateOfLoss       = dateOfLoss.HasValue ? DateTime.SpecifyKind(dateOfLoss.Value, DateTimeKind.Utc) : null,
        IsHistorical     = isHistorical
    };
    db.Families.Add(family);
    await db.SaveChangesAsync(); // need family.Id for the request FK

    var reasonRaw = S("reasonRaw");
    var notes = new List<string>();
    if (!string.IsNullOrWhiteSpace(completedNote)) notes.Add($"Legacy tracking note: {completedNote}");
    if (reason == PackageReason.Other && !string.IsNullOrWhiteSpace(reasonRaw)) notes.Add($"Original reason text: {reasonRaw}");
    notes.Add($"Imported from spreadsheet ({year} tab).");

    var request = new PackageRequest
    {
        FamilyId    = family.Id,
        IsForSelf   = B("isForSelf"),
        ReferrerName = S("referrerName"),
        Reason      = reason,
        Category    = RequestCategory.PackageDelivery,
        Status      = isFulfilled || isHistorical ? CaseStatus.Fulfilled : CaseStatus.New,
        Priority    = RequestPriority.Normal,
        ChapterId   = chapter.Id,
        CreatedAt   = DateTime.SpecifyKind(dateReceived, DateTimeKind.Utc),
        UpdatedAt   = DateTime.SpecifyKind(dateReceived, DateTimeKind.Utc),
        ChildrenInitials = S("childrenInitials"),
        InternalNotes = string.Join(" ", notes),
        StaffOutreachRequested = false
    };
    db.Requests.Add(request);
    caseCount++;

    if (caseCount % 100 == 0)
    {
        await db.SaveChangesAsync();
        Console.WriteLine($"...{caseCount} cases imported");
    }
}
await db.SaveChangesAsync();

int mailCount = 0;
foreach (var m in root.GetProperty("mailingList").EnumerateArray())
{
    string? S(string name) => m.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    bool B(string name) => m.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    var mother = S("motherName");
    if (string.IsNullOrWhiteSpace(mother)) { skipped++; continue; }

    db.MailingListEntries.Add(new MailingListEntry
    {
        Year            = m.GetProperty("year").GetInt32(),
        MotherName      = mother,
        FatherName      = S("fatherName"),
        StreetAddress   = S("street") ?? "",
        Apt             = S("apt"),
        City            = S("city") ?? "",
        State           = S("state") ?? "",
        Zip             = S("zip") ?? "",
        Country         = S("country"),
        MothersDayOnly  = B("mothersDayOnly"),
        FlaggedForReview = B("flagged"),
        ReviewNote      = S("reviewNote"),
        Sent            = false,
        CreatedAt       = DateTime.UtcNow
    });
    mailCount++;
}
await db.SaveChangesAsync();

Console.WriteLine($"Done. Imported {caseCount} historical/current cases and {mailCount} mailing-list entries ({skipped} skipped for missing name).");
return 0;
