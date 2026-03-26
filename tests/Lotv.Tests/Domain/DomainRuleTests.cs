using Lotv.Core.Models;

namespace Lotv.Tests.Domain;

/// <summary>
/// Tests for computed properties and display-name extension methods on domain models.
/// No database or HTTP needed — pure model logic.
/// </summary>
public class DomainRuleTests
{
    // ── PackageRequest.IsOverdue ──────────────────────────────────────────────

    [Fact]
    public void IsOverdue_FreshRequest_ReturnsFalse()
    {
        var r = new PackageRequest { Status = CaseStatus.New, CreatedAt = DateTime.UtcNow };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_EightDayOldNewRequest_ReturnsTrue()
    {
        var r = new PackageRequest { Status = CaseStatus.New, CreatedAt = DateTime.UtcNow.AddDays(-8) };
        Assert.True(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_EightDayOldInProgressRequest_ReturnsTrue()
    {
        var r = new PackageRequest { Status = CaseStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-8) };
        Assert.True(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_FulfilledRequest_ReturnsFalse()
    {
        var r = new PackageRequest { Status = CaseStatus.Fulfilled, CreatedAt = DateTime.UtcNow.AddDays(-14) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_CancelledRequest_ReturnsFalse()
    {
        var r = new PackageRequest { Status = CaseStatus.Cancelled, CreatedAt = DateTime.UtcNow.AddDays(-14) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_ShippedRequest_ReturnsFalse()
    {
        var r = new PackageRequest { Status = CaseStatus.Shipped, CreatedAt = DateTime.UtcNow.AddDays(-14) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void IsOverdue_SixDaysOld_ReturnsFalse()
    {
        // 6 days old is safely below the 7-day threshold
        var r = new PackageRequest { Status = CaseStatus.New, CreatedAt = DateTime.UtcNow.AddDays(-6) };
        Assert.False(r.IsOverdue);
    }

    // ── PackageReason.ToDisplayName ───────────────────────────────────────────

    [Theory]
    [InlineData(PackageReason.PrenatalDiagnosis,             "Prenatal Diagnosis")]
    [InlineData(PackageReason.PrenatalLifeLimitingDiagnosis, "Prenatal Life-Limiting Diagnosis")]
    [InlineData(PackageReason.InfantLoss,                    "Infant Loss")]
    [InlineData(PackageReason.PastLoss,                      "Past Loss")]
    [InlineData(PackageReason.Infertility,                   "Infertility")]
    [InlineData(PackageReason.Miscarriage,                   "Miscarriage")]
    [InlineData(PackageReason.Stillbirth,                    "Stillbirth")]
    [InlineData(PackageReason.Other,                         "Other")]
    public void PackageReason_ToDisplayName_ReturnsExpected(PackageReason reason, string expected)
    {
        Assert.Equal(expected, reason.ToDisplayName());
    }

    // ── VolunteerRole.ToDisplayName ───────────────────────────────────────────

    [Theory]
    [InlineData(VolunteerRole.PackageAssembler, "Package Assembler")]
    [InlineData(VolunteerRole.PrayerAmbassador, "Prayer Ambassador")]
    [InlineData(VolunteerRole.ParishLiaison,    "Parish Liaison")]
    [InlineData(VolunteerRole.EventHelper,      "Event Helper")]
    [InlineData(VolunteerRole.Admin,            "Admin Support")]
    [InlineData(VolunteerRole.Driver,           "Driver")]
    public void VolunteerRole_ToDisplayName_ReturnsExpected(VolunteerRole role, string expected)
    {
        Assert.Equal(expected, role.ToDisplayName());
    }

    // ── DonationChannel.ToDisplayName ────────────────────────────────────────

    [Theory]
    [InlineData(DonationChannel.SilentAuction,  "Silent Auction")]
    [InlineData(DonationChannel.CorporateMatch, "Corporate Match")]
    [InlineData(DonationChannel.PlannedGiving,  "Planned Giving")]
    [InlineData(DonationChannel.Online,         "Online")]
    [InlineData(DonationChannel.Check,          "Check")]
    [InlineData(DonationChannel.Cash,           "Cash")]
    [InlineData(DonationChannel.Gala,           "Gala")]
    [InlineData(DonationChannel.Other,          "Other")]
    public void DonationChannel_ToDisplayName_ReturnsExpected(DonationChannel channel, string expected)
    {
        Assert.Equal(expected, channel.ToDisplayName());
    }

    // ── Volunteer.FullName ────────────────────────────────────────────────────

    [Fact]
    public void Volunteer_FullName_ConcatenatesFirstAndLast()
    {
        var v = new Volunteer { FirstName = "Jane", LastName = "Smith" };
        Assert.Equal("Jane Smith", v.FullName);
    }

    // ── Family.FullName ───────────────────────────────────────────────────────

    [Fact]
    public void Family_FullName_SingleParent_ReturnsParent1Name()
    {
        var f = new Family { Parent1FirstName = "Mary", Parent1LastName = "Jones" };
        Assert.Equal("Mary Jones", f.FullName);
    }

    [Fact]
    public void Family_FullName_TwoParents_ReturnsBothFirstNames()
    {
        var f = new Family
        {
            Parent1FirstName = "Mary",
            Parent2FirstName = "John",
            Parent1LastName  = "Jones"
        };
        Assert.Equal("Mary & John Jones", f.FullName);
    }
}
