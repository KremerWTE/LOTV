using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;

namespace Lotv.Tests.Integration;

/// <summary>Bulk-adding recipients to the Mother's Day mailing list from a CSV.</summary>
[Collection("Integration")]
public class MailingListImportTests
{
    private const string Url = "/api/v1/mailing-list/import";
    private const string Header = "Mother Name,Father Name,Street Address,Apt,City,State,Zip,Country,Mothers Day Only";
    private readonly LotvApiFactory _factory;

    public MailingListImportTests(LotvApiFactory factory) => _factory = factory;

    private static int _nextYear = 2040;                                        // valid range is 2000-2100; real cycles are ~2026-2028
    private static int UniqueYear() => Interlocked.Increment(ref _nextYear);

    // ── Access ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoToken_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync(Url, new { Csv = Header, Year = 2030 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ChapterStaff_Returns403()
    {
        var staff = await AuthedClientAsync("ChapterStaff");
        var resp = await staff.PostAsJsonAsync(Url, new { Csv = Header, Year = 2030 });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Importing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_AddsTheRows_ToTheChosenYear_AndTheyAppearOnTheList()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = $"{Header}\n" +
                  "Mary Alpha,Dan Alpha,1 First St,Apt 2,Chicago,IL,60601,United States,\n" +
                  "Jane Beta,,2 Second St,,Milwaukee,WI,53202,United States,yes\n" +
                  "Rose Gamma,Sam Gamma,3 Third St,,Minneapolis,MN,55401,Canada,";

        var result = await ImportAsync(admin, csv, year);

        Assert.Equal(3, result.GetProperty("created").GetInt32());
        var list = (await (await admin.GetAsync($"/api/v1/mailing-list?year={year}")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(new[] { "Jane Beta", "Mary Alpha", "Rose Gamma" }, list.Select(m => m.GetProperty("motherName").GetString()).ToArray());
        var jane = list.Single(m => m.GetProperty("motherName").GetString() == "Jane Beta");
        Assert.True(jane.GetProperty("mothersDayOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, jane.GetProperty("fatherName").ValueKind);
        Assert.Equal("Canada", list.Single(m => m.GetProperty("motherName").GetString() == "Rose Gamma").GetProperty("country").GetString());
    }

    [Fact]
    public async Task ImportingTheSameFileAgain_AddsNothing_AndReportsTheSkips()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = $"{Header}\nMary Alpha,,1 First St,,Chicago,IL,60601,,\nJane Beta,,2 Second St,,Chicago,IL,60601,,";

        await ImportAsync(admin, csv, year);
        var again = await ImportAsync(admin, csv, year);

        Assert.Equal(0, again.GetProperty("created").GetInt32());
        Assert.Equal(2, again.GetProperty("skippedDuplicates").GetInt32());
        var list = await (await admin.GetAsync($"/api/v1/mailing-list?year={year}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task ADryRun_ReportsTheOutcome_ButSavesNothing()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = $"{Header}\nMary Alpha,,1 First St,,Chicago,IL,60601,,\n,,,,,,,,";

        var result = await ImportAsync(admin, csv, year, dryRun: true);

        Assert.True(result.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, result.GetProperty("created").GetInt32());
        var list = await (await admin.GetAsync($"/api/v1/mailing-list?year={year}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task TheCrmExportHeadings_AreAccepted_SoAnExportedFileCanBeImported()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = "Family name,Moms first name,Moms last name,Email address,Phone number,Street address,City,State,Zip,Country\n" +
                  "Smith Family,Ann,Smith,ann@example.com,+13125550101,1 Main St,Chicago,IL,60601,United States";

        var result = await ImportAsync(admin, csv, year);

        Assert.Equal(1, result.GetProperty("created").GetInt32());
        var list = await (await admin.GetAsync($"/api/v1/mailing-list?year={year}")).Content.ReadFromJsonAsync<JsonElement>();
        var entry = list.EnumerateArray().Single();
        Assert.Equal("Ann Smith", entry.GetProperty("motherName").GetString());
        Assert.Equal("United States", entry.GetProperty("country").GetString());
    }

    [Fact]
    public async Task QuotedFields_BomAndWindowsLineEndings_AreHandled()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = "﻿" + Header + "\r\n" +
                  "\"Ann \"\"Annie\"\" Smith\",\"Tom, Jr.\",\"1 Main St, Unit 5\",,Chicago,IL,60601,,\r\n";

        var result = await ImportAsync(admin, csv, year);

        Assert.Equal(1, result.GetProperty("created").GetInt32());
        var list = await (await admin.GetAsync($"/api/v1/mailing-list?year={year}")).Content.ReadFromJsonAsync<JsonElement>();
        var entry = list.EnumerateArray().Single();
        Assert.Equal("Ann \"Annie\" Smith", entry.GetProperty("motherName").GetString());
        Assert.Equal("Tom, Jr.", entry.GetProperty("fatherName").GetString());
        Assert.Equal("1 Main St, Unit 5", entry.GetProperty("streetAddress").GetString());
    }

    [Fact]
    public async Task BadRows_AreReportedWithTheirLineNumbers_AndTheGoodRowsAreStillImported()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = $"{Header}\n" +
                  "Good One,,1 First St,,Chicago,IL,60601,,\n" +      // line 2
                  ",,2 Second St,,Chicago,IL,60601,,\n" +             // line 3: no mother
                  "No Address,,,,Chicago,IL,,,\n" +                   // line 4: no street, no zip
                  "Good Two,,3 Third St,,Chicago,IL,60601,,";         // line 5

        var result = await ImportAsync(admin, csv, year);

        Assert.Equal(4, result.GetProperty("totalRows").GetInt32());
        Assert.Equal(2, result.GetProperty("created").GetInt32());
        var errors = result.GetProperty("errors").EnumerateArray().ToList();
        Assert.Equal(new[] { 3, 4 }, errors.Select(e => e.GetProperty("row").GetInt32()).ToArray());
        Assert.Contains("mother's name", errors[0].GetProperty("problem").GetString());
        Assert.Contains("street address", errors[1].GetProperty("problem").GetString());
    }

    [Fact]
    public async Task ARepeatedRowInTheSameFile_IsOnlyAddedOnce()
    {
        var year = UniqueYear();
        var admin = await AuthedClientAsync("HQAdmin");
        var csv = $"{Header}\nMary Alpha,,1 First St,,Chicago,IL,60601,,\nMARY ALPHA,,1 first st,,Chicago,IL,60601,,";

        var result = await ImportAsync(admin, csv, year);

        Assert.Equal(1, result.GetProperty("created").GetInt32());
        Assert.Equal(1, result.GetProperty("skippedDuplicates").GetInt32());
    }

    [Fact]
    public async Task AMotherAlreadyAddedFromARequest_IsNotAddedAgainByAnImport()
    {
        // Same mother + address as an automatically added entry (this year's cycle) counts as a duplicate.
        var admin = await AuthedClientAsync("HQAdmin");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var apply = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Tom", Parent1LastName = $"Auto{tag}", Parent2FirstName = "Ann", Parent2LastName = $"Auto{tag}",
                Email = $"auto-{tag}@test.example.com", StreetAddress = "9 Card Lane", City = "Chicago", State = "IL", Zip = "60609",
                Reason = "Infertility", ChapterId = 1,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var csv = $"{Header}\nAnn Auto{tag},Tom Auto{tag},9 Card Lane,,Chicago,IL,60609,,";
        var result = await ImportAsync(admin, csv, MothersDayCycleYear());

        Assert.Equal(0, result.GetProperty("created").GetInt32());
        Assert.Equal(1, result.GetProperty("skippedDuplicates").GetInt32());
    }

    // ── Rejected files ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "empty")]
    [InlineData("Mother Name\nMary", "address columns")]
    [InlineData("Street Address,City,Zip\n1 First St,Chicago,60601", "mother's name column")]
    [InlineData("Mother Name,Street Address,City,Zip", "at least one recipient")]
    public async Task UnusableFiles_Return400_WithAHelpfulMessage(string csv, string expectedInMessage)
    {
        var admin = await AuthedClientAsync("HQAdmin");

        var resp = await admin.PostAsJsonAsync(Url, new { Csv = csv, Year = UniqueYear() });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains(expectedInMessage, error);
    }

    [Fact]
    public void CsvReader_HandlesQuotesCommasAndNewlinesInsideFields()
    {
        var rows = CsvReader.Parse("a,\"b,1\",\"line1\nline2\"\r\n\"q\"\"uote\",,c\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "a", "b,1", "line1\nline2" }, rows[0].ToArray());
        Assert.Equal(new[] { "q\"uote", "", "c" }, rows[1].ToArray());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int MothersDayCycleYear() => Lotv.Core.Models.MothersDayCycle.YearFor(DateTime.UtcNow);

    private static async Task<JsonElement> ImportAsync(HttpClient client, string csv, int year, bool dryRun = false)
    {
        var resp = await client.PostAsJsonAsync(Url, new { Csv = csv, Year = year, DryRun = dryRun });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> AuthedClientAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"mailimport-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Mail!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Mail", LastName = "Tester", Role = role, ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
