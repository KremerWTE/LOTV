namespace Lotv.Core.Models;

public class Family
{
    public int Id { get; set; }
    public string Parent1FirstName { get; set; } = "";
    public string Parent1LastName { get; set; } = "";
    public string? Parent2FirstName { get; set; }
    public string? Parent2LastName { get; set; }
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string StreetAddress { get; set; } = "";
    public string? Apt { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public PackageReason Reason { get; set; }
    public string? FaithTradition { get; set; }
    public string? ChildrenInitials { get; set; }
    public string? Story { get; set; }
    public string? ParishName { get; set; }
    public string? DioceseName { get; set; }
    public string? HowHeard { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public FamilyStatus Status { get; set; } = FamilyStatus.Active;
    public string? ContactNotes { get; set; }

    public string FullName => string.IsNullOrEmpty(Parent2FirstName)
        ? $"{Parent1FirstName} {Parent1LastName}"
        : $"{Parent1FirstName} & {Parent2FirstName} {Parent1LastName}";
}

public enum PackageReason
{
    Infertility,
    PrenatalDiagnosis,
    PrenatalLifeLimitingDiagnosis,
    Miscarriage,
    Stillbirth,
    InfantLoss,
    PastLoss,
    Other
}

public static class PackageReasonExtensions
{
    public static string ToDisplayName(this PackageReason r) => r switch
    {
        PackageReason.PrenatalDiagnosis             => "Prenatal Diagnosis",
        PackageReason.PrenatalLifeLimitingDiagnosis => "Prenatal Life-Limiting Diagnosis",
        PackageReason.InfantLoss                    => "Infant Loss",
        PackageReason.PastLoss                      => "Past Loss",
        _                                           => r.ToString()
    };
}

public enum FamilyStatus
{
    Active,
    Closed,
    FollowUp,
    Referred
}
