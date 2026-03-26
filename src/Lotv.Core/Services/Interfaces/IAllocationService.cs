using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IAllocationService
{
    Task<List<FundAllocation>> GetAllocationsAsync(int chapterId);
    Task<FundAllocation?> GetAllocationAsync(int id);
    Task<Result<FundAllocation>> AllocateAsync(int donationId, string allocatedTo, decimal amount, string allocatedBy, string? notes);
    Task<Result> ApproveAsync(int allocationId, string approvedBy);
    Task<Result> ReverseAsync(int allocationId, string reversedBy, string reason);
}
