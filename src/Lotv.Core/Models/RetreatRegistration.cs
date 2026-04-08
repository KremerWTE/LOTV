namespace Lotv.Core.Models;

public class RetreatRegistration
{
    public int Id { get; set; }
    public int RetreatId { get; set; }
    public Retreat? Retreat { get; set; }
    public int ChapterId { get; set; }

    // Contact
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    // Special needs
    public string? DietaryNeeds { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // Payment
    public decimal AmountPaid { get; set; }
    public RegistrationPaymentStatus PaymentStatus { get; set; } = RegistrationPaymentStatus.Unpaid;
    public RegistrationPaymentMethod? PaymentMethod { get; set; }

    // Source tracking (for dedup)
    public RegistrationSource RegistrationSource { get; set; } = RegistrationSource.Manual;
    public string? GiveButterTransactionId { get; set; }   // unique — prevents duplicate syncs
    public string? DudaSubmissionId { get; set; }          // unique — prevents duplicate webhook replays

    public string? Notes { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}";
}

public enum RegistrationPaymentStatus
{
    Unpaid,
    Partial,
    Paid,
    Refunded,
    Complimentary
}

public enum RegistrationPaymentMethod
{
    GiveButter,
    Check,
    Cash,
    DudaForm,
    Waived
}

public enum RegistrationSource
{
    Manual,
    GiveButter,
    Duda
}
