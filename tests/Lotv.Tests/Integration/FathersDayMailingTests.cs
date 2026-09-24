using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Father's Day card list: one father per family, on his own list beside the Mother's Day list.
/// A family can be on both card lists and the bereavement tracker.
/// </summary>
[Collection("Integration")]
public class FathersDayMailingTests
{
    private static int _nextChapterId = 7000;
    private static int _nextYear = 2070;
    private readonly LotvApiFactory _factory;

    public FathersDayMailingTests(LotvApiFactory factory) => _factory = factory;

    // ── The cycle ─────────────────────────────────────────────────────────────

    [Fact]
    public void FathersDay_IsTheThirdSundayOfJune_AndTheCycleRollsOverAfterIt()
    {
        Assert.Equal(new DateTime(2026, 6, 21), FathersDayCycle.FathersDay(2026));
        Assert.Equal(new DateTime(2027, 6, 20), FathersDayCycle.FathersDay(2027));

        Assert.Equal(2026, MailingCycle.YearFor(MailingKind.FathersDay, new DateTime(2026, 6, 21)));
        Assert.Equal(2027, MailingCycle.YearFor(MailingKind.FathersDay, new DateTime(2026, 6, 22)));
        // Mother's Day (May 10, 2026) has already passed on June 1, Father's Day (June 21) has not.
        Assert.Equal(2027, MailingCycle.YearFor(MailingKind.MothersDay, new DateTime(2026, 6, 1)));
        Assert.Equal(2026, MailingCycle.YearFor(MailingKind.FathersDay, new DateTime(2026, 6, 1)));
    }

    // ── New requests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NewRequest_PutsTheMotherOnTheMothersDayList_AndTheFatherOnTheFathersDayList_OncePerFamily()
    {
        var (familyId, _) = await ApplyAsync(withFather: true);

        var mothers = await EntriesAsync(familyId, MailingKind.MothersDay);
        var fathers = await EntriesAsync(familyId, MailingKind.FathersDay);

        var mother = Assert.Single(mothers);
        var father = Assert.Single(fathers);
        Assert.StartsWith("Ann ", mother.RecipientName);
        Assert.StartsWith("Tom ", father.RecipientName);
        Assert.Equal(MailingCycle.YearFor(MailingKind.FathersDay, DateTime.UtcNow), father.Year);
    }

    [Fact]
    public async Task SingleParentFamily_IsOnTheMothersDayList_ButNotTheFathersDayList()
    {
        var (familyId, _) = await ApplyAsync(withFather: false);

        Assert.Single(await EntriesAsync(familyId, MailingKind.MothersDay));
        Assert.Empty(await EntriesAsync(familyId, MailingKind.FathersDay));
    }

    [Fact]
    public async Task TheApi_ListsEachKindSeparately_AndDefaultsToMothersDay()
    {
        var (familyId, _) = await ApplyAsync(withFather: true);
        var admin = await AdminClientAsync();

        var defaultList = await GetListAsync(admin, "/api/v1/mailing-list");
        var mothersList = await GetListAsync(admin, "/api/v1/mailing-list?kind=MothersDay");
        var fathersList = await GetListAsync(admin, "/api/v1/mailing-list?kind=FathersDay");

        Assert.Contains(defaultList, e => e.FamilyId == familyId && e.Kind == MailingKind.MothersDay);
        Assert.DoesNotContain(defaultList, e => e.Kind == MailingKind.FathersDay);
        Assert.Contains(mothersList, e => e.FamilyId == familyId);
        Assert.Contains(fathersList, e => e.FamilyId == familyId && e.Kind == MailingKind.FathersDay);
        Assert.DoesNotContain(fathersList, e => e.Kind == MailingKind.MothersDay);
    }

    // ── Building from the last year's requests ────────────────────────────────

