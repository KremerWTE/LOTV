using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>The public volunteer sign-up (/volunteer): what it stores and where staff approve it.</summary>
[Collection("Integration")]
public class VolunteerSignupTests
{
    private readonly LotvApiFactory _factory;
    public VolunteerSignupTests(LotvApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ASignup_IsStoredAsOnboarding_InARealChapter_AndDoesNotGiveALogin()
    {
        var email = $"signup-{Guid.NewGuid():N}@test.com";
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/volunteer",
            new { firstName = "Sue", lastName = "Signup", email, role = 1, parishName = "St. Test", notes = "I can pack on Saturdays" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var saved = await db.Volunteers.AsNoTracking().SingleAsync(v => v.Email == email);
        Assert.Equal(VolunteerStatus.Onboarding, saved.Status);       // waits for staff to approve it
        Assert.True(await db.Chapters.AnyAsync(c => c.Id == saved.ChapterId), "the signup must belong to a real chapter");
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));  // approving is separate from giving a login
    }

    [Fact]
    public async Task ASignup_SentWithAnApprovedStatus_CannotApproveItself()
    {
        var email = $"signup-{Guid.NewGuid():N}@test.com";
        await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/volunteer",
            new { firstName = "Sly", lastName = "Signup", email, role = 0, status = 0, level = 3 });

        using var scope = _factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<LotvDbContext>().Volunteers.AsNoTracking().SingleAsync(v => v.Email == email);
        Assert.Equal(VolunteerStatus.Onboarding, saved.Status);
    }
}
