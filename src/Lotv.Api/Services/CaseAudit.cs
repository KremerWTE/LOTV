using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// The record of who looked at a case, kept in the case's own activity log next to who assigned it and who changed it.
/// One entry per person per case per <see cref="Window"/>, so refreshing a page or keeping it open doesn't fill the log.
/// </summary>
public static class CaseAudit
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static async Task RecordViewAsync(LotvDbContext db, int requestId, string? userId, string? userName)
    {
        var who = string.IsNullOrWhiteSpace(userId) ? "unknown" : userId;
        var since = DateTime.UtcNow - Window;
        if (await db.RequestActivities.AnyAsync(a => a.RequestId == requestId && a.ActivityType == ActivityType.Viewed && a.ActorId == who && a.Timestamp >= since))
            return;

        db.RequestActivities.Add(new RequestActivity
        {
            RequestId = requestId, ActorId = who, ActorName = string.IsNullOrWhiteSpace(userName) ? "Unknown user" : userName,
            ActivityType = ActivityType.Viewed, Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
