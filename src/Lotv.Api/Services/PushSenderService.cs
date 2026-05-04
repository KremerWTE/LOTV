using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Lotv.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public interface IPushSender
{
    Task SendToUserAsync(string userId, string title, string body, string? url = null, CancellationToken ct = default);
    Task SendToAllAsync(string title, string body, string? url = null, CancellationToken ct = default);
}

public class PushSenderService(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<PushSenderService> log) : IPushSender
{
    private readonly string? _publicKey  = cfg["Push:VapidPublicKey"];
    private readonly string? _privateKey = cfg["Push:VapidPrivateKey"];
    private readonly string  _subject    = cfg["Push:VapidSubject"] ?? "mailto:admin@lotv.local";

    public Task SendToUserAsync(string userId, string title, string body, string? url, CancellationToken ct) =>
        DispatchAsync(p => p.UserId == userId, title, body, url, ct);

    public Task SendToAllAsync(string title, string body, string? url, CancellationToken ct) =>
        DispatchAsync(_ => true, title, body, url, ct);

    private async Task DispatchAsync(System.Linq.Expressions.Expression<Func<Lotv.Core.Models.PushSubscription, bool>> filter,
        string title, string body, string? url, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_privateKey))
        {
            log.LogDebug("Push send skipped — VAPID keys not configured.");
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LotvDbContext>();
        var subs = await db.PushSubscriptions.Where(filter).ToListAsync(ct);
        if (subs.Count == 0) return;

        var client = new PushServiceClient
        {
            DefaultAuthentication = new VapidAuthentication(_publicKey, _privateKey) { Subject = _subject }
        };

        var payload = JsonSerializer.Serialize(new { title, body, url = url ?? "/" });
        var stale = new List<int>();

        foreach (var s in subs)
        {
            var sub = new Lib.Net.Http.WebPush.PushSubscription
            {
                Endpoint = s.Endpoint
            };
            sub.SetKey(PushEncryptionKeyName.P256DH, s.P256dh);
            sub.SetKey(PushEncryptionKeyName.Auth, s.Auth);

            try
            {
                await client.RequestPushMessageDeliveryAsync(sub, new PushMessage(payload), ct);
            }
            catch (PushServiceClientException ex) when ((int)ex.StatusCode is 404 or 410)
            {
                stale.Add(s.Id);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Push delivery failed for endpoint {Endpoint}", s.Endpoint);
            }
        }

        if (stale.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(db.PushSubscriptions.Where(p => stale.Contains(p.Id)));
            await db.SaveChangesAsync(ct);
        }
    }
}
