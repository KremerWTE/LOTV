namespace Lotv.Core.Models;

/// <summary>Outcome of a Mother's Day mailing list CSV import (or dry run).</summary>
public record MailingImportResultDto(int Year, bool DryRun, int TotalRows, int Created, int SkippedDuplicates, List<MailingImportErrorDto> Errors);

/// <summary>A row that couldn't be imported; <see cref="Row"/> is the 1-based line in the file (line 1 is the header).</summary>
public record MailingImportErrorDto(int Row, string Problem);

public static class MailingImportLimits
{
    /// <summary>Largest CSV accepted, in bytes (the API enforces the same limit in characters).</summary>
    public const int MaxBytes = 2_000_000;
}
