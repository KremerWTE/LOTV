namespace Lotv.Core.Models;

public class FamilyNote
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public string NoteType { get; set; } = "General"; // General, MilestoneDate, FollowUp, SupportCall, Prayer, Referral
    public string Content { get; set; } = "";
    public DateTime? MilestoneDate { get; set; } // for anniversary, due date, birth date, etc.
    public string StaffName { get; set; } = "";
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Family? Family { get; set; }
}
