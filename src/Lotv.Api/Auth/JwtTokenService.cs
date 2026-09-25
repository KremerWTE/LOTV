using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Lotv.Api.Auth;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string CreateAccessToken(LotvIdentityUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", user.Role.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
        };

        if (user.ChapterId.HasValue)
            claims.Add(new Claim("chapterId", user.ChapterId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A short token that lets <paramref name="admin"/> use the portal as <paramref name="target"/> ("Login As"). It carries the
    /// admin's identity so every action can be attributed to both, and there is deliberately no refresh token for it.
    /// </summary>
    public string CreateImpersonationToken(LotvIdentityUser target, LotvIdentityUser admin, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, target.Id),
            new(JwtRegisteredClaimNames.Email, target.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", target.Role.ToString()),
            new(ClaimTypes.NameIdentifier, target.Id),
            new(ClaimTypes.GivenName, target.FirstName),
            new(ClaimTypes.Surname, target.LastName),
            new("impersonated_by", admin.Id),
            new("impersonated_by_name", admin.FullName),
        };
        if (target.ChapterId.HasValue) claims.Add(new Claim("chapterId", target.ChapterId.Value.ToString()));
        var token = new JwtSecurityToken(_config["Jwt:Issuer"], _config["Jwt:Audience"], claims, expires: DateTime.UtcNow.Add(lifetime), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken CreateRefreshToken(string userId)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow
        };
    }
}
