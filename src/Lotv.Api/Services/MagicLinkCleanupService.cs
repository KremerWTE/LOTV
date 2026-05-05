using Lotv.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>Hourly prune of expired or used DonorMagicLink rows; keeps the table small and tidy.</summary>
public class MagicLinkCleanupService(IServiceScopeFactory scopes, ILogger<MagicLinkCleanupService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db  = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
                var cut = DateTime.UtcNow.AddDays(-1);
                var staleD = await db.DonorMagicLinks
                    .Where(l => l.UsedAt != null || l.ExpiresAt < cut)
                    .ToListAsync(stoppingToken);
                var staleV = await db.VolunteerMagicLinks
                    .Where(l => l.UsedAt != null || l.ExpiresAt < cut)
                    .ToListAsync(stoppingToken);
                if (staleD.Count > 0) db.DonorMagicLinks.RemoveRange(staleD);
                if (staleV.Count > 0) db.VolunteerMagicLinks.RemoveRange(staleV);
                if (staleD.Count + staleV.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    log.LogDebug("Pruned {D} donor + {V} volunteer magic links", staleD.Count, staleV.Count);
                }
            }
            catch (Exception ex) { log.LogWarning(ex, "Magic-link cleanup failed"); }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
