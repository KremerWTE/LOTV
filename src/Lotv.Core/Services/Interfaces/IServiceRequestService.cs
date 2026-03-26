using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IServiceRequestService
{
    // Queries
    Task<PagedResult<PackageRequest>> GetRequestsAsync(int chapterId, RequestFilter filter, int page = 1, int pageSize = 25);
    Task<PackageRequest?> GetRequestAsync(int id);
    Task<List<PackageRequest>> GetQueueAsync(int chapterId);           // unassigned
    Task<List<PackageRequest>> GetOverdueAsync(int chapterId);
    Task<List<PackageRequest>> GetMyRequestsAsync(int familyId);

    // Mutations
    Task<Result<PackageRequest>> SubmitAsync(PackageRequest request);
    Task<Result> UpdateStatusAsync(int id, CaseStatus newStatus, string actorId);
    Task<Result> AssignAsync(int id, int volunteerId, string assignedById);
    Task<Result> SetPriorityAsync(int id, RequestPriority priority, string actorId);
    Task<Result> SetDueDateAsync(int id, DateTime dueDate, string actorId);
    Task<Result> AcceptAsync(int id, int volunteerId);
    Task<Result> DeclineAsync(int id, int volunteerId, string reason);
    Task<Result> EscalateAsync(int id, string reason, string actorId);
    Task<Result> FulfillAsync(int id, string actorId, string? notes);
    Task<Result> CancelAsync(int id, string actorId);

    // Notes
    Task<List<RequestNote>> GetNotesAsync(int requestId);
    Task<Result<RequestNote>> AddNoteAsync(int requestId, string authorId, string authorName, string content, bool isInternal);

    // Activity log
    Task<List<RequestActivity>> GetActivityAsync(int requestId);
}

public record RequestFilter(
    CaseStatus? Status = null,
    RequestPriority? Priority = null,
    int? AssignedToId = null,
    bool? IsOverdue = null,
    string? Search = null
);
