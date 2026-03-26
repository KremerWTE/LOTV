namespace Lotv.Core.Models;

public class Parish
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int DioceseId { get; set; }
    public string DioceseName { get; set; } = "";
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public string? LiaisonName { get; set; }
    public string? LiaisonEmail { get; set; }
    public CertificationLevel CertificationLevel { get; set; }
    public int ActiveCases { get; set; }
    public int TotalCasesFulfilled { get; set; }
    public ParishStatus Status { get; set; } = ParishStatus.Active;
    public DateTime EnrolledDate { get; set; }
}

public enum CertificationLevel
{
    None,
    Level1,
    Level2,
    Level3
}

public enum ParishStatus
{
    Active,
    Inactive,
    Pending
}
