using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>Every parish belongs to a diocese: it is required on create and edit, and an import never adds a parish it can't place.</summary>
[Collection("Integration")]
public class ParishTests : IAsyncLifetime
{
    private static int _nextChapterId = 9700;   // shared, so no two tests ever pick the same chapter
    private readonly LotvApiFactory _factory;
    private int _chapter;

    public ParishTests(LotvApiFactory factory) => _factory = factory;

    // The diocese list is shared, so each test starts from (and leaves) an empty diocese and parish list.
    public async Task InitializeAsync() => await ResetAsync();
    public async Task DisposeAsync() => await ResetAsync();

    private async Task ResetAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Parishes.RemoveRange(db.Parishes);
        db.Dioceses.RemoveRange(db.Dioceses);
        await db.SaveChangesAsync();
        if (_chapter == 0)
        {
            _chapter = Interlocked.Increment(ref _nextChapterId);
            db.Chapters.Add(new Chapter { Id = _chapter, Name = $"Parish tests {_chapter}", City = "T", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
            await db.SaveChangesAsync();
        }
    }

    private async Task<int> AddDioceseAsync(string name, string city, string state)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var d = new Diocese { Name = name, City = city, State = state, ChapterId = _chapter };
        db.Dioceses.Add(d);
        await db.SaveChangesAsync();
        return d.Id;
    }

    private async Task SeedTheUsualDiocesesAsync()
    {
        await AddDioceseAsync("Archdiocese of Chicago", "Chicago", "IL");
        await AddDioceseAsync("Diocese of Joliet", "Joliet", "IL");
        await AddDioceseAsync("Diocese of Cheyenne", "Cheyenne", "WY");
    }

