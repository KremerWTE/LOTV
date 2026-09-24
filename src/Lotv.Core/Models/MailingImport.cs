namespace Lotv.Core.Models;

/// <summary>Outcome of a Mother's Day mailing list CSV import (or dry run).</summary>
public record MailingImportResultDto(int Year, bool DryRun, int TotalRows, int Created, int SkippedDuplicates, List<MailingImportErrorDto> Errors);

/// <summary>Outcome of filling a mailing list from the last year's requests.</summary>
public record MailingBuildResultDto(int Year, int Created, int AlreadyOnList, int NoFather);

/// <summary>A row that couldn't be imported; <see cref="Row"/> is the 1-based line in the file (line 1 is the header).</summary>
public record MailingImportErrorDto(int Row, string Problem);

/// <summary>Sample renders of every request email, and who the team emails go to.</summary>
public record EmailPreviewsDto(List<string> TeamRecipients, List<EmailPreviewDto> Emails, string Provider = "");

/// <summary>One email: who it is for, when it is sent, its subject and its full HTML.</summary>
public record EmailPreviewDto(string Key, string Audience, string Name, string When, string Subject, string Html);

public static class MailingImportLimits
{
    /// <summary>Largest CSV accepted, in bytes (the API enforces the same limit in characters).</summary>
    public const int MaxBytes = 2_000_000;
}
