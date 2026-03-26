using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Reporting;

namespace Lotv.Core.Services.Interfaces;

public interface IDonorService
{
    Task<PagedResult<Donor>> GetDonorsAsync(int chapterId, int page = 1, int pageSize = 25, string? search = null);
    Task<Donor?> GetDonorAsync(int id);
    Task<List<Donation>> GetContributionHistoryAsync(int donorId);
    Task<DonorImpactStatement> GetImpactStatementAsync(int donorId);
    Task<Result<Donor>> RegisterAsync(Donor donor);
    Task<Result> UpdateAsync(Donor donor);
}
