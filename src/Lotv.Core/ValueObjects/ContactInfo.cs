namespace Lotv.Core.ValueObjects;

public enum PreferredContact { Email, Phone, Text }

public record ContactInfo(
    string? Phone,
    string? Email,
    PreferredContact PreferredContact = PreferredContact.Email
);
