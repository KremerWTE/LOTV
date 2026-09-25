using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lotv.Api.Data;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lotv.Tests.Integration;

/// <summary>
/// Creating an account (and choosing its role) is for HQ admins only; the public can't make themselves an admin.
/// These run against a host configured like production, with open registration off.
/// </summary>
[Collection("Integration")]
public class RegistrationSecurityTests
{
    private const string Password = "TestPass1Security!";
    private readonly LotvApiFactory _factory;
    private readonly List<(string To, string Subject, string Html)> _sent = [];

    public RegistrationSecurityTests(LotvApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> ProductionLikeHost()
    {
        var notify = new Mock<INotificationService>();
        notify.Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
              .Callback<string, string, string, string>((to, _, subject, html) => { lock (_sent) _sent.Add((to, subject, html)); })
              .ReturnsAsync(Result.Ok());
        return _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowOpenRegistration"] = "false",
                ["App:WebBaseUrl"] = "https://portal.example.org",
            }));
            b.ConfigureTestServices(s => s.AddSingleton(notify.Object));
        });
    }

    private static async Task<string> CreateUserAsync(WebApplicationFactory<Program> host, UserRole role)
    {
        var email = $"sec-{Guid.NewGuid():N}@test.com";
        using var scope = host.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
        var result = await userMgr.CreateAsync(new LotvIdentityUser
        {
            UserName = email, Email = email, FirstName = "Sec", LastName = "Tester", Role = role, ChapterId = role == UserRole.HQAdmin ? null : 1, IsActive = true,
        }, Password);
        Assert.True(result.Succeeded);
        return email;
    }

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task ThePublic_CannotRegisterAnAccount_LeastOfAllAnAdmin()
    {
        using var host = ProductionLikeHost();
        var email = $"intruder-{Guid.NewGuid():N}@test.com";

        var resp = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { Email = email, Password, FirstName = "In", LastName = "Truder", Role = "HQAdmin", ChapterId = (int?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        using var scope = host.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task ANonAdmin_CannotRegisterAccountsEither()
    {
        using var host = ProductionLikeHost();
        var staff = await SignedInAsync(host, await CreateUserAsync(host, UserRole.ChapterStaff));

        var resp = await staff.PostAsJsonAsync("/api/v1/auth/register",
            new { Email = $"x-{Guid.NewGuid():N}@test.com", Password, FirstName = "X", LastName = "Y", Role = "HQAdmin", ChapterId = (int?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AnHqAdmin_CanCreateAnAccount_WithTheRoleTheyChoose()
    {
        using var host = ProductionLikeHost();
        var admin = await SignedInAsync(host, await CreateUserAsync(host, UserRole.HQAdmin));
        var email = $"newstaff-{Guid.NewGuid():N}@test.com";

        var resp = await admin.PostAsJsonAsync("/api/v1/auth/register",
            new { Email = email, Password, FirstName = "New", LastName = "Staff", Role = "ChapterStaff", ChapterId = 1 });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var scope = host.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>().FindByEmailAsync(email);
        Assert.Equal(UserRole.ChapterStaff, created!.Role);
    }

    [Fact]
    public async Task ThePasswordResetEmail_HasAnAbsoluteLink_AndWorksWithTheEmailAddressAsTheUsername()
    {
        using var host = ProductionLikeHost();
        var email = await CreateUserAsync(host, UserRole.ChapterStaff);

        var resp = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { Username = email });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var mail = Assert.Single(_sent, m => m.To == email);
        Assert.Contains("https://portal.example.org/reset-password?u=", mail.Html);
        Assert.DoesNotContain("href=\"/reset-password", mail.Html);
    }

    [Fact]
    public async Task AnAccountCanSignInWithItsEmailAddress()
    {
        using var host = ProductionLikeHost();
        var email = await CreateUserAsync(host, UserRole.ChapterStaff);
        using var scope = host.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
        var user = (await userMgr.FindByEmailAsync(email))!;
        user.UserName = "sec.person";   // the username is not the email here
        await userMgr.UpdateAsync(user);

        var byEmail = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password });
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
        var wrong = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = "WrongPass1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    // ── Provisioned accounts (Susan Harper, Nicolas Kremer) ───────────────────

    private static async Task DeleteProvisionedAsync(UserManager<LotvIdentityUser> userMgr)
    {
        foreach (var a in StaffAccountProvisioning.Accounts)
            if (await userMgr.FindByNameAsync(a.UserName) is { } existing) await userMgr.DeleteAsync(existing);
    }

    [Fact]
    public async Task SusanGetsAVolunteerRecord_ThatIsNeverAutoAssignedRealCases_AndIsCreatedOnlyOnce()
    {
        using var host = ProductionLikeHost();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var provisionedEmails = StaffAccountProvisioning.Accounts.Select(a => a.Email.ToLower()).ToList();
        db.Volunteers.RemoveRange(db.Volunteers.Where(v => provisionedEmails.Contains(v.Email.ToLower())));
        if (!await db.Chapters.AnyAsync())
        {
            db.Chapters.Add(new Chapter { Id = 9901, Name = "Vol Chapter", City = "Testville", State = "IL", ContactName = "T", ContactEmail = "t@test.example.com", IsActive = true });
        }
        await db.SaveChangesAsync();

        Assert.Equal(StaffAccountProvisioning.Accounts.Count(a => a.AlsoVolunteer), await StaffAccountProvisioning.EnsureVolunteerRecordsAsync(db, logger));
        var volunteer = await db.Volunteers.AsNoTracking().SingleAsync(v => v.Email == "susan@wte.net");
        Assert.Equal("Susan", volunteer.FirstName);
        Assert.Equal("Harper", volunteer.LastName);
        Assert.Equal(VolunteerStatus.Active, volunteer.Status);
        // Not a role automatic assignment picks from, so real requests are only ever handed to her by a person or a rule
        Assert.DoesNotContain(volunteer.Role, new[] { VolunteerRole.PackageAssembler, VolunteerRole.Admin });

        // Nicolas gets a record too, on the same terms
        var nicolas = await db.Volunteers.AsNoTracking().SingleAsync(v => v.Email == "kremer@wte.net");
        Assert.Equal("Nicolas", nicolas.FirstName);
        Assert.Equal("Kremer", nicolas.LastName);
        Assert.Equal(VolunteerStatus.Active, nicolas.Status);
        Assert.DoesNotContain(nicolas.Role, new[] { VolunteerRole.PackageAssembler, VolunteerRole.Admin });

        Assert.Equal(0, await StaffAccountProvisioning.EnsureVolunteerRecordsAsync(db, logger));   // already there
        Assert.Equal(1, await db.Volunteers.CountAsync(v => v.Email == "susan@wte.net"));
        Assert.Equal(1, await db.Volunteers.CountAsync(v => v.Email == "kremer@wte.net"));
    }


    [Fact]
    public async Task AStartingPasswordFromConfiguration_IsUsedOnCreation_AndOnlyUntilTheFirstSignIn()
    {
        const string starting = "Start-Pass-2026!x";
        const string chosen = "Chosen-By-Susan-9!";
        using var host = ProductionLikeHost();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [StaffAccountProvisioning.InitialPasswordKey("susan.harper")] = starting,
        }).Build();

        // Each step gets its own scope, as each application start does.
        async Task<T> InScope<T>(Func<UserManager<LotvIdentityUser>, Task<T>> body)
        {
            using var scope = host.Services.CreateScope();
            return await body(scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>());
        }

        await InScope(async m => { await DeleteProvisionedAsync(m); return 0; });

        // Created with the supplied password, and she can sign in with it (by username or email)
        Assert.Equal(StaffAccountProvisioning.Accounts.Length, await InScope(m => StaffAccountProvisioning.EnsureAsync(m, logger, config)));
        foreach (var who in new[] { "susan.harper", "susan@wte.net" })
        {
            var ok = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = who, Password = starting });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        // She has signed in, and picks her own password.
        await InScope(async m =>
        {
            var susan = (await m.FindByNameAsync("susan.harper"))!;
            Assert.NotNull(susan.LastLoginAt);
            Assert.True((await m.ChangePasswordAsync(susan, starting, chosen)).Succeeded);
            return 0;
        });

        // Deploying again must not put the starting password back.
        Assert.Equal(0, await InScope(m => StaffAccountProvisioning.EnsureAsync(m, logger, config)));
        await InScope(async m =>
        {
            var susan = (await m.FindByNameAsync("susan.harper"))!;
            Assert.True(await m.CheckPasswordAsync(susan, chosen));
            Assert.False(await m.CheckPasswordAsync(susan, starting));
            return 0;
        });
    }

    [Fact]
    public async Task AStartingPasswordSuppliedLater_IsGivenToAnAccountThatNeverSignedIn()
    {
        const string starting = "Later-Pass-2026!y";
        using var host = ProductionLikeHost();
        using var scope = host.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var existing = await userMgr.FindByNameAsync("susan.harper");
        if (existing is not null) await userMgr.DeleteAsync(existing);
        await StaffAccountProvisioning.EnsureAsync(userMgr, logger);   // created first with a random password
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [StaffAccountProvisioning.InitialPasswordKey("susan.harper")] = starting,
        }).Build();

        await StaffAccountProvisioning.EnsureAsync(userMgr, logger, config);   // the secret is added on a later deploy

        var ok = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = "susan@wte.net", Password = starting });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task StaffProvisioning_CreatesSusanAsAnHqAdmin_WithNoPasswordAnyoneKnows_AndLeavesExistingAccountsAlone()
    {
        using var host = ProductionLikeHost();
        using var scope = host.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<LotvIdentityUser>>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        await DeleteProvisionedAsync(userMgr);   // start from a clean slate for this test

        Assert.Equal(StaffAccountProvisioning.Accounts.Length, await StaffAccountProvisioning.EnsureAsync(userMgr, logger));
        var susan = (await userMgr.FindByNameAsync("susan.harper"))!;
        Assert.Equal("susan@wte.net", susan.Email);
        Assert.Equal(UserRole.HQAdmin, susan.Role);
        Assert.Null(susan.ChapterId);
        Assert.False(await userMgr.CheckPasswordAsync(susan, "DevPassword1!"));
        Assert.False(await userMgr.CheckPasswordAsync(susan, Password));

        // Nicolas is a volunteer, not an administrator: the Volunteer role and no chapter tie, with no password anyone knows.
        var nicolas = (await userMgr.FindByNameAsync("nicolas.kremer"))!;
        Assert.Equal("kremer@wte.net", nicolas.Email);
        Assert.Equal(UserRole.Volunteer, nicolas.Role);
        Assert.Null(nicolas.ChapterId);
        Assert.False(await userMgr.CheckPasswordAsync(nicolas, "DevPassword1!"));

        // The only way in is the emailed reset link.
        var forgot = await host.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { Username = "susan@wte.net" });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Contains(_sent, m => m.To == "susan@wte.net");

        Assert.Equal(0, await StaffAccountProvisioning.EnsureAsync(userMgr, logger));   // running it again changes nothing
    }
}
