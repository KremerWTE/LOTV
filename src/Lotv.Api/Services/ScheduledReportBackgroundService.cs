using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// Background service that fires scheduled reports on a CRON-style timer.
/// Daily digest at 6:00 AM UTC, weekly summary on Monday at 7:00 AM UTC.
/// </summary>
public class ScheduledReportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledReportBackgroundService> _logger;

    public ScheduledReportBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ScheduledReportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = GetNextRunTime(now);
            var delay = next - now;

            _logger.LogInformation("Scheduled reports: next run at {Next} (in {Delay})", next, delay);
            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await RunScheduledJobsAsync(next);
        }
    }

    private async Task RunScheduledJobsAsync(DateTime runTime)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IScheduledReportService>();

        try
        {
            _logger.LogInformation("Running daily digest reports at {Time}", runTime);
            await service.SendDailyDigestsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running daily digest");
        }

        // Weekly summary on Mondays
        if (runTime.DayOfWeek == DayOfWeek.Monday)
        {
            try
            {
                _logger.LogInformation("Running weekly summary reports");
                await service.SendWeeklySummariesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running weekly summary");
            }
        }
    }

    /// <summary>Returns the next 6:00 AM UTC after the given time.</summary>
    private static DateTime GetNextRunTime(DateTime now)
    {
        var today6am = now.Date.AddHours(6);
        return now < today6am ? today6am : today6am.AddDays(1);
    }
}