    private async Task<HttpClient> ClientAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"parish-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Parish!", FirstName = "P", LastName = "Tester", Role = role, ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Parish!" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<Diocese> DioceseAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LotvDbContext>().Dioceses.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    // ── One parish ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AParishWithNoDiocese_IsRefused_WithAReasonAndTheChoices()
    {
        await SeedTheUsualDiocesesAsync();
        var admin = await ClientAsync("HQAdmin");

        // Illinois has two dioceses here and Naperville is not a seat, so it can't be worked out.
        var resp = await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "St. Raphael", city = "Naperville", state = "IL" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("must belong to a diocese", body.GetProperty("error").GetString());
        Assert.Equal(2, body.GetProperty("candidates").GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "No Place At All" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "Bad Id", dioceseId = 999999 })).StatusCode);
    }

    [Fact]
    public async Task AParish_TakesItsDiocesesNameAndChapter_AndTheDiocesesCountsFollow()
    {
        var chicago = await AddDioceseAsync("Archdiocese of Chicago", "Chicago", "IL");
        var admin = await ClientAsync("HQAdmin");

        var created = await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "  St. Anne  ", dioceseId = chicago, city = "Oak Park", state = "Illinois" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var parish = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("St. Anne", parish.GetProperty("name").GetString());
        Assert.Equal("Archdiocese of Chicago", parish.GetProperty("dioceseName").GetString());
        Assert.Equal(_chapter, parish.GetProperty("chapterId").GetInt32());
        Assert.Equal("IL", parish.GetProperty("state").GetString());

        var d = await DioceseAsync(chicago);
        Assert.Equal(1, d.TotalParishes);
        Assert.Equal(1, d.ActiveParishes);
    }

    [Fact]
    public async Task ADioceseCanBeWorkedOutFromTheCityAndState_WhenItIsCertain()
    {
        await SeedTheUsualDiocesesAsync();
        var admin = await ClientAsync("HQAdmin");

        // A seat city, and a state with one diocese.
        var seat = await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "Holy Name Cathedral", city = "Chicago", state = "IL" });
        var only = await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "St. Mary", city = "Laramie", state = "WY" });
        Assert.Equal(HttpStatusCode.Created, seat.StatusCode);
        Assert.Equal(HttpStatusCode.Created, only.StatusCode);
        Assert.Equal("Archdiocese of Chicago", (await seat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dioceseName").GetString());
        Assert.Equal("Diocese of Cheyenne", (await only.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dioceseName").GetString());
    }

    [Fact]
    public async Task AnEdit_KeepsTheDiocese_ItCanBeMoved_ButNeverLeftWithNone()
    {
        var chicago = await AddDioceseAsync("Archdiocese of Chicago", "Chicago", "IL");
        var joliet = await AddDioceseAsync("Diocese of Joliet", "Joliet", "IL");
        var admin = await ClientAsync("HQAdmin");
        var id = (await (await admin.PostAsJsonAsync("/api/v1/parishes", new { name = "St. Peter", dioceseId = chicago })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Renaming, with no diocese mentioned, leaves it where it is.
        var renamed = await admin.PutAsJsonAsync($"/api/v1/parishes/{id}", new { name = "St. Peter the Apostle" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Archdiocese of Chicago", (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dioceseName").GetString());

        // Moving it updates both dioceses' counts; a diocese that doesn't exist is refused.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/parishes/{id}", new { dioceseId = joliet })).StatusCode);
        Assert.Equal(0, (await DioceseAsync(chicago)).TotalParishes);
        Assert.Equal(1, (await DioceseAsync(joliet)).TotalParishes);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/parishes/{id}", new { dioceseId = 999999 })).StatusCode);
    }

    [Fact]
    public async Task ThePersonWhoWritesMustBeAnAdmin_AndVolunteersCannotEvenRead()
    {
        var chicago = await AddDioceseAsync("Archdiocese of Chicago", "Chicago", "IL");
        var staff = await ClientAsync("ChapterStaff");
        var volunteer = await ClientAsync("Volunteer");

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/parishes")).StatusCode);                 // staff can read
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/parishes", new { name = "X", dioceseId = chicago })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/parishes/import", new { csv = "parish\nX" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await volunteer.GetAsync("/api/v1/parishes")).StatusCode);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    private const string ImportFile = """
        Parish,Diocese,City,State
        Holy Name Cathedral,Archdiocese of Chicago,Chicago,IL
        "St. Mary, Queen of Peace",Diocese of Joliet,Joliet,IL
        Holy Name Cathedral,Archdiocese of Chicago,Chicago,IL
        St. Laramie,,Laramie,WY
        St. Someone,,Naperville,IL
        St. Lost,Diocese of Nowhere,Nowhere,IL
        ,Diocese of Joliet,Joliet,IL
        """;

    [Fact]
    public async Task ADryRunReportsEverythingAndChangesNothing()
    {
        await SeedTheUsualDiocesesAsync();
        var admin = await ClientAsync("HQAdmin");

        var result = await (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = ImportFile, dryRun = true })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(result.GetProperty("dryRun").GetBoolean());
        Assert.Equal(7, result.GetProperty("rows").GetInt32());
        Assert.Equal(3, result.GetProperty("created").GetInt32());        // Holy Name, St. Mary, St. Laramie (state has one diocese)
        Assert.Equal(1, result.GetProperty("duplicates").GetInt32());     // Holy Name listed twice
        Assert.Equal(1, result.GetProperty("needsReview").GetInt32());    // Naperville: two dioceses in IL, not a seat
        Assert.Equal(2, result.GetProperty("rejected").GetInt32());       // unknown diocese, and the row with no name
        var review = result.GetProperty("problems").EnumerateArray().Single(p => p.GetProperty("outcome").GetString() == "needs-review");
        Assert.Equal("St. Someone", review.GetProperty("name").GetString());
        Assert.Equal(2, review.GetProperty("candidates").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<LotvDbContext>().Parishes.CountAsync());
    }

    [Fact]
    public async Task TheRealImport_AddsOnlyWhatItCanPlace_LeavesNoParishWithoutADiocese_AndIsSafeToRepeat()
    {
        await SeedTheUsualDiocesesAsync();
        var admin = await ClientAsync("HQAdmin");

        var result = await (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = ImportFile, dryRun = false })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("dryRun").GetBoolean());
        Assert.Equal(3, result.GetProperty("created").GetInt32());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            var parishes = await db.Parishes.AsNoTracking().ToListAsync();
            Assert.Equal(3, parishes.Count);
            var dioceseIds = await db.Dioceses.Select(d => d.Id).ToListAsync();
            Assert.All(parishes, p => Assert.Contains(p.DioceseId, dioceseIds));   // nothing without a real diocese
            Assert.Contains(parishes, p => p.Name == "St. Mary, Queen of Peace" && p.DioceseName == "Diocese of Joliet");
            Assert.Contains(parishes, p => p.Name == "St. Laramie" && p.DioceseName == "Diocese of Cheyenne" && p.State == "WY");
            Assert.Equal(1, (await db.Dioceses.AsNoTracking().SingleAsync(d => d.Name == "Diocese of Cheyenne")).TotalParishes);
        }

        // Importing the same file again adds nothing new.
        var again = await (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = ImportFile, dryRun = false })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, again.GetProperty("created").GetInt32());
        Assert.Equal(4, again.GetProperty("duplicates").GetInt32());   // the three added plus the file's own repeat
    }

    [Fact]
    public async Task AFileWithNoParishColumn_OrNoRows_IsRefused()
    {
        var admin = await ClientAsync("HQAdmin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = "Diocese,City\nX,Y" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = "Parish" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/parishes/import", new { csv = "" })).StatusCode);
    }

    // ── Existing parishes ────────────────────────────────────────────────────

    [Fact]
    public async Task StartupRepair_LinksAnOrphanedParishByItsDioceseName_AndLeavesTheOnesItCannotPlace()
    {
        var chicago = await AddDioceseAsync("Archdiocese of Chicago", "Chicago", "IL");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Parishes.AddRange(
            new Parish { Name = "Orphan With A Name", DioceseId = 0, DioceseName = "Chicago Archdiocese", ChapterId = _chapter },
            new Parish { Name = "Orphan With Nothing", DioceseId = 0, DioceseName = "", ChapterId = _chapter });
        await db.SaveChangesAsync();

        var (linked, still) = await ParishDioceseRepair.RunAsync(db);

        Assert.Equal(1, linked);
        Assert.Equal(1, still);
        db.ChangeTracker.Clear();
        Assert.Equal(chicago, (await db.Parishes.SingleAsync(p => p.Name == "Orphan With A Name")).DioceseId);
        Assert.Equal(0, (await db.Parishes.SingleAsync(p => p.Name == "Orphan With Nothing")).DioceseId);
        Assert.Equal(1, (await db.Dioceses.SingleAsync(d => d.Id == chicago)).TotalParishes);
    }
}
