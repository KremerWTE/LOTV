namespace Lotv.Core.Models;

public class EventAttendee
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public MinistryEvent? Event { get; set; }
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int TicketCount { get; set; } = 1;
    public decimal AmountPaid { get; set; }
    public DonationChannel PaymentChannel { get; set; }
    public bool CheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    /// <summary>Unique token encoded in the attendee's QR ticket. Generated on registration.</summary>
    public string TicketCode { get; set; } = Guid.NewGuid().ToString("N");
    public string? Notes { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
