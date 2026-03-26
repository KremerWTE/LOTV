using Lotv.Api.Auth;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace Lotv.Tests.Services;

public class JwtTokenServiceTests
{
    private static JwtTokenService BuildService() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = "LOTVTestSecretKeyThatIsLongEnoughForHS256Testing2026!",
                ["Jwt:Issuer"]   = "lotv-test",
                ["Jwt:Audience"] = "lotv-test"
            })
            .Build());

    private static LotvIdentityUser TestUser(UserRole role = UserRole.ChapterStaff, int? chapterId = null) =>
        new()
        {
            Id        = Guid.NewGuid().ToString(),
            Email     = "jwt-test@lotv.com",
            FirstName = "Jwt",
            LastName  = "Test",
            Role      = role,
            ChapterId = chapterId
        };

    // ── CreateAccessToken ────────────────────────────────────────────────────

    [Fact]
    public void CreateAccessToken_ReturnsNonEmptyString()
    {
        var svc   = BuildService();
        var token = svc.CreateAccessToken(TestUser());
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void CreateAccessToken_IsValidJwt()
    {
        var svc     = BuildService();
        var raw     = svc.CreateAccessToken(TestUser());
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(raw));
        var jwt = handler.ReadJwtToken(raw);
        Assert.NotNull(jwt);
    }

    [Fact]
    public void CreateAccessToken_ContainsEmailClaim()
    {
        var user  = TestUser();
        var svc   = BuildService();
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(user));
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
    }

    [Fact]
    public void CreateAccessToken_ContainsRoleClaim()
    {
        var user  = TestUser(UserRole.ChapterAdmin);
        var svc   = BuildService();
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(user));
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "ChapterAdmin");
    }

    [Fact]
    public void CreateAccessToken_ContainsSubClaim()
    {
        var user  = TestUser();
        var svc   = BuildService();
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(user));
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id);
    }

    [Fact]
    public void CreateAccessToken_WithChapterId_ContainsChapterIdClaim()
    {
        var user  = TestUser(UserRole.ChapterStaff, chapterId: 42);
        var svc   = BuildService();
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(user));
        Assert.Contains(jwt.Claims, c => c.Type == "chapterId" && c.Value == "42");
    }

    [Fact]
    public void CreateAccessToken_WithoutChapterId_NoChapterIdClaim()
    {
        var user  = TestUser(UserRole.HQAdmin, chapterId: null);
        var svc   = BuildService();
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(user));
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "chapterId");
    }

    [Fact]
    public void CreateAccessToken_ExpiresInFuture()
    {
        var svc = BuildService();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateAccessToken(TestUser()));
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    // ── CreateRefreshToken ───────────────────────────────────────────────────

    [Fact]
    public void CreateRefreshToken_SetsUserId()
    {
        var user    = TestUser();
        var svc     = BuildService();
        var refresh = svc.CreateRefreshToken(user.Id);
        Assert.Equal(user.Id, refresh.UserId);
    }

    [Fact]
    public void CreateRefreshToken_HasFutureExpiry()
    {
        var svc     = BuildService();
        var refresh = svc.CreateRefreshToken("user-123");
        Assert.True(refresh.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CreateRefreshToken_TokenIsNonEmpty()
    {
        var svc     = BuildService();
        var refresh = svc.CreateRefreshToken("user-123");
        Assert.False(string.IsNullOrWhiteSpace(refresh.Token));
    }

    [Fact]
    public void CreateRefreshToken_TwoCallsProduceDifferentTokens()
    {
        var svc = BuildService();
        var a   = svc.CreateRefreshToken("user-abc");
        var b   = svc.CreateRefreshToken("user-abc");
        Assert.NotEqual(a.Token, b.Token);
    }
}
