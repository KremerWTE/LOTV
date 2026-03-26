using Lotv.Core.Common;
using Lotv.Core.Reporting;

namespace Lotv.Core.Services.Interfaces;

public interface IAutoAssignmentService
{
    /// <summary>
    /// Score all eligible volunteers for the given request and auto-assign the top candidate.
    /// If no candidate meets the threshold, routes to the unassigned queue.
    /// </summary>
    Task<Result> TryAutoAssignAsync(int requestId);

    /// <summary>
    /// Returns ranked scores for all eligible volunteers for a given request.
    /// Used by the API to expose scoring results to staff.
    /// </summary>
    Task<List<VolunteerScoreResult>> GetScoresAsync(int requestId);

    /// <summary>
    /// Called when a volunteer declines. Attempts reassignment (up to max attempts),
    /// then escalates to staff queue.
    /// </summary>
    Task<Result> HandleDeclineAsync(int requestId, int volunteerId, string reason);
}
