using System.Net;
using System.Net.Http.Json;

namespace Lotv.Tests.Integration;

/// <summary>
/// An HQ/Chapter admin can hand someone a temporary password (POST /api/v1/users/{id}/set-temp-password)
/// without ever seeing or choosing their real one — it's a one-time, randomly generated password returned
/// in that single response. The target is forced through POST /api/v1/auth/change-password (surfaced to
/// the login response as MustChangePassword) before anything else, so the temp password never becomes a
/// standing one.
/// </summary>
[Collection("Integration")]
public class TempPasswordTests
{
    private readonly LotvApiFactory _factory;
    public TempPasswordTests(LotvApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string AccessToken)> RegisterAndLoginAsync(string role, string password = "TestPass1TempPw!")
    {
        var email = $"temppw-{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = password, FirstName = "Te", LastName = "Mp", Role = role, ChapterId = (int?)null });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        return (client, body.AccessToken);
    }

    [Fact]
    public async Task AdminCanSetATempPassword_AndTheTargetMustChangeItOnNextLogin()
    {
        var (admin, adminToken) = await RegisterAndLoginAsync("HQAdmin");
        admin.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        var targetEmail = $"temppw-{Guid.NewGuid():N}@test.com";
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { Email = targetEmail, Password = "OriginalPass1!", FirstName = "Ta", LastName = "Rget", Role = "ChapterStaff", ChapterId = (int?)null });
        var targetId = (await register.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetString();

        var setResp = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/set-temp-password", new { });
        Assert.Equal(HttpStatusCode.OK, setResp.StatusCode);
        var tempPassword = (await setResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("tempPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(tempPassword));
        Assert.True(tempPassword!.Length >= 12);

        // The old password no longer works.
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = targetEmail, Password = "OriginalPass1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // The temp password works, and the login response says a change is required.
        var newLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = targetEmail, Password = tempPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var loginBody = await newLogin.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.True(loginBody!.MustChangePassword);
    }

    [Fact]
    public async Task NonAdmin_CannotSetATempPasswordForAnyone()
    {
        var (staff, staffToken) = await RegisterAndLoginAsync("ChapterStaff");
        staff.DefaultRequestHeaders.Authorization = new("Bearer", staffToken);
        var (_, otherToken) = await RegisterAndLoginAsync("ChapterStaff");
        // Find the other user's id via /users/me using their own token, independent of the "staff" client.
        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);
        var me = await otherClient.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/users/me");
        var otherId = me.GetProperty("id").GetString();

        var resp = await staff.PostAsJsonAsync($"/api/v1/users/{otherId}/set-temp-password", new { });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ClearsTheFlag_AndRejectsAWrongCurrentPassword()
    {
        var (admin, adminToken) = await RegisterAndLoginAsync("HQAdmin");
        admin.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        var targetEmail = $"temppw-{Guid.NewGuid():N}@test.com";
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { Email = targetEmail, Password = "OriginalPass1!", FirstName = "Ta", LastName = "Rget2", Role = "ChapterStaff", ChapterId = (int?)null });
        var targetId = (await register.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetString();
        var setResp = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/set-temp-password", new { });
        var tempPassword = (await setResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("tempPassword").GetString();

        var targetClient = _factory.CreateClient();
        var login = await targetClient.PostAsJsonAsync("/api/v1/auth/login", new { Username = targetEmail, Password = tempPassword });
        var loginBody = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        targetClient.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.AccessToken);

        // Wrong current password is rejected.
        var wrong = await targetClient.PostAsJsonAsync("/api/v1/auth/change-password", new { CurrentPassword = "NotIt1!!!!!", NewPassword = "BrandNewPass1!" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // Correct current password succeeds.
        var ok = await targetClient.PostAsJsonAsync("/api/v1/auth/change-password", new { CurrentPassword = tempPassword, NewPassword = "BrandNewPass1!" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // The flag is cleared: logging in again with the new password no longer asks for a change.
        var relogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = targetEmail, Password = "BrandNewPass1!" });
        var reloginBody = await relogin.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.False(reloginBody!.MustChangePassword);
    }
}
