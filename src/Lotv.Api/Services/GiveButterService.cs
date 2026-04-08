using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

// ── GiveButter JSON records ───────────────────────────────────────────────────

public record GbWebhookEnvelope(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("data")]  GbTransaction Data
);

public record GbTransaction(
    [property: JsonPropertyName("id")]           string Id,
    [property: JsonPropertyName("campaign_id")]  string? CampaignId,
    [property: JsonPropertyName("first_name")]   string FirstName,
    [property: JsonPropertyName("last_name")]    string LastName,
    [property: JsonPropertyName("email")]        string Email,
    [property: JsonPropertyName("phone")]        string? Phone,
    [property: JsonPropertyName("address")]      GbAddress? Address,
    [property: JsonPropertyName("amount")]       int Amount,   // cents
    [property: JsonPropertyName("status")]       string Status,
    [property: JsonPropertyName("transacted_at")] DateTime TransactedAt
);

public record GbAddress(
    [property: JsonPropertyName("address_1")] string? Address1,
    [property: JsonPropertyName("city")]      string? City,
    [property: JsonPropertyName("state")]     string? State,
    [property: JsonPropertyName("zipcode")]   string? Zipcode
);

public record GbListResponse<T>(
    [property: JsonPropertyName("data")] List<T> Data,
    [property: JsonPropertyName("meta")] GbMeta? Meta
);

public record GbMeta(
    [property: JsonPropertyName("next_cursor")] string? NextCursor
);

public record GbSyncResult(int Synced, int Skipped, List<string> Errors);

// ── Service ───────────────────────────────────────────────────────────────────

public class GiveButterService(HttpClient http, LotvDbContext db, ILogger<GiveButterService> log)
{
    /// <summary>
    /// Verify an incoming GiveButter webhook signature.
    /// GiveButter sends: Givebutter-Signature header = HMAC-SHA256(secret, raw-body) as hex.
    /// </summary>
    public static bool VerifySignature(string rawBody, string signature, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        var expected = Convert.ToHexString(HMACSHA256.HashData(keyBytes, bodyBytes));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    /// <summary>
    /// Pull all transactions for a campaign from GiveButter and sync them as retreat registrations.
    /// Idempotent — skips already-synced transactions (unique GiveButterTransactionId).
    /// </summary>
    public async Task<GbSyncResult> SyncCampaignTransactionsAsync(
        string campaignId, int retreatId, int chapterId, DateOnly? since = null)
    {
        var synced = 0;
        var skipped = 0;
        var errors = new List<string>();
        string? cursor = null;

        do
        {
            var url = $"transactions?campaign_id={Uri.EscapeDataString(campaignId)}&limit=100";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            GbListResponse<GbTransaction>? page;
            try
            {
                page = await http.GetFromJsonAsync<GbListResponse<GbTransaction>>(url);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "GiveButter API error fetching transactions");
                errors.Add($"API error: {ex.Message}");
                break;
            }

            if (page is null) break;

            foreach (var tx in page.Data)
            {
                if (since.HasValue && DateOnly.FromDateTime(tx.TransactedAt) < since.Value)
                    continue;

                try
                {
                    var result = await ProcessTransactionAsync(tx, retreatId, chapterId);
                    if (result) synced++; else skipped++;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error processing GiveButter transaction {Id}", tx.Id);
                    errors.Add($"tx {tx.Id}: {ex.Message}");
                }
            }

            cursor = page.Meta?.NextCursor;
        } while (cursor is not null);

        return new GbSyncResult(synced, skipped, errors);
    }

    /// <summary>
    /// Process a single GiveButter transaction into a RetreatRegistration.
    /// Returns true if created, false if skipped (already exists).
    /// </summary>
    public async Task<bool> ProcessTransactionAsync(GbTransaction tx, int retreatId, int chapterId)
    {
        // Idempotency check
        if (await db.RetreatRegistrations.AnyAsync(r => r.GiveButterTransactionId == tx.Id))
            return false;

        var reg = new RetreatRegistration
        {
            RetreatId              = retreatId,
            ChapterId              = chapterId,
            FirstName              = tx.FirstName,
            LastName               = tx.LastName,
            Email                  = tx.Email,
            Phone                  = tx.Phone,
            Address                = tx.Address?.Address1,
            City                   = tx.Address?.City,
            State                  = tx.Address?.State,
            Zip                    = tx.Address?.Zipcode,
            AmountPaid             = tx.Amount / 100m,
            PaymentStatus          = RegistrationPaymentStatus.Paid,
            PaymentMethod          = RegistrationPaymentMethod.GiveButter,
            RegistrationSource     = RegistrationSource.GiveButter,
            GiveButterTransactionId = tx.Id,
            RegisteredAt           = tx.TransactedAt
        };

        db.RetreatRegistrations.Add(reg);
        await db.SaveChangesAsync();
        return true;
    }
}
