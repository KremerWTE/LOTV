using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>
/// Board and staff get access to everything (Board read-only); volunteers get only the prayer request package,
/// meaning their own assigned cases.
/// </summary>
[Collection("Integration")]
public class AccessRuleTests
{
    private const string Password = "TestPass1AccessRule!";
    private readonly LotvApiFactory _factory;

    public AccessRuleTests(LotvApiFactory factory) => _factory = factory;

    private record Account(string Email, HttpClient Client);

    private async Task<Account> AccountAsync(string role, string first = "Test", string last = "Person")
    {
        var client = _factory.CreateClient();
        var email = $"access-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password, FirstName = first, LastName = last, Role = role, ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return new Account(email, client);
    }

    /// <summary>A volunteer login with a matching volunteer record, one case assigned to them and one to someone else.</summary>
    private async Task<(Account Volunteer, int Mine, int NotMine)> VolunteerWithCasesAsync()
    {
        var acct = await AccountAsync("Volunteer", "Vera", "Volunteer");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var me = new Volunteer { FirstName = "Vera", LastName = "Volunteer", Email = acct.Email, Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Inactive, ChapterId = 1 };
        var other = new Volunteer { FirstName = "Olga", LastName = "Other", Email = $"other-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Inactive, ChapterId = 1 };
        db.Volunteers.AddRange(me, other);
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = 1 };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var mineReq = new PackageRequest { FamilyId = family.Id, ChapterId = 1, AssignedToId = me.Id, AssignedTo = "Vera Volunteer", Status = CaseStatus.InProgress };
        var otherReq = new PackageRequest { FamilyId = family.Id, ChapterId = 1, AssignedToId = other.Id, AssignedTo = "Olga Other", Status = CaseStatus.InProgress };
        db.Requests.AddRange(mineReq, otherReq);
        await db.SaveChangesAsync();
        return (acct, mineReq.Id, otherReq.Id);
    }

    [Fact]
    public async Task AVolunteer_SeesTheirOwnCases_AndTheirOwnCaseDetail()
    {
        var (v, mine, _) = await VolunteerWithCasesAsync();

        var list = await v.Client.GetAsync("/api/v1/requests/mine");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains($"\"id\":{mine}", await list.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await v.Client.GetAsync($"/api/v1/requests/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await v.Client.GetAsync($"/api/v1/requests/{mine}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await v.Client.GetAsync("/api/v1/volunteers/me")).StatusCode);
    }

    [Fact]
    public async Task AVolunteer_CannotOpenSomeoneElsesCase()
    {
        var (v, _, notMine) = await VolunteerWithCasesAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.GetAsync($"/api/v1/requests/{notMine}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.GetAsync($"/api/v1/requests/{notMine}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.PutAsJsonAsync($"/api/v1/requests/{notMine}/process-stage", new { Stage = "Packing" })).StatusCode);
    }

    [Fact]
    public async Task AVolunteer_CannotUseAnythingBeyondThePackage()
    {
        var (v, mine, _) = await VolunteerWithCasesAsync();

        foreach (var path in new[] { "/api/v1/requests", "/api/v1/requests/queue", "/api/v1/families", "/api/v1/volunteers", "/api/v1/donors",
                                     "/api/v1/donations", "/api/v1/dashboard/stats", "/api/v1/dioceses", "/api/v1/parishes", "/api/v1/settings", "/api/v1/inventory" })
            Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.GetAsync(path)).StatusCode);

        // even on their own case: assigning, unassigning and priority are staff decisions
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.PutAsJsonAsync($"/api/v1/requests/{mine}/unassign", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.PutAsJsonAsync($"/api/v1/requests/{mine}/assign", new { VolunteerId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await v.Client.PostAsJsonAsync("/api/v1/requests", new { FamilyId = 1 })).StatusCode);
    }

    [Theory]
    [InlineData("ChapterStaff")]
    [InlineData("ChapterAdmin")]
    [InlineData("Director")]
    [InlineData("HQAdmin")]
    public async Task Staff_HaveAccessToAllItems(string role)
    {
        var s = await AccountAsync(role);
        foreach (var path in new[] { "/api/v1/requests", "/api/v1/families", "/api/v1/volunteers", "/api/v1/dashboard/stats" })
            Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Board_SeesAllItems_ButCannotChangeAnything()
    {
        var b = await AccountAsync("Board");

        foreach (var path in new[] { "/api/v1/requests", "/api/v1/families", "/api/v1/volunteers", "/api/v1/donors", "/api/v1/donations",
                                     "/api/v1/dashboard/stats", "/api/v1/dioceses", "/api/v1/board/summary" })
            Assert.Equal(HttpStatusCode.OK, (await b.Client.GetAsync(path)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PostAsJsonAsync("/api/v1/requests", new { FamilyId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PutAsJsonAsync("/api/v1/requests/1/priority", new { Priority = "High" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.PostAsJsonAsync("/api/v1/volunteers", new { FirstName = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.DeleteAsync("/api/v1/requests/1/items/1")).StatusCode);
    }

    [Fact]
    public async Task Board_StaysOutOfHqAdminAreas()
    {
        var b = await AccountAsync("Board");
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync("/api/v1/apikeys")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync("/api/v1/chapters")).StatusCode);
    }
}
