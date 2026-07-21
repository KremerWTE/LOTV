using System.Text.RegularExpressions;
using Lotv.Api.Data;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

public class DuplicateFamilyDetectionService : IDuplicateFamilyDetectionService
{
    private readonly LotvDbContext _db;

    public DuplicateFamilyDetectionService(LotvDbContext db) => _db = db;

    public async Task<DuplicateFamilyMatch?> FindPossibleDuplicateAsync(Family candidate, int chapterId)
    {
        var candidateEmail = Normalize(candidate.Email);
        var candidatePhone = NormalizePhone(candidate.Phone);
        var candidateLastName = Normalize(candidate.Parent1LastName);
        var candidateZip = Normalize(candidate.Zip);

        // Small-nonprofit scale (hundreds, not millions, of families per chapter) —
        // loading the chapter's families and comparing in memory is simpler and fast
        // enough here than trying to express fuzzy matching in a single EF query.
        var chapterFamilies = await _db.Families
            .Where(f => f.ChapterId == chapterId)
            .ToListAsync();

        if (!string.IsNullOrEmpty(candidateEmail))
        {
            var byEmail = chapterFamilies.FirstOrDefault(f => Normalize(f.Email) == candidateEmail);
            if (byEmail is not null)
                return new DuplicateFamilyMatch(byEmail, $"Same email address as existing family #{byEmail.Id} ({byEmail.FullName})");
        }

        if (!string.IsNullOrEmpty(candidatePhone))
        {
            var byPhone = chapterFamilies.FirstOrDefault(f => NormalizePhone(f.Phone) == candidatePhone);
            if (byPhone is not null)
                return new DuplicateFamilyMatch(byPhone, $"Same phone number as existing family #{byPhone.Id} ({byPhone.FullName})");
        }

        if (!string.IsNullOrEmpty(candidateLastName) && !string.IsNullOrEmpty(candidateZip))
        {
            var byNameZip = chapterFamilies.FirstOrDefault(f =>
                Normalize(f.Parent1LastName) == candidateLastName && Normalize(f.Zip) == candidateZip);
            if (byNameZip is not null)
                return new DuplicateFamilyMatch(byNameZip, $"Same last name and ZIP code as existing family #{byNameZip.Id} ({byNameZip.FullName})");
        }

        return null;
    }

    private static string Normalize(string? s) => (s ?? "").Trim().ToLowerInvariant();

    private static string NormalizePhone(string? s) => Regex.Replace(s ?? "", @"\D", "");
}
