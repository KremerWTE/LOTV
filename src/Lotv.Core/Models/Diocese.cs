namespace Lotv.Core.Models;

public class Diocese
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public string? CoordinatorName { get; set; }
    public string? CoordinatorEmail { get; set; }
    public int TotalParishes { get; set; }
    public int ActiveParishes { get; set; }
    public int TotalCasesFulfilled { get; set; }
    public decimal TotalDonations { get; set; }
}
