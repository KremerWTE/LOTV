using Lotv.Core.Common;
using Lotv.Core.Reporting;
using Lotv.Core.ValueObjects;

namespace Lotv.Core.Services.Interfaces;

public interface IReportingService
{
    Task<ImpactSummary> GetImpactReportAsync(int? chapterId, DateRange period);
    Task<Result<byte[]>> ExportCasesToCsvAsync(int chapterId, DateRange period);
    Task<Result<byte[]>> ExportDonationsToCsvAsync(int chapterId, DateRange period);
    Task<Result<byte[]>> ExportVolunteersToCsvAsync(int chapterId);
}
