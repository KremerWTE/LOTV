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
                var stale = await db.DonorMagicLinks
                    .Where(l => l.UsedAt != null || l.ExpiresAt < cut)
                    .ToListAsync(stoppingToken);
                if (stale.Count > 0)
                {
                    db.DonorMagicLinks.RemoveRange(stale);
                    await db.SaveChangesAsync(stoppingToken);
                    log.LogDebug("Pruned {Count} magic links", stale.Count);
                }
            }
            catch (Exception ex) { log.LogWarning(ex, "Magic-link cleanup failed"); }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
