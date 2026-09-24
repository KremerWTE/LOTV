using System.ComponentModel.DataAnnotations.Schema;

namespace Lotv.Core.Models;

/// <summary>
/// A routing rule for new requests: "requests like this go to this volunteer". Rules are tried in
/// order (lowest Priority first) before automatic scoring. Every condition that is set must match;
/// a rule needs at least one condition. If none of its volunteers is available (active and under the
/// chapter's case limit), the rule is skipped and the next rule (then automatic assignment) applies.
/// </summary>
public class AssignmentRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Lower numbers are tried first.</summary>
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;

    // ── Conditions (all that are set must match) ───────────────────────────────
    /// <summary>Comma-separated <see cref="PackageReason"/> names, e.g. "Stillbirth,InfantLoss".</summary>
    public string? Reasons { get; set; }
    /// <summary>Two-letter state of the family's address.</summary>
    public string? State { get; set; }
    public string? City { get; set; }
    /// <summary>Family zip code starts with this (e.g. "606").</summary>
    public string? ZipPrefix { get; set; }
    public int? ChapterId { get; set; }
    /// <summary>true = requests people make for themselves, false = requests made for someone else, null = either.</summary>
    public bool? ForSelf { get; set; }

    // ── Action ────────────────────────────────────────────────────────────────
    /// <summary>Comma-separated volunteer ids: one person, or a team. The least busy eligible one gets the request.</summary>
    public string AssignToVolunteerIds { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }

    [NotMapped]
    public IReadOnlyList<PackageReason> ReasonList =>
        (Reasons ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => Enum.TryParse<PackageReason>(r, true, out var v) ? (PackageReason?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();

    [NotMapped]
    public IReadOnlyList<int> VolunteerIdList =>
        (AssignToVolunteerIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();

    [NotMapped]
    public bool HasCondition =>
        ReasonList.Count > 0 || !string.IsNullOrWhiteSpace(State) || !string.IsNullOrWhiteSpace(City)
        || !string.IsNullOrWhiteSpace(ZipPrefix) || ChapterId.HasValue || ForSelf.HasValue;

    public bool Matches(PackageRequest request, Family? family)
    {
        if (!HasCondition) return false;
        if (ReasonList.Count > 0 && !ReasonList.Contains(request.Reason)) return false;
        if (ChapterId.HasValue && request.ChapterId != ChapterId.Value) return false;
        if (ForSelf.HasValue && request.IsForSelf != ForSelf.Value) return false;
        if (!string.IsNullOrWhiteSpace(State)
            && !string.Equals(family?.State?.Trim(), State.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(City)
            && !string.Equals(family?.City?.Trim(), City.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(ZipPrefix)
            && !(family?.Zip ?? "").Trim().StartsWith(ZipPrefix.Trim(), StringComparison.Ordinal)) return false;
        return true;
    }
}
