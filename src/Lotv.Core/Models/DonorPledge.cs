namespace Lotv.Core.Models;

public class DonorPledge
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Total amount the donor has committed to give.</summary>
    public decimal PledgedAmount { get; set; }

    /// <summary>Running total of donations that have been applied to this pledge.</summary>
    public decimal FulfilledAmount { get; set; }

    /// <summary>Date by which the donor expects to fulfill the pledge.</summary>
    public DateTime TargetDate { get; set; }

    public PledgeStatus Status { get; set; } = PledgeStatus.Active;

    /// <summary>Optional campaign or fund this pledge is earmarked for.</summary>
    public string? Campaign { get; set; }

    /// <summary>Internal notes (staff-only, not shown to donor).</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────────

    public decimal RemainingAmount => PledgedAmount - FulfilledAmount;

    public decimal FulfillmentPercent =>
        PledgedAmount == 0 ? 0 : Math.Min(100m, FulfilledAmount / PledgedAmount * 100m);

    public bool IsOverdue =>
        Status == PledgeStatus.Active
        && DateTime.UtcNow > TargetDate
        && FulfilledAmount < PledgedAmount;

    public bool IsFulfilled => FulfilledAmount >= PledgedAmount;
}

public enum PledgeStatus
{
    Active,
    Fulfilled,
    Overdue,
    Cancelled,
    Lapsed      // donor did not fulfill and target date has passed without further contact
}
