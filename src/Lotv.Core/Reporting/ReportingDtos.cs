namespace Lotv.Core.Reporting;

// ─── Auto-Assignment ─────────────────────────────────────────────────────────

public record VolunteerScoreResult(
    int VolunteerId,
    string Name,
    double ProximityScore,
    double WorkloadScore,
    double LoyaltyScore,
    double CompositeScore,
    bool Recommended,
    double DistanceMiles
);

// ─── HQ Dashboard ────────────────────────────────────────────────────────────

public record ChapterSummaryRow(
    int ChapterId,
    string ChapterName,
    int OpenRequests,
    int OverdueRequests,
    int FulfilledThisPeriod,
    decimal TotalDonations,
    int ActiveVolunteers
);

// ─── Scheduled Reports ───────────────────────────────────────────────────────

public record DailyDigestReport(
    DateTime ReportDate,
    int ChapterId,
    string ChapterName,
    int NewRequests,
    int DonationsReceived,
    decimal DonationAmount,
    int RequestsFulfilled,
    int OverdueCount,
    int StuckCount         // open > 7 days with no activity
);

public record WeeklySummaryReport(
    DateTime WeekStarting,
    int ChapterId,
    string ChapterName,
    int NewRequestsThisWeek,
    int NewRequestsPriorWeek,
    int FulfilledThisWeek,
    int FulfilledPriorWeek,
    decimal DonationsThisWeek,
    decimal DonationsPriorWeek,
    int ActiveVolunteers,
    int OverdueRequests
);

// ─── Donation Breakdowns ─────────────────────────────────────────────────────

public record DonationByPersonRow(
    int DonorId,
    string Name,
    string? Diocese,
    string? City,
    string? State,
    decimal TotalAmount,
    int GiftCount,
    decimal AverageGift,
    DateTime FirstGiftDate,
    DateTime LastGiftDate
);

public record DonationByDioceseRow(
    int DioceseId,
    string DioceseName,
    string? City,
    string? State,
    int TotalDonors,
    decimal TotalAmount,
    decimal AverageGift
);

public record DonationByCityRow(
    string City,
    string State,
    int TotalDonors,
    decimal TotalAmount
);

public record DonationByChannelRow(
    string Channel,
    decimal TotalAmount,
    int GiftCount,
    double Percentage
);

public record DonationByAmountBand(
    string Band,      // e.g. "$1–$99", "$100–$499", "$500–$999", "$1,000+"
    int GiftCount,
    decimal TotalAmount,
    double Percentage
);

// ─── Impact ──────────────────────────────────────────────────────────────────

public record ImpactSummary(
    int? ChapterId,          // null = HQ roll-up across all chapters
    decimal TotalMoneyRaised,
    decimal TotalMoneyAllocated,
    int PeopleHelped,
    int RequestsFulfilled,
    int ActiveVolunteers,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

public record DonorImpactStatement(
    int DonorId,
    string DonorName,
    decimal TotalGiven,
    int PeopleHelped,
    string? ChapterName,
    string? City
);
