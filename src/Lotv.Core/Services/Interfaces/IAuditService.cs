using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(string userName, string action, string entity, string? entityId = null, string? details = null, string? ipAddress = null);
    Task<List<AuditEntry>> GetLogAsync(int? chapterId = null, int page = 1, int pageSize = 50);
}
