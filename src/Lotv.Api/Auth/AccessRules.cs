using Lotv.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace Lotv.Api.Auth;

/// <summary>
/// The access rule, in one place:
///   • Staff (HQ admin, chapter admin, chapter staff, director) work with everything.
///   • Board sees everything the staff areas show, read-only: reads are allowed, any change is refused.
///   • Volunteers see only the prayer request package: their own assigned cases and the steps of packing them
///     (see <see cref="VolunteerCanUse"/>); nothing else.
/// HQ-admin-only areas (system admin, users, API keys, forms) stay closed to Board and volunteers.
/// </summary>
public static class AccessRules
{
    static readonly string[] Staff = [nameof(UserRole.HQAdmin), nameof(UserRole.ChapterAdmin), nameof(UserRole.ChapterStaff), nameof(UserRole.Director)];
    static readonly string[] Admins = [nameof(UserRole.HQAdmin), nameof(UserRole.ChapterAdmin), nameof(UserRole.Director)];

    static string? Role(AuthorizationHandlerContext c) => c.User.FindFirst("role")?.Value;

    /// <summary>Reads only: GET, HEAD and OPTIONS.</summary>
    public static bool IsRead(HttpContext http) =>
        HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) || HttpMethods.IsOptions(http.Request.Method);

    static bool BoardMayRead(AuthorizationHandlerContext c) =>
        Role(c) == nameof(UserRole.Board) && c.Resource is HttpContext http && IsRead(http);

    /// <summary>Staff areas: staff fully, Board read-only.</summary>
    public static void StaffOrBoardRead(AuthorizationPolicyBuilder p) =>
        p.RequireAssertion(c => Staff.Contains(Role(c)) || BoardMayRead(c));

    /// <summary>Admin areas: admins fully, Board read-only.</summary>
    public static void AdminOrBoardRead(AuthorizationPolicyBuilder p) =>
        p.RequireAssertion(c => Admins.Contains(Role(c)) || BoardMayRead(c));

    /// <summary>The case endpoints: staff fully, Board read-only, volunteers narrowly (an endpoint filter narrows them further).</summary>
    public static void CaseWork(AuthorizationPolicyBuilder p) =>
        p.RequireAssertion(c => Staff.Contains(Role(c)) || Role(c) == nameof(UserRole.Volunteer) || BoardMayRead(c));

    // What a volunteer may do with a case that is assigned to them, and nothing more.
    static readonly HashSet<string> VolunteerOwnCase = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET /api/v1/requests/{id:int}",
        "PATCH /api/v1/requests/{id:int}",                       // tracking number and shipped date
        "GET /api/v1/requests/{id:int}/items",
        "PUT /api/v1/requests/{id:int}/items/{itemId:int}/pack",
        "GET /api/v1/requests/{id:int}/notes",
        "POST /api/v1/requests/{id:int}/notes",
        "GET /api/v1/requests/{id:int}/activity",
        "PUT /api/v1/requests/{id:int}/process-stage",
        "PUT /api/v1/requests/{id:int}/status",
        "POST /api/v1/requests/{id:int}/fulfill",
    };

    /// <summary>The list route a volunteer may call without naming a case (it returns only their own).</summary>
    public const string VolunteerList = "GET /api/v1/requests/mine";

    /// <summary>
    /// True when a volunteer may call this route. <paramref name="isOwnCase"/> is asked only for routes that name a case.
    /// </summary>
    public static async Task<bool> VolunteerCanUse(HttpContext http, Func<int, Task<bool>> isOwnCase)
    {
        var pattern = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (pattern is null) return false;
        var key = $"{http.Request.Method} {pattern}";
        if (key.Equals(VolunteerList, StringComparison.OrdinalIgnoreCase)) return true;
        if (!VolunteerOwnCase.Contains(key)) return false;
        return int.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id) && await isOwnCase(id);
    }
}
