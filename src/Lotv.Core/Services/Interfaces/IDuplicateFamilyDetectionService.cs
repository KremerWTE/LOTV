using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public record DuplicateFamilyMatch(Family Family, string Reason);

public interface IDuplicateFamilyDetectionService
{
    /// <summary>
    /// Looks for an existing Family in the same chapter that plausibly matches the
    /// candidate (e.g. a repeat submission for the same family). Checked in order of
    /// confidence: exact email, exact phone, then last name + ZIP. Returns the first
    /// match found, or null if nothing looks similar.
    /// </summary>
    Task<DuplicateFamilyMatch?> FindPossibleDuplicateAsync(Family candidate, int chapterId);
}
