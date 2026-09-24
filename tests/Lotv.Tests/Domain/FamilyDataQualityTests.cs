using Lotv.Core.Models;

namespace Lotv.Tests.Domain;

/// <summary>Names that look wrong and missing contact details are caught so staff can call or email.</summary>
public class FamilyDataQualityTests
{
    private static Family Good() => new()
    {
        Parent1FirstName = "David", Parent1LastName = "Park", Parent2FirstName = "Jenny", Parent2LastName = "Park",
        Email = "d.park@example.com", Phone = "+13125550203", StreetAddress = "755 W. Belden Ave", City = "Chicago", State = "IL", Zip = "60614",
    };

    private static List<string> Fields(Family f) => FamilyDataQuality.Check(f).Issues.Select(i => i.Field).ToList();

    [Fact]
    public void ACompleteRecord_HasNoIssues()
    {
        var report = FamilyDataQuality.Check(Good());
        Assert.False(report.NeedsAttention);
        Assert.Equal("", report.Advice);
    }

    [Theory]
    [InlineData("Jrny6b6228d8")]          // random-looking, has numbers
    [InlineData("Smith2")]
    [InlineData("J")]
    [InlineData("Test")]
    [InlineData("asdf")]
    [InlineData("N/A")]
    [InlineData("aaaa")]
    [InlineData("")]
    [InlineData("Jo$hn")]
    public void ABadLastName_IsAProblem(string last)
    {
        var f = Good(); f.Parent1LastName = last;
        var issue = Assert.Single(FamilyDataQuality.Check(f).Issues, i => i.Field == "First parent's last name");
        Assert.Equal(DataIssueSeverity.Problem, issue.Severity);
    }

    [Theory]
    [InlineData("O'Brien")]
    [InlineData("Smith-Jones")]
    [InlineData("de la Cruz")]
    [InlineData("Nguyen")]
    [InlineData("Lynn")]
    [InlineData("Ng")]
    [InlineData("José")]
    public void RealNames_AreNotFlagged(string last)
    {
        var f = Good(); f.Parent1LastName = last; f.Parent2LastName = last;
        Assert.DoesNotContain(Fields(f), field => field.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ASecondParentWithNoFirstName_IsFlagged_ButNoSecondParentIsFine()
    {
        var f = Good(); f.Parent2FirstName = "";
        Assert.Contains("Second parent's first name", Fields(f));

        var single = Good(); single.Parent2FirstName = null; single.Parent2LastName = null;
        Assert.False(FamilyDataQuality.Check(single).NeedsAttention);
    }

    [Fact]
    public void AMissingOrBrokenEmail_IsAProblem_AndAdviceSaysToCall()
    {
        var f = Good(); f.Email = "not-an-email";
        var report = FamilyDataQuality.Check(f);
        Assert.Contains(report.Issues, i => i.Field == "Email" && i.Severity == DataIssueSeverity.Problem);
        Assert.StartsWith("Call the family", report.Advice);

        f.Email = "";
        Assert.Contains(Fields(f), x => x == "Email");
    }

    [Fact]
    public void AMissingPhone_IsAWarning_AndAdviceSaysToEmail()
    {
        var f = Good(); f.Phone = "";
        var report = FamilyDataQuality.Check(f);
        Assert.Contains(report.Issues, i => i.Field == "Phone" && i.Severity == DataIssueSeverity.Warning);
        Assert.False(report.HasProblems);
        Assert.StartsWith("Email the family", report.Advice);
    }

    [Fact]
    public void NoWayToReachTheFamily_SaysToCheckTheReferrer()
    {
        var f = Good(); f.Email = ""; f.Phone = "";
        Assert.Contains("referred", FamilyDataQuality.Check(f).Advice);
    }

    [Fact]
    public void AnIncompleteAddress_IsFlagged_FieldByField()
    {
        var f = Good(); f.StreetAddress = ""; f.City = ""; f.State = "Illinois"; f.Zip = "606";
        var fields = Fields(f);
        Assert.Contains("Street address", fields);
        Assert.Contains("City", fields);
        Assert.Contains("State", fields);
        Assert.Contains("Zip", fields);
    }

    [Fact]
    public void AFutureDateOfLoss_IsAWarning()
    {
        var f = Good(); f.DateOfLoss = DateTime.UtcNow.AddDays(30);
        Assert.Contains("Date of loss", Fields(f));
    }
}
