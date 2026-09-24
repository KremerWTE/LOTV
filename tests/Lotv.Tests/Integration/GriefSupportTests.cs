using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>The "quarterly Grief Support" answer on the request form is saved on the family, not just written into notes.</summary>
[Collection("Integration")]
public class GriefSupportTests
{
    private readonly LotvApiFactory _factory;

    public GriefSupportTests(LotvApiFactory factory) => _factory = factory;

    private async Task<int> ApplyAsync(string reason, bool? grief)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = "Griefless", Parent2FirstName = "Ann", Parent2LastName = "Griefless",
                Email = $"grief-{tag}@test.example.com", Phone = "", StreetAddress = "1 Test St", City = "Testville", State = "IL",
                Zip = "60601", Reason = reason, ChapterId = 1, GriefSupportRequested = grief,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("familyId").GetInt32();
    }

    private async Task<bool?> SavedAnswerAsync(int familyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        return (await db.Families.AsNoTracking().SingleAsync(f => f.Id == familyId)).GriefSupportRequested;
    }

    [Theory]
    [InlineData("Stillbirth", true)]
    [InlineData("InfantLoss", true)]
    [InlineData("Stillbirth", false)]
    public async Task TheAnswer_IsSavedForStillbirthAndInfantLoss(string reason, bool answer)
    {
        Assert.Equal(answer, await SavedAnswerAsync(await ApplyAsync(reason, answer)));
    }

    [Theory]
    [InlineData("Infertility")]
    [InlineData("Miscarriage")]
    public async Task ForOtherReasons_AnAnswerIsNotKept(string reason)
    {
        Assert.Null(await SavedAnswerAsync(await ApplyAsync(reason, true)));
    }

    [Fact]
    public async Task NoAnswer_StaysUnanswered()
    {
        Assert.Null(await SavedAnswerAsync(await ApplyAsync("Stillbirth", null)));
    }

    [Fact]
    public void Bootstrap_AddsTheColumn_AndFillsItFromTheOldNotesSentence_AndCanRunAgain()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lotv-grief-{Guid.NewGuid():N}.db");
        try
        {
            var cs = $"Data Source={path};Pooling=False";
            using (var conn = new SqliteConnection(cs))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE \"Families\" (\"Id\" INTEGER PRIMARY KEY, \"ContactNotes\" TEXT NULL);" +
                                  "INSERT INTO \"Families\" (\"Id\", \"ContactNotes\") VALUES (1, 'Quarterly Grief Support requested: Yes | Opted in to: Newsletter');" +
                                  "INSERT INTO \"Families\" (\"Id\", \"ContactNotes\") VALUES (2, 'Quarterly Grief Support requested: No');" +
                                  "INSERT INTO \"Families\" (\"Id\", \"ContactNotes\") VALUES (3, 'Something else');";
                cmd.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<LotvDbContext>().UseSqlite(cs).Options;
            using (var db = new LotvDbContext(options))
            {
                FamilyGriefSupportColumnBootstrap.EnsureColumn(db);
                FamilyGriefSupportColumnBootstrap.EnsureColumn(db);
            }

            using var check = new SqliteConnection(cs);
            check.Open();
            using var q = check.CreateCommand();
            q.CommandText = "SELECT \"Id\", \"GriefSupportRequested\" FROM \"Families\" ORDER BY \"Id\"";
            using var r = q.ExecuteReader();
            var values = new List<object>();
            while (r.Read()) values.Add(r.IsDBNull(1) ? "null" : r.GetInt64(1));
            Assert.Equal(new object[] { 1L, 0L, "null" }, values.ToArray());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch { }
        }
    }
}
