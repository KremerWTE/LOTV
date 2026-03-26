using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IWorkloadService
{
    /// <summary>Chapter-scoped: workload summary for all staff and volunteers in a chapter.</summary>
    Task<List<WorkloadRow>> GetWorkloadAsync(int chapterId);

    /// <summary>Detail for a specific assignee: their requests broken down by status.</summary>
    Task<WorkloadDetail> GetWorkloadDetailAsync(int chapterId, int userId);

    /// <summary>HQ roll-up: aggregate workload across all chapters.</summary>
    Task<List<WorkloadRow>> GetHqWorkloadAsync();
}

public record WorkloadRow(
    int UserId,
    string Name,
    string Role,
    int OpenCases,
    int OverdueCases,
    int FulfilledLast30Days,
    int CapacityMax
);

public record WorkloadDetail(
    int UserId,
    string Name,
    List<PackageRequest> OpenRequests,
    List<PackageRequest> OverdueRequests,
    List<PackageRequest> RecentlyFulfilled
);
