using Lotv.Api.Data;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public class ScheduledReportService : IScheduledReportService
{
    private readonly LotvDbContext _db;
    private readonly INotificationService _notify;
    private readonly ILogger<ScheduledReportService> _logger;

    public ScheduledReportService(LotvDbContext db, INotificationService notify, ILogger<ScheduledReportService> logger)
    {
        _db = db;
        _notify = notify;
        _logger = logger;
    }

    public async Task SendDailyDigestsAsync()
    {
        var chapters = await _db.Chapters.Where(c => c.IsActive).ToListAsync();
        var yesterday = DateTime.UtcNow.AddDays(-1).Date;
        var today = DateTime.UtcNow.Date;

        foreach (var chapter in chapters)
        {
            var newRequests = await _db.Requests
                .CountAsync(r => r.ChapterId == chapter.Id && r.CreatedAt >= yesterday && r.CreatedAt < today);

            var fulfilled = await _db.Requests
                .CountAsync(r => r.ChapterId == chapter.Id && r.Status == Core.Models.CaseStatus.Fulfilled
                              && r.UpdatedAt >= yesterday && r.UpdatedAt < today);

            var overdue = await _db.Requests
                .CountAsync(r => r.ChapterId == chapter.Id
                              && r.Status != Core.Models.CaseStatus.Fulfilled
                              && r.Status != Core.Models.CaseStatus.Cancelled
                              && r.CreatedAt < DateTime.UtcNow.AddDays(-7));

            var donationAmount = await _db.Donations
                .Where(d => d.ChapterId == chapter.Id && d.Date >= yesterday && d.Date < today)
                .SumAsync(d => (decimal?)d.Amount) ?? 0m;

            _logger.LogInformation(
                "Daily digest — Chapter {Name}: {New} new, {Fulfilled} fulfilled, {Overdue} overdue, {Amount:C} donations",
                chapter.Name, newRequests, fulfilled, overdue, donationAmount);

            // TODO: email chapter lead via _notify.SendEmailTemplateAsync(...)
        }
    }

    public async Task SendWeeklySummariesAsync()
    {
        var chapters = await _db.Chapters.Where(c => c.IsActive).ToListAsync();
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var twoWeeksAgo = DateTime.UtcNow.AddDays(-14);

        foreach (var chapter in chapters)
        {
            var thisWeek = await _db.Requests.CountAsync(r => r.ChapterId == chapter.Id && r.CreatedAt >= weekAgo);
            var priorWeek = await _db.Requests.CountAsync(r => r.ChapterId == chapter.Id && r.CreatedAt >= twoWeeksAgo && r.CreatedAt < weekAgo);

            _logger.LogInformation(
                "Weekly summary — Chapter {Name}: {This} requests this week vs {Prior} prior week",
                chapter.Name, thisWeek, priorWeek);

            // TODO: email chapter lead + HQ via _notify.SendEmailTemplateAsync(...)
        }
    }
}
