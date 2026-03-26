namespace Lotv.Core.Models;

public class Expense
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";   // e.g. Supplies, Printing, Shipping, Staffing
    public DateTime PaidAt { get; set; }
    public string PaidBy { get; set; } = "";     // ApplicationUser.Id
    public string? ReceiptBlobUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
