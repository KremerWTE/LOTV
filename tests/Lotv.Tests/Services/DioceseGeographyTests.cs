using Lotv.Api.Services;

namespace Lotv.Tests.Services;

/// <summary>The shipped diocesan map: every county has a diocese, and known places land where they really are.</summary>
public class DioceseGeographyTests
{
    private static readonly DioceseGeography Map = DioceseGeography.Instance;

    [Theory]
    [InlineData("IL", "Cook", "Archdiocese of Chicago")]
    [InlineData("IL", "Will County", "Diocese of Joliet")]
    [InlineData("IL", "DuPage", "Diocese of Joliet")]
    [InlineData("TX", "Harris", "Archdiocese of Galveston–Houston")]
    [InlineData("LA", "St. Charles Parish", "Archdiocese of New Orleans")]
    [InlineData("LA", "Lafayette", "Diocese of Lafayette in Louisiana")]
    [InlineData("IN", "Tippecanoe", "Diocese of Lafayette in Indiana")]
    [InlineData("OR", "Multnomah", "Archdiocese of Portland in Oregon")]
    [InlineData("ME", "Cumberland", "Diocese of Portland")]
    [InlineData("VA", "Richmond city", "Diocese of Richmond")]
    public void ACounty_IsInTheRightDiocese(string state, string county, string diocese)
    {
        var hit = Map.ByCounty(state, county);
        Assert.NotNull(hit);
        Assert.Equal(diocese, hit!.DioceseName);
        Assert.Null(hit.AlsoIn);
    }

    [Fact]
    public void ASplitCounty_NamesBothDioceses_SoItIsNeverGuessed()
    {
        var hit = Map.ByCounty("ID", "Fremont");
        Assert.NotNull(hit);
        Assert.Equal("Diocese of Boise", hit!.DioceseName);
        Assert.Equal("Diocese of Cheyenne", hit.AlsoIn);
    }

    [Theory]
    [InlineData("IL", "Naperville", "Diocese of Joliet")]
    [InlineData("IL", "Oak Park", "Archdiocese of Chicago")]
    [InlineData("TX", "Katy", "Archdiocese of Galveston–Houston")]
    [InlineData("NY", "Yonkers", "Archdiocese of New York")]
    [InlineData("MN", "St. Cloud", "Diocese of Saint Cloud")]
    public void ATown_IsInTheRightDiocese(string state, string city, string diocese) =>
        Assert.Equal(diocese, Map.ByPlace(state, city)?.DioceseName);

    [Fact]
    public void ANameThatIsInSeveralDioceses_OrIsUnknown_GivesNoAnswer()
    {
        Assert.Null(Map.ByPlace("IL", "Nowhereville"));
        Assert.Null(Map.ByCounty("IL", "Nowhere"));
        Assert.Null(Map.ByCounty(null, "Cook"));
        Assert.Null(Map.ByPlace("IL", ""));
    }

    [Fact]
    public void EveryDioceseNamedInTheMap_IsInTheUsDioceseList()
    {
        var known = DioceseDirectory.Reference().Select(d => d.Name).ToHashSet();
        foreach (var (state, county) in new[] { ("IL", "Cook"), ("CA", "Los Angeles"), ("AK", "Anchorage"), ("HI", "Honolulu"), ("MO", "Jackson") })
        {
            var hit = Map.ByCounty(state, county);
            Assert.NotNull(hit);
            Assert.Contains(hit!.DioceseName, known);
        }
    }
}
