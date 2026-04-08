namespace Lotv.Core.Models;

public class Retreat
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public string Location { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int Capacity { get; set; }
    public decimal TicketPrice { get; set; }        // 0 = free
    public decimal GoalAmount { get; set; }
    public string? GiveButterCampaignId { get; set; } // filters which GB transactions belong here
    public RetreatStatus Status { get; set; } = RetreatStatus.Draft;
    public string CreatedBy { get; set; } = "";     // ApplicationUser.Id
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RetreatRegistration> Registrations { get; set; } = [];
    public ICollection<RetreatExpense> Expenses { get; set; } = [];
}

public enum RetreatStatus
{
    Draft,
    Open,
    Closed,
    Completed,
    Cancelled
}
