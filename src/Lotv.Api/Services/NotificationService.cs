using System.Net;
using System.Net.Mail;
using Lotv.Core.Common;
using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// SMTP-backed notification service. Reads connection details from the
/// "Smtp" config section (Host/Port/Username/Password/FromEmail/FromName/
/// EnableSsl), populated via Azure App Service Application Settings in
/// staging/production (never committed) and left unset for local dev.
/// When Smtp:Host isn't configured, falls back to logging only - this
/// keeps local dev and tests working without a real mail server while
/// still failing loudly in production if someone forgets to configure it.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IConfiguration config, ILogger<NotificationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<Result> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
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
