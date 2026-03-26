namespace Lotv.Core.Services.Interfaces;

public interface IScheduledReportService
{
    /// <summary>
    /// Generates and emails the daily digest for each chapter.
    /// Triggered by a CRON job at 6:00 AM daily.
    /// </summary>
    Task SendDailyDigestsAsync();

    /// <summary>
    /// Generates and emails the weekly summary to HQ and chapter leads.
    /// Triggered by a CRON job on Monday at 7:00 AM.
    /// </summary>
    Task SendWeeklySummariesAsync();
}
