using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// The Mother's Day and Father's Day lists include every mother (or father) with a submission since the previous
/// holiday, whether or not an entry was made when the request came in, and the same rule applies to both holidays.
/// </summary>
[Collection("Integration")]
public class MailingListCycleTests
{
    private static int _nextChapterId = 9950;
    private readonly LotvApiFactory _factory;

    public MailingListCycleTests(LotvApiFactory factory) => _factory = factory;

    private record Made(int FamilyId, int RequestId);

    private async Task<int> NewChapterAsync()
    {
        var id = Interlocked.Increment(ref _nextChapterId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        db.Chapters.Add(new Chapter { Id = id, Name = $"Cycle Chapter {id}", City = "Testville", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>A family with a request created at a given moment, with no mailing entry made for it.</summary>
    private async Task<Made> SubmissionAsync(int chapter, DateTime createdAt, bool twoParents = true, FamilyStatus status = FamilyStatus.Active, bool historical = false, string tag = "")
    {
        var name = new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)('g' + (c <= '9' ? c - '0' : c - 'a' + 10) % 20)).ToArray());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var family = new Family
        {
            Parent1FirstName = "Tom", Parent1LastName = $"Cycle{name}", Parent2FirstName = twoParents ? "Ann" : null, Parent2LastName = twoParents ? $"Cycle{name}" : null,
            Email = $"cycle-{name}@test.example.com", StreetAddress = "1 Cycle St", City = "Chicago", State = "IL", Zip = "60601",
            ChapterId = chapter, Status = status, IsHistorical = historical, CreatedAt = createdAt,
        };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var request = new PackageRequest { FamilyId = family.Id, ChapterId = chapter, Reason = PackageReason.Infertility, Status = CaseStatus.New, CreatedAt = createdAt, UpdatedAt = createdAt };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return new Made(family.Id, request.Id);
    }

    private async Task<HttpClient> ClientAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"cycle-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Cycle!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = password, FirstName = "Cy", LastName = "Cle", Role = role, ChapterId = 1 });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static async Task<List<JsonElement>> ListAsync(HttpClient client, MailingKind kind, int year)
    {
        var resp = await client.GetAsync($"/api/v1/mailing-list?kind={kind}&year={year}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static bool Has(List<JsonElement> list, int familyId) =>
        list.Any(e => e.GetProperty("familyId").ValueKind == JsonValueKind.Number && e.GetProperty("familyId").GetInt32() == familyId);

    [Theory]
    [InlineData(MailingKind.MothersDay)]
    [InlineData(MailingKind.FathersDay)]
    public async Task TheList_IncludesEveryoneWithASubmissionSinceTheLastHoliday_WithoutAnyEntryBeingMadeFirst(MailingKind kind)
    {
        var chapter = await NewChapterAsync();
        var year = MailingCycle.YearFor(kind, DateTime.UtcNow);
        var (after, before) = MailingCycle.Window(kind, year);
        var admin = await ClientAsync("HQAdmin");

        var early = await SubmissionAsync(chapter, after.AddHours(2));                                   // the day after the last holiday
        var middle = await SubmissionAsync(chapter, after.AddDays(80));
        var onTheDay = await SubmissionAsync(chapter, before.AddDays(-1).AddHours(12));                  // on this holiday itself
        var beforeWindow = await SubmissionAsync(chapter, after.AddHours(-12));                          // on the last holiday: belongs to the previous cycle
        var afterWindow = await SubmissionAsync(chapter, before.AddHours(2));                            // the day after this holiday: next cycle
        var merged = await SubmissionAsync(chapter, after.AddDays(30), status: FamilyStatus.Closed);      // merged away as a duplicate
        var historical = await SubmissionAsync(chapter, after.AddDays(31), historical: true);             // imported from a prior year

        var list = await ListAsync(admin, kind, year);

        Assert.True(Has(list, early.FamilyId), "the request the day after the last holiday");
        Assert.True(Has(list, middle.FamilyId));
        Assert.True(Has(list, onTheDay.FamilyId), "the request on the holiday itself");
        Assert.False(Has(list, beforeWindow.FamilyId), "belongs to the previous cycle");
        Assert.False(Has(list, afterWindow.FamilyId), "belongs to the next cycle");
        Assert.False(Has(list, merged.FamilyId));
        Assert.False(Has(list, historical.FamilyId));
    }

    [Fact]
    public async Task ARepeatedVisit_AddsNobodyTwice_AndAFamilyWithTwoRequestsIsOneEntry()
    {
        var chapter = await NewChapterAsync();
        var kind = MailingKind.MothersDay;
        var year = MailingCycle.YearFor(kind, DateTime.UtcNow);
        var (after, _) = MailingCycle.Window(kind, year);
        var admin = await ClientAsync("HQAdmin");
        var made = await SubmissionAsync(chapter, after.AddDays(10));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            db.Requests.Add(new PackageRequest { FamilyId = made.FamilyId, ChapterId = chapter, Reason = PackageReason.Stillbirth, CreatedAt = after.AddDays(40), UpdatedAt = after.AddDays(40) });
            await db.SaveChangesAsync();
        }

        await ListAsync(admin, kind, year);
        var second = await ListAsync(admin, kind, year);

        Assert.Single(second, e => e.GetProperty("familyId").ValueKind == JsonValueKind.Number && e.GetProperty("familyId").GetInt32() == made.FamilyId);
    }

    [Fact]
    public async Task TheMothersListHasTheMother_AndTheFathersListHasTheFather_AndSingleParentsHaveNoFather()
    {
        var chapter = await NewChapterAsync();
        var admin = await ClientAsync("HQAdmin");
        var mYear = MailingCycle.YearFor(MailingKind.MothersDay, DateTime.UtcNow);
        var fYear = MailingCycle.YearFor(MailingKind.FathersDay, DateTime.UtcNow);
        var when = DateTime.UtcNow.AddDays(-1);   // inside the current cycle of both holidays
        var couple = await SubmissionAsync(chapter, when);
        var single = await SubmissionAsync(chapter, when, twoParents: false);

        var mothers = await ListAsync(admin, MailingKind.MothersDay, mYear);
        var fathers = await ListAsync(admin, MailingKind.FathersDay, fYear);

        Assert.True(Has(mothers, couple.FamilyId));
        Assert.True(Has(mothers, single.FamilyId));
        Assert.True(Has(fathers, couple.FamilyId));
        Assert.False(Has(fathers, single.FamilyId));
        var mother = mothers.First(e => e.GetProperty("familyId").ValueKind == JsonValueKind.Number && e.GetProperty("familyId").GetInt32() == couple.FamilyId);
        var father = fathers.First(e => e.GetProperty("familyId").ValueKind == JsonValueKind.Number && e.GetProperty("familyId").GetInt32() == couple.FamilyId);
        Assert.StartsWith("Ann ", mother.GetProperty("recipientName").GetString());
        Assert.StartsWith("Tom ", father.GetProperty("recipientName").GetString());
    }

    [Fact]
    public async Task AnyStaffMemberSeesTheFullList()
    {
        var chapter = await NewChapterAsync();
        var kind = MailingKind.MothersDay;
        var year = MailingCycle.YearFor(kind, DateTime.UtcNow);
        var (after, _) = MailingCycle.Window(kind, year);
        var made = await SubmissionAsync(chapter, after.AddDays(5));
        var staff = await ClientAsync("ChapterStaff");

        Assert.True(Has(await ListAsync(staff, kind, year), made.FamilyId));
    }
}
