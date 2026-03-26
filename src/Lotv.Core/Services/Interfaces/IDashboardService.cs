using Lotv.Core.Reporting;
using Lotv.Core.ValueObjects;

namespace Lotv.Core.Services.Interfaces;

public interface IDashboardService
{
    // Chapter-scoped (for ChapterAdmin / ChapterStaff)
    Task<ImpactSummary> GetChapterSummaryAsync(int chapterId, DateRange period);
    Task<List<DonationByPersonRow>> GetDonationsByPersonAsync(int chapterId, DateRange period);
    Task<List<DonationByDioceseRow>> GetDonationsByDioceseAsync(int chapterId, DateRange period);
    Task<List<DonationByCityRow>> GetDonationsByCityAsync(int chapterId, DateRange period);
    Task<List<DonationByChannelRow>> GetDonationsByChannelAsync(int chapterId, DateRange period);
    Task<List<DonationByAmountBand>> GetDonationsByAmountBandAsync(int chapterId, DateRange period);

    // HQ roll-up (for HQAdmin — no ChapterId filter)
    Task<ImpactSummary> GetHqSummaryAsync(DateRange period);
    Task<List<ChapterSummaryRow>> GetChapterBreakdownAsync(DateRange period);
}
