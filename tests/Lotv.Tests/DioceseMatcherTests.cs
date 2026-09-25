using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Tests;

/// <summary>A parish is only tied to a diocese when that is certain; otherwise it is flagged, never guessed.</summary>
public class DioceseMatcherTests
{
    private static Diocese D(int id, string name, string city, string state) => new() { Id = id, Name = name, City = city, State = state };

    private static readonly List<Diocese> All =
    [
        D(1, "Archdiocese of Chicago", "Chicago", "IL"),
        D(2, "Diocese of Joliet", "Joliet", "IL"),
        D(3, "Diocese of Springfield in Illinois", "Springfield", "IL"),
        D(4, "Diocese of Cheyenne", "Cheyenne", "WY"),          // the only diocese in Wyoming
        D(5, "Diocese of Portland", "Portland", "ME"),
        D(6, "Archdiocese of Portland in Oregon", "Portland", "OR"),
        D(7, "Diocese of Portland", "Portland", "OR"),          // deliberately ambiguous by name alone
    ];

    [Theory]
    [InlineData("Archdiocese of Chicago", "chicago")]
    [InlineData("Diocese of Fort Wayne-South Bend", "fort wayne south bend")]
    [InlineData("Archdiocese of St. Paul and Minneapolis", "st paul and minneapolis")]
    [InlineData("Saint Paul and Minneapolis", "st paul and minneapolis")]
    [InlineData("Diocese of Springfield in Illinois", "springfield")]      // the state is where it is, not what it is called
    [InlineData("Archdiocese of Portland in Oregon", "portland")]
    [InlineData("Archdiocese of Kansas City in Kansas", "kansas city")]
    [InlineData("Diocese of Kansas City–Saint Joseph", "kansas city st joseph")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Normalize_ReducesADioceseNameToItsDistinguishingWords(string? name, string expected) =>
        Assert.Equal(expected, DioceseMatcher.Normalize(name));

    [Fact]
    public void ANamedDiocese_IsMatchedWhateverTheWording()
    {
        var m = DioceseMatcher.Find(All, "Chicago Archdiocese", "Naperville", "IL");
        Assert.Equal(1, m.Diocese?.Id);
        Assert.Equal("named in the list", m.How);
        Assert.Equal(1, DioceseMatcher.Find(All, "the Roman Catholic Archdiocese of Chicago", null, null).Diocese?.Id);
    }

    [Fact]
    public void ANameThatIsNotOnFile_IsNotGuessedFromTheState()
    {
        var m = DioceseMatcher.Find(All, "Diocese of Nowhere", "Chicago", "IL");
        Assert.Null(m.Diocese);
        Assert.Contains("no diocese called", m.How);
    }

    [Fact]
    public void TheSameNameInTwoStates_NeedsTheState_ToChoose()
    {
        Assert.Equal(5, DioceseMatcher.Find(All, "Diocese of Portland", "Biddeford", "ME").Diocese?.Id);
        var ambiguous = DioceseMatcher.Find(All, "Diocese of Portland", null, null);
        Assert.Null(ambiguous.Diocese);
        // all three are "Portland" once "in Oregon" is dropped from the archdiocese name
        Assert.Equal([5, 6, 7], ambiguous.Candidates.Select(d => d.Id).OrderBy(i => i));
    }

    [Fact]
    public void WithNoName_AParishInADiocesesSeatCity_BelongsToIt()
    {
        var m = DioceseMatcher.Find(All, null, "Joliet", "il");
        Assert.Equal(2, m.Diocese?.Id);
        Assert.Equal("same city as the diocese's seat", m.How);
        Assert.Equal(1, DioceseMatcher.Find(All, "", "chicago", "IL").Diocese?.Id);
    }

    [Fact]
    public void WithNoName_AStateWithOneDiocese_ThatDiocese()
    {
        var m = DioceseMatcher.Find(All, null, "Laramie", "Wyoming");
        Assert.Equal(4, m.Diocese?.Id);
        Assert.Equal("the only diocese in WY", m.How);
    }

    [Fact]
    public void WithNoName_AStateWithSeveralDioceses_AndANonSeatCity_IsFlaggedWithCandidates_NotGuessed()
    {
        var m = DioceseMatcher.Find(All, null, "Naperville", "IL");
        Assert.Null(m.Diocese);
        Assert.Equal([1, 2, 3], m.Candidates.Select(d => d.Id).OrderBy(i => i));
        Assert.Contains("choose one", m.How);
    }

    [Fact]
    public void AListThatSaysJustSpringfield_FindsSpringfieldInIllinois_WhenTheStateIsKnown()
    {
        var all = new List<Diocese> { D(1, "Diocese of Springfield in Illinois", "Springfield", "IL"), D(2, "Diocese of Springfield in Massachusetts", "Springfield", "MA"), D(3, "Diocese of Springfield–Cape Girardeau", "Springfield", "MO") };
        Assert.Equal(1, DioceseMatcher.Find(all, "Diocese of Springfield", "Decatur", "IL").Diocese?.Id);
        Assert.Equal(2, DioceseMatcher.Find(all, "Springfield", "Holyoke", "Massachusetts").Diocese?.Id);
        Assert.Null(DioceseMatcher.Find(all, "Diocese of Springfield", null, null).Diocese);   // three of them: the state is needed
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Springfield", null)]
    [InlineData("Paris", "France")]
    [InlineData("Anchorage", "AK")]   // no diocese on file for Alaska in this list
    public void NothingToGoOn_IsNotAMatch(string? city, string? state) =>
        Assert.Null(DioceseMatcher.Find(All, null, city, state).Diocese);
}
