using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lotv.Tests.Integration;

/// <summary>"Login As": narrow, time-limited, audited, and it changes nothing about the account being viewed.</summary>
[Collection("Integration")]
public class LoginAsTests
{
    private const string Password = "TestPass1LoginAs!";
    private readonly LotvApiFactory _factory;

    public LoginAsTests(LotvApiFactory factory) => _factory = factory;

    private record Account(string Email, string Id, string Name, HttpClient Client);

    private async Task<Account> AccountAsync(string role, string first, string last)
    {
        var client = _factory.CreateClient();
        var email = $"loginas-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password, FirstName = first, LastName = last, Role = role, ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken);
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>().FindByEmailAsync(email);
        return new Account(email, user!.Id, $"{first} {last}", client);
    }

    private HttpClient WithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> TokenAsync(HttpResponseMessage resp) =>
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

    private async Task<List<AuditEntry>> AuditAsync(string action, string entityId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LotvDbContext>().AuditEntries.AsNoTracking()
            .Where(a => a.Action == action && a.EntityId == entityId).ToListAsync();
    }

    [Fact]
    public async Task AnHqAdmin_CanSeeThePortalAsAStaffMember_WithATimeLimitedTokenAndNoRefreshToken()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var staff = await AccountAsync("ChapterStaff", "Sam", "Staffer");

        var resp = await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = staff.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("refreshToken", out _));   // it can only expire, never be renewed
        Assert.Equal("Ada Admin", body.GetProperty("impersonatedBy").GetString());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.GetProperty("accessToken").GetString());
        Assert.Equal("ChapterStaff", jwt.Claims.First(c => c.Type == "role").Value);        // acts as the staff member, not as an admin
        Assert.Equal(staff.Id, jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal(admin.Id, jwt.Claims.First(c => c.Type == "impersonated_by").Value);
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 25, 31);

        // ...and it really is staff access: it opens the staff API but not the admin-only one.
        var asStaff = WithToken(body.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await asStaff.GetAsync("/api/v1/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asStaff.PostAsJsonAsync("/api/v1/dioceses/load-us-directory", new { dryRun = true })).StatusCode);
    }

    [Fact]
    public async Task ForAVolunteer_TheAdminSeesWhatAVolunteerSees_NotTheStaffPortalData()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var volunteer = await AccountAsync("Volunteer", "Vera", "Volunteer");

        var token = await TokenAsync(await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = volunteer.Id }));
        var asVolunteer = WithToken(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await asVolunteer.GetAsync("/api/v1/requests")).StatusCode);   // exactly what a volunteer gets
        Assert.Equal(HttpStatusCode.Forbidden, (await asVolunteer.GetAsync("/api/v1/families")).StatusCode);
    }

    [Fact]
    public async Task ItCannotBeUsedOnYourself_AnotherAdmin_ATurnedOffAccount_OrAPersonWhoDoesNotExist()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var otherAdmin = await AccountAsync("HQAdmin", "Bea", "Boss");
        var off = await AccountAsync("ChapterStaff", "Ola", "Off");
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
            var u = (await users.FindByIdAsync(off.Id))!;
            u.IsActive = false;
            await users.UpdateAsync(u);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = admin.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = otherAdmin.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = off.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = "no-such-user" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = "" })).StatusCode);
    }

    [Fact]
    public async Task OnlyAnHqAdmin_CanDoIt_AndItCannotBeNested()
    {
        var chapterAdmin = await AccountAsync("ChapterAdmin", "Cara", "ChapterAdmin");
        var staff = await AccountAsync("ChapterStaff", "Sam", "Staffer");
        var volunteer = await AccountAsync("Volunteer", "Vera", "Volunteer");
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await chapterAdmin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = volunteer.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = volunteer.Id })).StatusCode);

        // Signed in as someone else, you cannot then sign in as a third person.
        var asStaff = WithToken(await TokenAsync(await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = staff.Id })));
        var nested = await asStaff.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = volunteer.Id });
        Assert.True(nested.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheStartAndTheEnd_AreBothRecordedInTheAuditLog_WithBothNames()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var staff = await AccountAsync("ChapterStaff", "Sam", "Staffer");

        var asStaff = WithToken(await TokenAsync(await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = staff.Id })));
        var started = Assert.Single(await AuditAsync("LoginAsStarted", staff.Id));
        Assert.Equal("Ada Admin", started.UserName);
        Assert.Contains("Ada Admin signed in as Sam Staffer", started.Details);

        Assert.Equal(HttpStatusCode.OK, (await asStaff.PostAsync("/api/v1/auth/impersonate/end", null)).StatusCode);
        var ended = Assert.Single(await AuditAsync("LoginAsEnded", staff.Id));
        Assert.Equal("Ada Admin", ended.UserName);
        Assert.Contains("returned to their own account", ended.Details);
    }

    [Fact]
    public async Task WhatIsDoneWhileSignedInAs_IsRecordedUnderBothNames()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var staff = await AccountAsync("ChapterStaff", "Sam", "Staffer");
        var tag = Guid.NewGuid().ToString("N")[..8];
        int chapter;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
            chapter = await db.Chapters.Select(c => c.Id).FirstOrDefaultAsync();
            if (chapter == 0) { db.Chapters.Add(new Chapter { Id = 9810, Name = "Login as", City = "T", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true }); await db.SaveChangesAsync(); chapter = 9810; }
        }
        var apply = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new { Parent1FirstName = "As", Parent1LastName = $"Seen{tag}", Email = $"as-{tag}@test.example.com", StreetAddress = "1 St", City = "Town", State = "IL", Zip = "60601", Reason = "Infertility", ChapterId = chapter },
            ForSelf = true,
        });
        var request = (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetInt32();

        var asStaff = WithToken(await TokenAsync(await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = staff.Id })));
        Assert.Equal(HttpStatusCode.OK, (await asStaff.GetAsync($"/api/v1/requests/{request}")).StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var view = await scope2.ServiceProvider.GetRequiredService<LotvDbContext>().RequestActivities.AsNoTracking()
            .SingleAsync(a => a.RequestId == request && a.ActivityType == ActivityType.Viewed);
        Assert.Equal("Sam Staffer (signed in by Ada Admin)", view.ActorName);
    }

    [Fact]
    public async Task WhileSignedInAs_AccountAndProfileChangesAreRefused_ButReadingAndEndingAreFine()
    {
        var admin = await AccountAsync("HQAdmin", "Ada", "Admin");
        var staff = await AccountAsync("ChapterStaff", "Sam", "Staffer");
        var asStaff = WithToken(await TokenAsync(await admin.Client.PostAsJsonAsync("/api/v1/auth/impersonate", new { userId = staff.Id })));

        var avatar = await asStaff.PutAsJsonAsync("/api/v1/users/me/avatar", new { avatarUrl = "https://example.com/x.png" });
        Assert.Equal(HttpStatusCode.Forbidden, avatar.StatusCode);
        Assert.Contains("not allowed while you are signed in as someone else", await avatar.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await asStaff.PutAsJsonAsync($"/api/v1/users/{staff.Id}/email", new { email = "takeover@test.com" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await asStaff.GetAsync("/api/v1/users/me")).StatusCode);                       // looking is fine
        Assert.Equal(HttpStatusCode.OK, (await asStaff.PostAsync("/api/v1/auth/impersonate/end", null)).StatusCode);    // and so is leaving
    }
}
