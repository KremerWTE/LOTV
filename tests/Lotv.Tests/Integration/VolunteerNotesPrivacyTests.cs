using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>A volunteer reading notes on their own case never sees a staff-internal one.</summary>
[Collection("Integration")]
public class VolunteerNotesPrivacyTests
{
    private readonly LotvApiFactory _factory;
    public VolunteerNotesPrivacyTests(LotvApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AVolunteer_SeesOnlyTheirOwnCasesPublicNotes_NeverTheInternalOnes()
    {
        var email = $"notes-vol-{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1Notes!", FirstName = "Nina", LastName = "Notes", Role = "Volunteer", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1Notes!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var vol = new Volunteer { FirstName = "Nina", LastName = "Notes", Email = email, Role = VolunteerRole.PackageAssembler, Status = VolunteerStatus.Active, ChapterId = 1 };
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = 1 };
        db.Volunteers.Add(vol);
        db.Families.Add(family);
        await db.SaveChangesAsync();
        var request = new PackageRequest { FamilyId = family.Id, ChapterId = 1, AssignedToId = vol.Id, AssignedTo = "Nina Notes", Status = CaseStatus.InProgress };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        db.RequestNotes.AddRange(
            new RequestNote { RequestId = request.Id, AuthorId = "s", AuthorName = "Staff", Content = "For the volunteer: thank you!", IsInternal = false },
            new RequestNote { RequestId = request.Id, AuthorId = "s", AuthorName = "Staff", Content = "Sensitive: handle with care, prior miscarriage history", IsInternal = true });
        await db.SaveChangesAsync();

        var notes = await client.GetFromJsonAsync<List<RequestNote>>($"/api/v1/requests/{request.Id}/notes");
        Assert.NotNull(notes);
        Assert.Single(notes!);
        Assert.False(notes![0].IsInternal);
        Assert.DoesNotContain(notes, n => n.Content.Contains("Sensitive"));
    }
}
