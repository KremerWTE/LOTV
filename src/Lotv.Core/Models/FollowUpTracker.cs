namespace Lotv.Core.Models;

/// <summary>
/// Stephen's Ministry-style bereavement follow-up tracker: a family gets a
/// touchpoint (and a grief book mailed) at 3 weeks, 3 months, 6 months, and
/// 11 months after their loss.
/// </summary>
public class FollowUpTracker
{
    public int Id { get; set; }
    public int? FamilyId { get; set; }
    public Family? Family { get; set; }

    public string Parent1Name { get; set; } = "";
    public string? Parent2Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string StreetAddress { get; set; } = "";
    public string? Apt { get; set; }
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public string? Reason { get; set; }
    public string? ChildName { get; set; }
    public DateTime? DateOfLoss { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<FollowUpMilestone> Milestones { get; set; } = [];
}

/// <summary>One of the four scheduled touchpoints for a FollowUpTracker.</summary>
public class FollowUpMilestone
{
    public int Id { get; set; }
    public int FollowUpTrackerId { get; set; }
    public FollowUpTracker? FollowUpTracker { get; set; }

    public FollowUpMilestoneType Type { get; set; }
    public DateTime? DueDate { get; set; }
    public bool BookSent { get; set; }
}

public enum FollowUpMilestoneType
{
    ThreeWeeks,
    ThreeMonths,
    SixMonths,
    ElevenMonths
}

public static class FollowUpMilestoneTypeExtensions
{
    public static string ToDisplayName(this FollowUpMilestoneType t) => t switch
    {
        FollowUpMilestoneType.ThreeWeeks   => "3 Weeks",
        FollowUpMilestoneType.ThreeMonths  => "3 Months",
        FollowUpMilestoneType.SixMonths    => "6 Months",
        FollowUpMilestoneType.ElevenMonths => "11 Months",
        _ => t.ToString()
    };
}
