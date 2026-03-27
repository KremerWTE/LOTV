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

            if (!string.IsNullOrEmpty(chapter.ContactEmail))
            {
                var html = $"""
                    <!DOCTYPE html><html><body style="font-family:Georgia,serif;max-width:600px;margin:40px auto;color:#333">
                    <h2 style="color:#1a4a6b">LOTV Ministry — Daily Digest</h2>
                    <h3 style="color:#333">{chapter.Name} &mdash; {yesterday:MMMM d, yyyy}</h3>
                    <table style="width:100%;border-collapse:collapse;margin:20px 0;">
                      <tr style="background:#f0f4f8"><td style="padding:10px">New Requests</td><td style="padding:10px;font-weight:700">{newRequests}</td></tr>
                      <tr><td style="padding:10px">Fulfilled Today</td><td style="padding:10px;font-weight:700;color:#198754">{fulfilled}</td></tr>
                      <tr style="background:#f0f4f8"><td style="padding:10px">Overdue Cases</td><td style="padding:10px;font-weight:700;color:#e67e22">{overdue}</td></tr>
                      <tr><td style="padding:10px">Donations Received</td><td style="padding:10px;font-weight:700;color:#2d469d">{donationAmount:C}</td></tr>
                    </table>
                    <p style="font-size:.85em;color:#888">This is an automated digest from LOTV Ministry.</p>
                    </body></html>
                    """;
                await _notify.SendEmailAsync(chapter.ContactEmail, chapter.ContactName,
                    $"LOTV Daily Digest — {chapter.Name} — {yesterday:MMM d}", html);
            }
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

            if (!string.IsNullOrEmpty(chapter.ContactEmail))
            {
                var trend = thisWeek > priorWeek ? "&#8679; Up" : thisWeek < priorWeek ? "&#8681; Down" : "&#8596; Flat";
                var html = $"""
                    <!DOCTYPE html><html><body style="font-family:Georgia,serif;max-width:600px;margin:40px auto;color:#333">
                    <h2 style="color:#1a4a6b">LOTV Ministry — Weekly Summary</h2>
                    <h3 style="color:#333">{chapter.Name}</h3>
                    <table style="width:100%;border-collapse:collapse;margin:20px 0;">
                      <tr style="background:#f0f4f8"><td style="padding:10px">Requests This Week</td><td style="padding:10px;font-weight:700">{thisWeek}</td></tr>
                      <tr><td style="padding:10px">Requests Prior Week</td><td style="padding:10px;font-weight:700">{priorWeek}</td></tr>
                      <tr style="background:#f0f4f8"><td style="padding:10px">Week-over-Week</td><td style="padding:10px;font-weight:700">{trend}</td></tr>
                    </table>
                    <p style="font-size:.85em;color:#888">This is an automated weekly summary from LOTV Ministry.</p>
                    </body></html>
                    """;
                await _notify.SendEmailAsync(chapter.ContactEmail, chapter.ContactName,
                    $"LOTV Weekly Summary — {chapter.Name}", html);
            }
        }
    }
}
