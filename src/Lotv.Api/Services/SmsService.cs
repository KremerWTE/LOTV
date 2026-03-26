using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Lotv.Api.Services;

/// <summary>
/// Twilio-backed SMS service. Set the following env vars / app settings:
///   Twilio:AccountSid   — Twilio Account SID (AC…)
///   Twilio:AuthToken    — Twilio Auth Token
///   Twilio:FromNumber   — E.164 sender number (e.g. +13125550100)
///
/// When credentials are absent (local dev / CI), all sends succeed as no-ops
/// and are logged with Success=true and ProviderMessageId="DEV_NOOP".
/// </summary>
public class SmsService(IConfiguration config, LotvDbContext db, ILogger<SmsService> log)
    : ISmsService
{
    private readonly string? _accountSid  = config["Twilio:AccountSid"];
    private readonly string? _authToken   = config["Twilio:AuthToken"];
    private readonly string? _fromNumber  = config["Twilio:FromNumber"];

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_accountSid) &&
        !string.IsNullOrWhiteSpace(_authToken)  &&
        !string.IsNullOrWhiteSpace(_fromNumber);

    // ── Public API ────────────────────────────────────────────────────────────

    public Task<SmsResult> SendAsync(string toPhoneNumber, string body)
        => SendCoreAsync(toPhoneNumber, body, SmsMessageType.Custom);

    public Task<SmsResult> NotifyAssignmentAsync(string toPhoneNumber, string volunteerFirstName,
        int caseId, string familyCity, string acceptUrl)
    {
        var body = $"Hi {volunteerFirstName}, a new LOTV assignment in {familyCity} needs you! " +
                   $"Case #{caseId}. Accept within 24 hrs: {acceptUrl}";
        return SendCoreAsync(toPhoneNumber, body, SmsMessageType.AssignmentNotification, caseId);
    }

    public Task<SmsResult> SendOverdueReminderAsync(string toPhoneNumber, string volunteerFirstName,
        int caseId, int daysOverdue)
    {
        var body = $"Hi {volunteerFirstName}, LOTV case #{caseId} is {daysOverdue} day(s) overdue. " +
                   $"Please log in to update the status or contact your coordinator.";
        return SendCoreAsync(toPhoneNumber, body, SmsMessageType.OverdueReminder, caseId);
    }

    public Task<SmsResult> ConfirmCheckInAsync(string toPhoneNumber, string volunteerFirstName, int caseId)
    {
        var body = $"Hi {volunteerFirstName}, you've checked in on LOTV case #{caseId}. " +
                   $"Thank you for serving this family!";
        return SendCoreAsync(toPhoneNumber, body, SmsMessageType.CheckInConfirmation, caseId);
    }

    public Task<SmsResult> NotifyAcceptedAsync(string toPhoneNumber, string volunteerFirstName, int caseId)
    {
        var body = $"Hi {volunteerFirstName}, your acceptance of LOTV case #{caseId} has been recorded. " +
                   $"Check the portal for delivery details.";
        return SendCoreAsync(toPhoneNumber, body, SmsMessageType.AssignmentAccepted, caseId);
    }

    // ── Core send + logging ───────────────────────────────────────────────────

    private async Task<SmsResult> SendCoreAsync(string to, string body,
        SmsMessageType type, int? caseId = null)
    {
        SmsResult result;

        if (!IsConfigured)
        {
            log.LogInformation("[SMS NOOP] To={To} Body={Body}", to, body);
            result = new SmsResult(true, "DEV_NOOP", null);
        }
        else
        {
            // Twilio REST API call — requires Twilio.AspNet.Core NuGet in production.
            // Implemented here as a plain HttpClient call to avoid adding the package now.
            try
            {
                using var http    = new HttpClient();
                var credentials   = Convert.ToBase64String(
                    System.Text.Encoding.ASCII.GetBytes($"{_accountSid}:{_authToken}"));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"]   = to,
                    ["From"] = _fromNumber!,
                    ["Body"] = body
                });

                var response = await http.PostAsync(
                    $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    // Parse sid from JSON cheaply without adding System.Text.Json dependency risk
                    var sid  = ExtractJsonField(json, "sid");
                    result   = new SmsResult(true, sid, null);
                }
                else
                {
                    var err = await response.Content.ReadAsStringAsync();
                    log.LogError("[SMS FAIL] To={To} Status={Status} Error={Err}", to, response.StatusCode, err);
                    result = new SmsResult(false, null, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[SMS EXCEPTION] To={To}", to);
                result = new SmsResult(false, null, ex.Message);
            }
        }

        // Persist log entry
        db.SmsLogs.Add(new SmsLog
        {
            ToPhoneNumber     = to,
            MessageType       = type,
            CaseId            = caseId,
            Body              = body,
            Success           = result.Success,
            ProviderMessageId = result.MessageSid,
            ErrorMessage      = result.Error,
            SentAt            = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return result;
    }

    private static string? ExtractJsonField(string json, string field)
    {
        var key = $"\"{field}\"";
        int idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx + key.Length);
        if (colon < 0) return null;
        int start = json.IndexOf('"', colon) + 1;
        int end   = json.IndexOf('"', start);
        return start > 0 && end > start ? json[start..end] : null;
    }
}
