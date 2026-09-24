using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lotv.Core.Common;
using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// Sends email through SocketLabs when "SocketLabs:ServerId" and "SocketLabs:ApiKey" are set (their HTTP injection
/// API), else through SMTP when "Smtp:Host" is set, else only logs - which keeps local dev and tests working with no
/// mail server. Credentials come from deployment secrets and are never committed or logged. Replies go to the
/// configured Reply-To ("SocketLabs:ReplyTo" / "Smtp:ReplyTo"), since our emails invite people to reply.
/// </summary>
public class NotificationService : INotificationService
{
    public const string DefaultSocketLabsEndpoint = "https://inject.socketlabs.com/api/v1/email";

    private static readonly HttpClient FallbackHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;
    private readonly IHttpClientFactory? _httpFactory;

    public NotificationService(IConfiguration config, ILogger<NotificationService> logger, IHttpClientFactory? httpFactory = null)
    {
        _config = config;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    private static bool HasSocketLabs(IConfiguration c) =>
        int.TryParse(c["SocketLabs:ServerId"], out var id) && id > 0 && !string.IsNullOrWhiteSpace(c["SocketLabs:ApiKey"]);

    /// <summary>Which mechanism emails will go out through right now: "SocketLabs", "SMTP" or "Log only".</summary>
    public static string ActiveProvider(IConfiguration c) =>
        HasSocketLabs(c) ? "SocketLabs" : !string.IsNullOrWhiteSpace(c["Smtp:Host"]) ? "SMTP" : "Log only";

    /// <summary>A plain-text version of an HTML email, for mail clients (and spam filters) that want one.</summary>
    public static string ToPlainText(string html)
    {
        var text = Regex.Replace(html, @"<(style|script|head)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*(br|/p|/h[1-6]|/li|/tr|/table)\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<a\s[^>]*href=""([^""]+)""[^>]*>(.*?)</a>", "$2 ($1)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n");
        return Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
    }

    private async Task<Result> SendViaSocketLabsAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var fromEmail = _config["SocketLabs:FromEmail"] ?? "no-reply@lotvministry.org";
        var fromName = _config["SocketLabs:FromName"] ?? "LOTV Ministry";
        var replyTo = _config["SocketLabs:ReplyTo"];
        var endpoint = _config["SocketLabs:Endpoint"] is { Length: > 0 } e ? e : DefaultSocketLabsEndpoint;

        var message = new Dictionary<string, object?>
        {
            ["to"] = new[] { new { emailAddress = toEmail, friendlyName = toName } },
            ["from"] = new { emailAddress = fromEmail, friendlyName = fromName },
            ["subject"] = subject,
            ["textBody"] = ToPlainText(htmlBody),
            ["htmlBody"] = htmlBody,
        };
        if (!string.IsNullOrWhiteSpace(replyTo)) message["replyTo"] = new { emailAddress = replyTo, friendlyName = fromName };

        var payload = new
        {
            serverId = int.Parse(_config["SocketLabs:ServerId"]!),
            apiKey = _config["SocketLabs:ApiKey"],
            messages = new[] { message },
        };

        try
        {
            var http = _httpFactory?.CreateClient() ?? FallbackHttp;
            using var response = await http.PostAsJsonAsync(endpoint, payload);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SocketLabs rejected the email to {Email}: HTTP {Status}", toEmail, (int)response.StatusCode);
                return Result.Fail($"Email send failed: SocketLabs returned HTTP {(int)response.StatusCode}.");
            }

            var code = "";
            try { code = JsonDocument.Parse(body).RootElement.TryGetProperty("ErrorCode", out var c) ? c.GetString() ?? "" : ""; }
            catch (JsonException) { /* an unreadable body is treated as a failure below */ }
            if (!code.Equals("Success", StringComparison.OrdinalIgnoreCase) && !code.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("SocketLabs did not accept the email to {Email}: {Code}", toEmail, string.IsNullOrEmpty(code) ? "unreadable response" : code);
                return Result.Fail($"Email send failed: SocketLabs said {(string.IsNullOrEmpty(code) ? "an unreadable response" : code)}.");
            }
            _logger.LogInformation("Email sent via SocketLabs to {Email}: {Subject}", toEmail, subject);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Failed to reach SocketLabs to send an email to {Email}", toEmail);
            return Result.Fail($"Email send failed: {ex.Message}");
        }
    }

    public async Task<Result> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        // Addresses on the reserved .invalid domain can never receive mail (QA sample records use them): log, don't send.
        if (toEmail.Trim().EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[Email - undeliverable .invalid address, not sent] To: {Email} | Subject: {Subject}", toEmail, subject);
            return Result.Ok();
        }

        if (HasSocketLabs(_config)) return await SendViaSocketLabsAsync(toEmail, toName, subject, htmlBody);

        var host = _config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("[Email - SMTP not configured, logging only] To: {Email} | Subject: {Subject}\n{Body}", toEmail, subject, htmlBody);
            return Result.Ok();
        }

        var port = _config.GetValue<int?>("Smtp:Port") ?? 587;
        var enableSsl = _config.GetValue<bool?>("Smtp:EnableSsl") ?? true;
        var fromEmail = _config["Smtp:FromEmail"] ?? _config["Smtp:Username"] ?? "no-reply@lotvministry.org";
        var fromName = _config["Smtp:FromName"] ?? "LOTV Ministry";
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };
            if (!string.IsNullOrWhiteSpace(username))
                client.Credentials = new NetworkCredential(username, password);

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));
            if (_config["Smtp:ReplyTo"] is { Length: > 0 } smtpReplyTo) message.ReplyToList.Add(new MailAddress(smtpReplyTo, fromName));

            await client.SendMailAsync(message);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SMTP host {Host}", toEmail, host);
            return Result.Fail($"Email send failed: {ex.Message}");
        }
    }

    public Task<Result> SendEmailTemplateAsync(string toEmail, string toName, string templateId, object templateData)
    {
        _logger.LogInformation("[Email Template] To: {Email} | Template: {Id}", toEmail, templateId);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendSmsAsync(string toPhone, string message)
    {
        _logger.LogInformation("[SMS] To: {Phone} | {Message}", toPhone, message);
        return Task.FromResult(Result.Ok());
    }

    public Task QueueNotificationAsync(string userId, string type, string message, object? payload = null)
    {
        _logger.LogInformation("[Notification] User: {UserId} | Type: {Type} | {Message}", userId, type, message);
        return Task.CompletedTask;
    }
}
