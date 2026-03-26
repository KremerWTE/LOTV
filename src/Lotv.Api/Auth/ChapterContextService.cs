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
