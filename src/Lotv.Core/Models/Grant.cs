namespace Lotv.Core.Models;

public class Grant
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public string GrantorName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime AwardedDate { get; set; }
    public DateTime? ReportDueDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Reporting, Complete, Expired
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
