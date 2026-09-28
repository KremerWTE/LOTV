using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>What the Prayer List page reads: a volunteer's own assigned families and their own role record.</summary>
[Collection("Integration")]
public class PrayerListTests
{
    private readonly LotvApiFactory _factory;
    public PrayerListTests(LotvApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ADualRoleVolunteer_CanReadTheirOwnRoleRecordAndTheirAssignedFamilies()
    {
        var email = $"pray-{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Pray!", FirstName = "Priya", LastName = "Prayer", Role = "Volunteer", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Pray!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        // Its own chapter — Intake:AutoAssign is on for the whole test run, and a Package Assembler dropped into a
        // chapter another test relies on could pick up that test's request before it gets to assert on it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"Prayer {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        var vol = new Volunteer { FirstName = "Priya", LastName = "Prayer", Email = email, Role = VolunteerRole.PackageAssembler, AdditionalRoles = "PrayerAmbassador", Status = VolunteerStatus.Active, ChapterId = chapter.Id };
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = chapter.Id };
        db.Volunteers.Add(vol);
        db.Families.Add(family);
        await db.SaveChangesAsync();
        db.Requests.Add(new PackageRequest { FamilyId = family.Id, ChapterId = chapter.Id, AssignedToId = vol.Id, AssignedTo = "Priya Prayer", Status = CaseStatus.InProgress });
        await db.SaveChangesAsync();

        var me = await client.GetFromJsonAsync<Volunteer>("/api/v1/volunteers/me", Json);
        Assert.NotNull(me);
        Assert.True(me!.HasRole(VolunteerRole.PrayerAmbassador));

        var mine = await client.GetFromJsonAsync<List<PackageRequest>>("/api/v1/requests/mine", Json);
        Assert.NotNull(mine);
        Assert.Single(mine!);
    }
}
