using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lotv.Web.Services;

/// <summary>
/// Blazor WASM AuthenticationStateProvider backed by the in-memory JWT access token.
/// Tokens are never stored in localStorage — only in memory (per ADR-001).
/// </summary>
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState _anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _accessToken;

    public void SetToken(string? accessToken)
    {
        _accessToken = accessToken;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public string? GetAccessToken() => _accessToken;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return Task.FromResult(_anonymous);

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(_accessToken);

            // Reject expired tokens
            if (jwt.ValidTo < DateTime.UtcNow)
            {
                _accessToken = null;
                return Task.FromResult(_anonymous);
            }

            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }
        catch
        {
            return Task.FromResult(_anonymous);
        }
    }
}
