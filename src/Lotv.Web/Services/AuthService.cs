using Lotv.Core.Models;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;

namespace Lotv.Web.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly JwtAuthStateProvider _authState;
    private readonly IJSRuntime _js;

    private string? _refreshToken;

    public AuthService(HttpClient http, JwtAuthStateProvider authState, IJSRuntime js)
    {
        _http = http;
        _authState = authState;
        _js = js;
    }

    // ── Public state ──────────────────────────────────────────────────────────
    public bool IsAuthenticated => _authState.GetAccessToken() is not null;
    public string UserName
    {
        get
        {
            var first = GetClaim(ClaimTypes.GivenName);
            var last  = GetClaim(ClaimTypes.Surname);
            var full  = $"{first} {last}".Trim();
            return full.Length > 0 ? full : GetClaim(JwtRegisteredClaimNames.Email) ?? "";
        }
    }
    public string UserEmail => GetClaim(JwtRegisteredClaimNames.Email) ?? "";
    public string UserRole  => GetClaim("role") ?? "";
    public int? ChapterId
    {
        get
        {
            var c = GetClaim("chapterId");
            return int.TryParse(c, out var id) ? id : null;
        }
    }
    public bool IsHqAdmin => UserRole == nameof(Lotv.Core.Models.UserRole.HQAdmin);
    public string? UserAvatarUrl { get; private set; }

    public async Task RefreshAvatarAsync()
    {
        try
        {
            var me = await _http.GetFromJsonAsync<MeDto>("/api/v1/users/me");
            UserAvatarUrl = me?.AvatarUrl;
            OnChange?.Invoke();
        }
        catch { }
    }
    private record MeDto(string? AvatarUrl);
    public string UserInitials
    {
        get
        {
            var name = UserName;
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Take(2).Select(p => p[0]));
        }
    }

    public event Action? OnChange;

    // ── Login ─────────────────────────────────────────────────────────────────
    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });

            if (!resp.IsSuccessStatusCode) return false;

            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            _authState.SetToken(result.AccessToken);
            _refreshToken = result.RefreshToken;

            // Persist refresh token in sessionStorage (cleared when tab closes)
            await _js.InvokeVoidAsync("sessionStorage.setItem", "lotv_rt", result.RefreshToken);

            OnChange?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Restore session on page load ──────────────────────────────────────────
    public async Task TryRestoreSessionAsync()
    {
        try
        {
            _refreshToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", "lotv_rt");
            if (string.IsNullOrEmpty(_refreshToken)) return;

            await RefreshTokenAsync();
        }
        catch { /* session restore failure is non-fatal */ }
    }

    // ── Token refresh ─────────────────────────────────────────────────────────
    // The server rotates refresh tokens (single-use — each /auth/refresh call
    // revokes the old token and issues a new one). Several API calls can hit a
    // 401 around the same moment (e.g. a page that fires many GetAsync calls on
    // load) and would otherwise each independently race to consume the same
    // refresh token: only the first wins, and every other caller gets a 401
    // back from an already-revoked token and logs the user out. Sharing one
    // in-flight refresh Task across concurrent callers avoids that race —
    // Blazor WASM runs single-threaded, so setting _refreshInFlight is safe
    // without a lock as long as it happens before the first await.
    private Task<bool>? _refreshInFlight;

    public Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return Task.FromResult(false);
        return _refreshInFlight ??= RefreshTokenCoreAsync();
    }

    private async Task<bool> RefreshTokenCoreAsync()
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = _refreshToken });

            if (!resp.IsSuccessStatusCode)
            {
                await LogoutAsync();
                return false;
            }

            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            _authState.SetToken(result.AccessToken);
            _refreshToken = result.RefreshToken;
            await _js.InvokeVoidAsync("sessionStorage.setItem", "lotv_rt", result.RefreshToken);
            OnChange?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _refreshInFlight = null;
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try { await _http.PostAsJsonAsync("/api/v1/auth/logout", new { RefreshToken = _refreshToken }); }
            catch { }
        }

        _authState.SetToken(null);
        _refreshToken = null;
        await _js.InvokeVoidAsync("sessionStorage.removeItem", "lotv_rt");
        OnChange?.Invoke();
    }

    // ── Backwards-compat sync Login (used by Login.razor during transition) ───
    /// <summary>Legacy sync wrapper — prefer LoginAsync.</summary>
    public bool Login(string email, string password)
    {
        // Falls through to async; used only during demo/mock mode
        // Real pages should call LoginAsync directly.
        return false;
    }

    public void Logout() => _ = LogoutAsync();

    // ── Helper ────────────────────────────────────────────────────────────────
    private string? GetClaim(string type)
    {
        var token = _authState.GetAccessToken();
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
        }
        catch { return null; }
    }

    private record LoginResponse(string AccessToken, string RefreshToken, string Role, int? ChapterId);
}
