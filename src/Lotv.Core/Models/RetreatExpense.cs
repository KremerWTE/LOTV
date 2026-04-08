namespace Lotv.Core.Models;

public class RetreatExpense
{
    public int Id { get; set; }
    public int RetreatId { get; set; }
    public Retreat? Retreat { get; set; }
    public int ChapterId { get; set; }
    public string Description { get; set; } = "";
    public RetreatExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidBy { get; set; }    // staff name or vendor
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum RetreatExpenseCategory
{
    Venue,
    Catering,
    Materials,
    Speaker,
    Travel,
    Marketing,
    AV,
    Other
}
