using Lotv.Core.Models;
using System.Security.Claims;

namespace Lotv.Api.Auth;

public class ChapterContextService : IChapterContextService
{
    private readonly IHttpContextAccessor _http;

    public ChapterContextService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public string UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    public string UserName
    {
        get
        {
            var given = User?.FindFirstValue(ClaimTypes.GivenName);
            var surname = User?.FindFirstValue(ClaimTypes.Surname);
            var full = $"{given} {surname}".Trim();
            var name = string.IsNullOrWhiteSpace(full) ? UserId : full;
            // Signed in as someone else ("Login As"): every activity entry names both people.
            var by = User?.FindFirstValue("impersonated_by_name");
            return string.IsNullOrEmpty(by) ? name : $"{name} (signed in by {by})";
        }
    }

    public int? ChapterId
    {
        get
        {
            var claim = User?.FindFirstValue("chapterId");
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsHqAdmin =>
        User?.FindFirstValue("role") == nameof(UserRole.HQAdmin);
}
