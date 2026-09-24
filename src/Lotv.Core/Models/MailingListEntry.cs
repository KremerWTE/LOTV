using System.ComponentModel.DataAnnotations.Schema;

namespace Lotv.Core.Models;

/// <summary>Which annual card mailing an entry belongs to.</summary>
public enum MailingKind
{
    MothersDay = 0,
    FathersDay = 1
}

/// <summary>
/// One recipient on an annual Mother's Day / Father's Day mailing cycle
/// (e.g. cards mailed to bereaved mothers between one Mother's Day and the next).
/// </summary>
public class MailingListEntry
{
    public int Id { get; set; }
    public int? FamilyId { get; set; }
    public Family? Family { get; set; }

    public int Year { get; set; }
    public MailingKind Kind { get; set; } = MailingKind.MothersDay;
    public string MotherName { get; set; } = "";
    public string? FatherName { get; set; }
    public string StreetAddress { get; set; } = "";
    public string? Apt { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public string? Country { get; set; }

    /// <summary>Who the card is addressed to: the mother on a Mother's Day entry, the father on a Father's Day entry.</summary>
    [NotMapped]
    public string RecipientName => Kind == MailingKind.FathersDay ? (FatherName ?? "") : MotherName;

    public bool MothersDayOnly { get; set; }
    public bool FlaggedForReview { get; set; }
    public string? ReviewNote { get; set; }
    public bool Sent { get; set; }
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
