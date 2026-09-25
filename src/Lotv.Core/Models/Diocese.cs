namespace Lotv.Core.Models;

public class Diocese
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string? Region { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public string? CoordinatorName { get; set; }
    public string? CoordinatorEmail { get; set; }
    /// <summary>Listed so parishes can belong to it, but not a partner yet: never counted in "dioceses reached".</summary>
    public bool IsDirectoryOnly { get; set; }
    public int TotalParishes { get; set; }
    public int ActiveParishes { get; set; }
    public int TotalCasesFulfilled { get; set; }
    public decimal TotalDonations { get; set; }
}
