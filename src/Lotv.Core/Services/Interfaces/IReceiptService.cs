using Lotv.Core.Common;

namespace Lotv.Core.Services.Interfaces;

public interface IReceiptService
{
    /// <summary>Generate and email a tax receipt PDF for a donation.</summary>
    Task<Result> SendReceiptAsync(int donationId);

    /// <summary>Generate a year-end giving statement and email it to a donor.</summary>
    Task<Result> SendYearEndStatementAsync(int donorId, int year);

    /// <summary>Return an HTML receipt string for display or download; (false, null) if not found.</summary>
    Task<(bool Found, string? Html)> GetReceiptHtmlAsync(int donationId);

    /// <summary>Return an HTML year-end statement string; (false, null) if not found.</summary>
    Task<(bool Found, string? Html)> GetYearEndHtmlAsync(int donorId, int year);
}
