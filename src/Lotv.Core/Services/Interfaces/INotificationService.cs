using Lotv.Core.Common;

namespace Lotv.Core.Services.Interfaces;

public interface INotificationService
{
    Task<Result> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
    Task<Result> SendEmailTemplateAsync(string toEmail, string toName, string templateId, object templateData);
    Task<Result> SendSmsAsync(string toPhone, string message);
    Task QueueNotificationAsync(string userId, string type, string message, object? payload = null);
}
