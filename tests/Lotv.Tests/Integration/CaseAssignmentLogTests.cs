using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>The HIPAA-style case log: who assigned it, who it was reassigned from and to, and who unassigned it.</summary>
[Collection("Integration")]
public class CaseAssignmentLogTests
{
    private readonly LotvApiFactory _factory;
    public CaseAssignmentLogTests(LotvApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"caselog-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = "TestPass1CaseLog!", FirstName = "Cara", LastName = "Logger", Role = "HQAdmin", ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "TestPass1CaseLog!" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        return client;
    }

    private async Task<(int Chapter, int A, int B, int Request)> SetupAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var chapter = new Chapter { Name = $"CaseLog {Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Chapters.Add(chapter);
        var a = new Volunteer { FirstName = "Aaa", LastName = "First", Email = $"a-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.Driver, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        var b = new Volunteer { FirstName = "Bbb", LastName = "Second", Email = $"b-{Guid.NewGuid():N}@test.com", Role = VolunteerRole.Driver, Status = VolunteerStatus.Active, JoinedDate = DateTime.UtcNow };
        var family = new Family { Parent1FirstName = "Fam", Parent1LastName = "Ily", ChapterId = 0 };
        db.Volunteers.AddRange(a, b);
        db.Families.Add(family);
        await db.SaveChangesAsync();
        a.ChapterId = chapter.Id; b.ChapterId = chapter.Id; family.ChapterId = chapter.Id;
        var request = new PackageRequest { FamilyId = family.Id, ChapterId = chapter.Id, Status = CaseStatus.New };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        return (chapter.Id, a.Id, b.Id, request.Id);
    }

    [Fact]
    public async Task AssigningThenReassigning_LogsWhoDidItAndWhoItMovedFromAndTo()
    {
        var (_, a, b, request) = await SetupAsync();
        var admin = await AdminClientAsync();

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = a });
        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = b });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var log = await db.RequestActivities.AsNoTracking().Where(x => x.RequestId == request).OrderBy(x => x.Id).ToListAsync();

        var first = log.Single(x => x.ActivityType == ActivityType.Assigned);
        Assert.Equal("Aaa First", first.NewValue);
        Assert.Null(first.OldValue);
        Assert.Equal("Cara Logger", first.ActorName);

        var second = log.Single(x => x.ActivityType == ActivityType.Reassigned);
        Assert.Equal("Aaa First", second.OldValue);
        Assert.Equal("Bbb Second", second.NewValue);
        Assert.Equal("Cara Logger", second.ActorName);
    }

    [Fact]
    public async Task Unassigning_LogsWhoItWasTakenFromAndWho()
    {
        var (_, a, _, request) = await SetupAsync();
        var admin = await AdminClientAsync();
        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/assign", new { VolunteerId = a });

        await admin.PutAsJsonAsync($"/api/v1/requests/{request}/unassign", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var entry = await db.RequestActivities.AsNoTracking().SingleAsync(x => x.RequestId == request && x.ActivityType == ActivityType.Unassigned);
        Assert.Equal("Aaa First", entry.OldValue);
        Assert.Equal("Cara Logger", entry.ActorName);
    }
}
