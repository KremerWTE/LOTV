using System.Security.Claims;
using Lotv.Api.Auth;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Services;

public record ImpersonateRequest(string? UserId);

/// <summary>
/// "Login As": an HQ administrator can open the portal as another person to see exactly what they see. It is deliberately narrow:
/// HQ admins only; never another administrator or yourself; never nested; the token lasts 30 minutes and has no refresh token, so it
/// ends on its own; every start and end is written to the audit log; and everything done during it is recorded under both names
/// ("Priya Nair (signed in by Eric Admin)"). While it is active, account, email and profile changes are refused.
/// </summary>
public static class ImpersonationEndpoints
{
    public const string ClaimBy = "impersonated_by";
    public const string ClaimByName = "impersonated_by_name";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    public static void MapImpersonationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/auth/impersonate", async (ImpersonateRequest body, HttpContext http, UserManager<LotvIdentityUser> userMgr,
            JwtTokenService tokens, LotvDbContext db) =>
        {
            if (http.User.FindFirstValue(ClaimBy) is not null)
                return Results.BadRequest(new { error = "You are already signed in as someone else. Return to your own account first." });
            var adminId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var admin = adminId is null ? null : await userMgr.FindByIdAsync(adminId);
            if (admin is null || admin.Role != UserRole.HQAdmin) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(body.UserId)) return Results.BadRequest(new { error = "Choose who to sign in as." });
            var target = await userMgr.FindByIdAsync(body.UserId);
            if (target is null) return Results.NotFound(new { error = "That user does not exist." });
            if (target.Id == admin.Id) return Results.BadRequest(new { error = "That is you." });
            if (target.Role == UserRole.HQAdmin) return Results.BadRequest(new { error = "You cannot sign in as another administrator." });
            if (!target.IsActive) return Results.BadRequest(new { error = "That account is turned off." });

            db.AuditEntries.Add(new AuditEntry
            {
                UserName = admin.FullName, Action = "LoginAsStarted", Entity = "User", EntityId = target.Id,
                Details = $"{admin.FullName} signed in as {target.FullName} ({target.Role}) for up to {(int)Lifetime.TotalMinutes} minutes.",
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            });
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                accessToken = tokens.CreateImpersonationToken(target, admin, Lifetime),
                expiresAt = DateTime.UtcNow.Add(Lifetime),
                user = new { target.Id, Name = target.FullName, target.Role },
                impersonatedBy = admin.FullName,
            });
        }).WithTags("Auth").RequireAuthorization("HQAdmin");

        // Recorded when the admin returns to their own account (an expiry or a reload just ends it without this).
        app.MapPost("/api/v1/auth/impersonate/end", async (HttpContext http, LotvDbContext db) =>
        {
            var by = http.User.FindFirstValue(ClaimByName);
            if (by is null) return Results.Ok(new { ended = false });
            var asName = $"{http.User.FindFirstValue(ClaimTypes.GivenName)} {http.User.FindFirstValue(ClaimTypes.Surname)}".Trim();
            db.AuditEntries.Add(new AuditEntry
            {
                UserName = by, Action = "LoginAsEnded", Entity = "User", EntityId = http.User.FindFirstValue(ClaimTypes.NameIdentifier),
                Details = $"{by} returned to their own account (was signed in as {asName}).",
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { ended = true });
        }).WithTags("Auth").RequireAuthorization();
    }

    /// <summary>While signed in as someone else, nothing that changes the account itself may be done.</summary>
    public static bool IsBlockedDuringLoginAs(HttpContext http)
    {
        if (http.User.FindFirstValue(ClaimBy) is null) return false;
        var method = http.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)) return false;
        var path = http.Request.Path.Value ?? "";
        if (path.StartsWith("/api/v1/auth/impersonate/end", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.StartsWith("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)) return false;
        return path.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/v1/users/", StringComparison.OrdinalIgnoreCase);
    }
}
