using Lotv.Core.Common;
using Lotv.Core.Services.Interfaces;

namespace Lotv.Api.Services;

/// <summary>
/// Notification service stub. Replace with SendGrid implementation for production.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public Task<Result> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        _logger.LogInformation("[Email] To: {Email} | Subject: {Subject}", toEmail, subject);
        return Task.FromResult(Result.Ok());
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
