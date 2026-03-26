namespace Lotv.Core.Models;

public class RecurringDonation
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int ChapterId { get; set; }

    /// <summary>Amount charged per billing cycle.</summary>
    public decimal Amount { get; set; }

    public DonationChannel Channel { get; set; } = DonationChannel.Online;
    public RecurringFrequency Frequency { get; set; }

    /// <summary>Next scheduled charge date (UTC).</summary>
    public DateTime NextChargeDate { get; set; }

    /// <summary>Stripe Subscription ID for automatic billing.</summary>
    public string? StripeSubscriptionId { get; set; }

    public RecurringStatus Status { get; set; } = RecurringStatus.Active;

    /// <summary>UTC timestamp when the donor first set up this schedule.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last successful charge.</summary>
    public DateTime? LastChargedAt { get; set; }

    /// <summary>Optional donor-specified end date; null = indefinite.</summary>
    public DateTime? EndsOn { get; set; }

    /// <summary>Optional campaign or fund designation.</summary>
    public string? Campaign { get; set; }

    public string? Notes { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────────

    public bool IsActive => Status == RecurringStatus.Active;

    /// <summary>True if the schedule has reached its end date.</summary>
    public bool IsExpired => EndsOn.HasValue && DateTime.UtcNow > EndsOn.Value;
}

public enum RecurringFrequency
{
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    Annually
}

public enum RecurringStatus
{
    Active,
    Paused,
    Cancelled,
    PastDue,    // payment failed; awaiting retry
    Completed   // reached EndsOn date
}

public static class RecurringFrequencyExtensions
{
    public static string ToDisplayName(this RecurringFrequency f) => f switch
    {
        RecurringFrequency.BiWeekly => "Every 2 weeks",
        RecurringFrequency.Monthly  => "Monthly",
        RecurringFrequency.Quarterly => "Quarterly",
        RecurringFrequency.Annually => "Annually",
        _                           => f.ToString()
    };

    /// <summary>Returns the next charge date from a given reference date.</summary>
    public static DateTime NextFrom(this RecurringFrequency f, DateTime from) => f switch
    {
        RecurringFrequency.Weekly    => from.AddDays(7),
        RecurringFrequency.BiWeekly  => from.AddDays(14),
        RecurringFrequency.Monthly   => from.AddMonths(1),
        RecurringFrequency.Quarterly => from.AddMonths(3),
        RecurringFrequency.Annually  => from.AddYears(1),
        _                            => from.AddMonths(1)
    };
}
