namespace Lotv.Core.Models;

public class DonorTouchpoint
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public string TouchType { get; set; } = "Call"; // Call, Email, Meeting, Letter, Visit, Other
    public string Notes { get; set; } = "";
    public DateTime TouchDate { get; set; } = DateTime.UtcNow;
    public string StaffName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Donor? Donor { get; set; }
}
