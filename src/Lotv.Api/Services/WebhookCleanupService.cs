using Lotv.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>Daily prune of WebhookEvents older than 90 days; complements the manual /admin/webhooks/old endpoint.</summary>
public class WebhookCleanupService(IServiceScopeFactory scopes, ILogger<WebhookCleanupService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-90);
                var stale = db.WebhookEvents.Where(w => w.ReceivedAt < cutoff);
                var count = await stale.CountAsync(stoppingToken);
                if (count > 0)
                {
                    db.WebhookEvents.RemoveRange(stale);
                    await db.SaveChangesAsync(stoppingToken);
                    log.LogInformation("Pruned {Count} webhook events older than 90 days", count);
                }
            }
            catch (Exception ex) { log.LogWarning(ex, "Webhook cleanup failed"); }

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