    [Fact]
    public async Task Build_FillsBothLists_FromRequests_OnceEach_AndSkipsFamiliesWithNoFather()
    {
        var (withDad, _) = await ApplyAsync(withFather: true);
        var (noDad, _) = await ApplyAsync(withFather: false);
        await RemoveEntriesAsync(withDad, noDad);
        var admin = await AdminClientAsync();

        foreach (var kind in new[] { MailingKind.MothersDay, MailingKind.FathersDay })
        {
            var year = MailingCycle.YearFor(kind, DateTime.UtcNow);
            var resp = await admin.PostAsJsonAsync("/api/v1/mailing-list/build", new { Kind = kind, Year = year });
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        Assert.Single(await EntriesAsync(withDad, MailingKind.MothersDay));
        Assert.Single(await EntriesAsync(withDad, MailingKind.FathersDay));
        Assert.Single(await EntriesAsync(noDad, MailingKind.MothersDay));
        Assert.Empty(await EntriesAsync(noDad, MailingKind.FathersDay));

        // Running it again adds nothing new for these families.
        foreach (var kind in new[] { MailingKind.MothersDay, MailingKind.FathersDay })
            await admin.PostAsJsonAsync("/api/v1/mailing-list/build", new { Kind = kind, Year = MailingCycle.YearFor(kind, DateTime.UtcNow) });
        Assert.Single(await EntriesAsync(withDad, MailingKind.MothersDay));
        Assert.Single(await EntriesAsync(withDad, MailingKind.FathersDay));
    }

    [Fact]
    public async Task Build_IsForAdminsOnly()
    {
        var staff = await AdminClientAsync("ChapterStaff");
        var resp = await staff.PostAsJsonAsync("/api/v1/mailing-list/build", new { Kind = MailingKind.FathersDay });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── CSV import ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FathersDayImport_AddsFathers_ToTheFathersDayList_AndSkipsRepeats()
    {
        var year = Interlocked.Increment(ref _nextYear);
        var admin = await AdminClientAsync();
        var csv = "Father Name,Mother Name,Street Address,Apt,City,State,Zip,Country\n" +
                  "Dan Alpha,Mary Alpha,1 First St,,Chicago,IL,60601,United States\n" +
                  "Sam Beta,,2 Second St,,Milwaukee,WI,53202,United States\n";

        var first = await admin.PostAsJsonAsync("/api/v1/mailing-list/import", new { Csv = csv, Year = year, DryRun = false, Kind = MailingKind.FathersDay });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<MailingImportResultDto>(Json);
        Assert.Equal(2, body!.Created);

        var again = await admin.PostAsJsonAsync("/api/v1/mailing-list/import", new { Csv = csv, Year = year, DryRun = false, Kind = MailingKind.FathersDay });
        Assert.Equal(0, (await again.Content.ReadFromJsonAsync<MailingImportResultDto>(Json))!.Created);

        var fathers = await GetListAsync(admin, $"/api/v1/mailing-list?kind=FathersDay&year={year}");
        Assert.Equal(new[] { "Dan Alpha", "Sam Beta" }, fathers.Select(f => f.RecipientName).Order().ToArray());
        Assert.Empty(await GetListAsync(admin, $"/api/v1/mailing-list?kind=MothersDay&year={year}"));
    }

    [Fact]
    public async Task FathersDayImport_NeedsAFatherNameColumn()
    {
        var admin = await AdminClientAsync();
        var csv = "Mother Name,Street Address,City,Zip\nMary,1 A St,Chicago,60601\n";
        var resp = await admin.PostAsJsonAsync("/api/v1/mailing-list/import", new { Csv = csv, Year = Interlocked.Increment(ref _nextYear), Kind = MailingKind.FathersDay });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("father", await resp.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    // ── Upgrading an older database ───────────────────────────────────────────

    [Fact]
    public void Bootstrap_AddsTheKindColumnToAnOlderMailingTable_AndCanRunAgain()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lotv-kind-{Guid.NewGuid():N}.db");
        try
        {
            var cs = $"Data Source={path};Pooling=False";
            using (var conn = new SqliteConnection(cs))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE \"MailingListEntries\" (\"Id\" INTEGER PRIMARY KEY, \"Year\" INTEGER NOT NULL, \"MotherName\" TEXT NOT NULL);" +
                                  "INSERT INTO \"MailingListEntries\" (\"Year\", \"MotherName\") VALUES (2026, 'Old Row');";
                cmd.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<LotvDbContext>().UseSqlite(cs).Options;
            using (var db = new LotvDbContext(options))
            {
                MailingListKindColumnBootstrap.EnsureColumn(db);
                MailingListKindColumnBootstrap.EnsureColumn(db);
            }

            using var check = new SqliteConnection(cs);
            check.Open();
            using var q = check.CreateCommand();
            q.CommandText = "SELECT \"Kind\" FROM \"MailingListEntries\" WHERE \"MotherName\" = 'Old Row'";
            Assert.Equal(0L, q.ExecuteScalar());   // existing rows stay Mother's Day entries
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch { }
        }
    }

    // ── Names that look wrong ─────────────────────────────────────────────────

    [Fact]
    public async Task ABadName_FlagsTheMailingEntryForReview_WithoutBlockingIt()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = Interlocked.Increment(ref _nextChapterId);
        db.Chapters.Add(new Chapter { Id = chapter, Name = $"FD Chapter {chapter}", City = "Testville", State = "IL",
            ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true });
        var family = new Family
        {
            Parent1FirstName = "Tom", Parent1LastName = "Jrny6b6228d8", Parent2FirstName = "Ann", Parent2LastName = "Jrny6b6228d8",
            Email = "x@test.example.com", StreetAddress = "1 A St", City = "Chicago", State = "IL", Zip = "60601", ChapterId = chapter,
        };
        db.Families.Add(family);
        await db.SaveChangesAsync();

        var entry = await MothersDayMailing.EnsureEntryAsync(db, family, possibleDuplicate: false);

        Assert.True(entry.FlaggedForReview);
        Assert.Contains(MothersDayMailing.NameCheckNote, entry.ReviewNote);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private async Task<(int FamilyId, int RequestId)> ApplyAsync(bool withFather)
    {
        var chapter = Interlocked.Increment(ref _nextChapterId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            db.Chapters.Add(new Chapter { Id = chapter, Name = $"FD Chapter {chapter}", City = "Testville", State = "IL",
                ContactName = "Test", ContactEmail = "chapter@test.example.com", IsActive = true });
            await db.SaveChangesAsync();
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Dad{tag}",
                Parent2FirstName = withFather ? "Ann" : null, Parent2LastName = withFather ? $"Dad{tag}" : null,
                Email = $"fd-{tag}@test.example.com", Phone = "", StreetAddress = $"{Random.Shared.Next(100, 999)} Test St",
                City = "Testville", State = "IL", Zip = $"6{Random.Shared.Next(1000, 9999)}", Reason = "Infertility", ChapterId = chapter,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("familyId").GetInt32(), body.GetProperty("requestId").GetInt32());
    }

    private async Task<List<MailingListEntry>> EntriesAsync(int familyId, MailingKind kind)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return await db.MailingListEntries.AsNoTracking().Where(m => m.FamilyId == familyId && m.Kind == kind).ToListAsync();
    }

    private async Task RemoveEntriesAsync(params int[] familyIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.MailingListEntries.RemoveRange(db.MailingListEntries.Where(m => m.FamilyId != null && familyIds.Contains(m.FamilyId.Value)));
        await db.SaveChangesAsync();
    }

    private static async Task<List<MailingListEntry>> GetListAsync(HttpClient client, string url)
    {
        var resp = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<List<MailingListEntry>>(Json))!;
    }

    private async Task<HttpClient> AdminClientAsync(string role = "HQAdmin")
    {
        var client = _factory.CreateClient();
        var email = $"fd-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1FathersDay!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "FD", LastName = "Tester", Role = role, ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
