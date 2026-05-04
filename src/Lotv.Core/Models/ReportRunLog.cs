namespace Lotv.Core.Models;

public class ReportRunLog
{
    public int Id { get; set; }
    public ReportRunType ReportType { get; set; }
    public int? ChapterId { get; set; }          // null = HQ cross-chapter report
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? RecipientEmails { get; set; } // comma-separated
    public ReportRunStatus Status { get; set; } = ReportRunStatus.Success;
    public string? ErrorMessage { get; set; }
    public int? RecordsIncluded { get; set; }    // e.g. number of cases/donations in report
}

public enum ReportRunType
{
    DailyDigest,
    WeeklySummary,
    HQWeeklySummary
}

public enum ReportRunStatus
{
    Success,
    Failed,
    PartialFailure  // generated but email delivery failed
}
