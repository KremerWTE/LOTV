using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// The families CSV for GiveButter / a CRM: exact headings, every family (current and
/// historical), contact columns only, safe to open in Excel, HQAdmin only.
/// </summary>
[Collection("Integration")]
public class FamilyCrmExportTests
{
    private const string Url = "/api/v1/export/families-crm";
    private const string Header =
        "Family name,Moms first name,Moms last name,Email address,Phone number,Street address,City,State,Zip,Country";

    private readonly LotvApiFactory _factory;

    public FamilyCrmExportTests(LotvApiFactory factory) => _factory = factory;

    // ── Access ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(Url)).StatusCode);
    }

    [Theory]
    [InlineData("ChapterStaff")]
    [InlineData("ChapterAdmin")]
    public async Task NonHqAdmin_Returns403(string role)
    {
        var client = await AuthedClientAsync(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    // ── Content ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_HasTheExactRequestedHeadings_AndNoSensitiveColumns()
    {
        var csv = await ExportAsync();
        var header = csv.Split("\r\n")[0];

        Assert.Equal(Header, header);
        Assert.DoesNotContain("Reason", header);
        Assert.DoesNotContain("Story", header);
        Assert.DoesNotContain("Notes", header);
    }

    [Fact]
    public async Task Export_IncludesCurrentAndHistoricalFamilies()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateFamilyAsync($"current-{tag}@test.example.com", "Tom", "Smith", "Ann", "Smith");
        var historicalId = await CreateFamilyAsync($"old-{tag}@test.example.com", "Bob", "Jones", null, null);
        await MarkHistoricalAsync(historicalId);

        var csv = await ExportAsync();

        Assert.Contains($"current-{tag}@test.example.com", csv);
        Assert.Contains($"old-{tag}@test.example.com", csv);
    }

    [Fact]
    public async Task Auto_TakesTheSecondParentAsMom_WhenThereIsOne_OtherwiseTheFirst()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateFamilyAsync($"two-{tag}@test.example.com", "Tom", "Smith", "Ann", "Smith");
        await CreateFamilyAsync($"one-{tag}@test.example.com", "Mary", "Jones", null, null);

        var csv = await ExportAsync();

        Assert.Contains($"Smith Family,Ann,Smith,two-{tag}@test.example.com,", csv);
        Assert.Contains($"Jones Family,Mary,Jones,one-{tag}@test.example.com,", csv);
    }

    [Fact]
    public async Task MomParent1_ForcesTheFirstParent()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateFamilyAsync($"two-{tag}@test.example.com", "Tom", "Smith", "Ann", "Smith");

        var csv = await ExportAsync("?mom=parent1");

        Assert.Contains($"Smith Family,Tom,Smith,two-{tag}@test.example.com,", csv);
    }

    [Fact]
    public async Task UnknownMomValue_Returns400()
    {
        var client = await AuthedClientAsync("HQAdmin");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url + "?mom=dad")).StatusCode);
    }

    [Fact]
    public async Task Address_CombinesStreetAndApt_AddsUnitedStates_AndQuotesCommas()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateFamilyAsync($"addr-{tag}@test.example.com", "Tom", "Smith", "Ann", "Smith",
            street: "1 Main St", apt: "Apt 2", city: "Chicago", state: "IL", zip: "60601", phone: "+13125550101");

        var row = (await ExportAsync()).Split("\r\n").Single(l => l.Contains($"addr-{tag}@"));

        Assert.Equal($"Smith Family,Ann,Smith,addr-{tag}@test.example.com,+13125550101,\"1 Main St, Apt 2\",Chicago,IL,60601,United States", row);
    }

    [Fact]
    public async Task SpreadsheetFormulas_AreNeutralised_ButPhoneNumbersAreNot()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await CreateFamilyAsync($"evil-{tag}@test.example.com", "Tom", "Smith", "Ann", "Smith",
            street: "=HYPERLINK(\"http://evil.example\")", phone: "+1 (312) 555-0101");

        var row = (await ExportAsync()).Split("\r\n").Single(l => l.Contains($"evil-{tag}@"));

        Assert.Contains("'=HYPERLINK(", row);          // formula defused with a leading '
        Assert.DoesNotContain(",=HYPERLINK", row);
        Assert.Contains(",+1 (312) 555-0101,", row);     // phone left alone
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> ExportAsync(string query = "")
    {
        var client = await AuthedClientAsync("HQAdmin");
        var resp = await client.GetAsync(Url + query);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.StartsWith("text/csv", resp.Content.Headers.ContentType!.ToString());
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<int> CreateFamilyAsync(string email, string p1First, string p1Last, string? p2First, string? p2Last,
        string street = "1 Test St", string? apt = null, string city = "Chicago", string state = "IL", string zip = "60601", string phone = "")
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = p1First, Parent1LastName = p1Last,
                Parent2FirstName = p2First, Parent2LastName = p2Last,
                Email = email, Phone = phone, StreetAddress = street, Apt = apt,
                City = city, State = state, Zip = zip, Reason = "Infertility", ChapterId = 1,
            },
            ForSelf = true, PackageType = "Comfort",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("familyId").GetInt32();
    }

    private async Task MarkHistoricalAsync(int familyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = await db.Families.SingleAsync(f => f.Id == familyId);
        family.IsHistorical = true;
        family.Status = Lotv.Core.Models.FamilyStatus.Closed;
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> AuthedClientAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"crm-test-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Crm!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Crm", LastName = "Tester", Role = role, ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
