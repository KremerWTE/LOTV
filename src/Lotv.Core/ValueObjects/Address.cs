namespace Lotv.Core.ValueObjects;

public record Address(
    string Street,
    string? Apt,
    string City,
    string State,
    string Zip,
    string Country = "US"
)
{
    public string OneLine => string.IsNullOrEmpty(Apt)
        ? $"{Street}, {City}, {State} {Zip}"
        : $"{Street} {Apt}, {City}, {State} {Zip}";
}
