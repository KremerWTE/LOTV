namespace Lotv.Core.Services.Interfaces;

/// <summary>
/// Sends SMS messages to volunteers and staff.
/// Implement with Twilio (or any provider) in Lotv.Api.
/// </summary>
public interface ISmsService
{
    /// <summary>Send a free-form SMS to a single phone number.</summary>
    Task<SmsResult> SendAsync(string toPhoneNumber, string body);

    /// <summary>Notify a volunteer that a new assignment has been created.</summary>
    Task<SmsResult> NotifyAssignmentAsync(string toPhoneNumber, string volunteerFirstName,
        int caseId, string familyCity, string acceptUrl);

    /// <summary>Remind a volunteer their assignment is overdue.</summary>
    Task<SmsResult> SendOverdueReminderAsync(string toPhoneNumber, string volunteerFirstName,
        int caseId, int daysOverdue);

    /// <summary>Confirm to a volunteer that they have successfully checked in on a case.</summary>
    Task<SmsResult> ConfirmCheckInAsync(string toPhoneNumber, string volunteerFirstName, int caseId);

    /// <summary>Notify a volunteer their assignment was accepted.</summary>
    Task<SmsResult> NotifyAcceptedAsync(string toPhoneNumber, string volunteerFirstName, int caseId);
}

public record SmsResult(bool Success, string? MessageSid, string? Error);
