using Lotv.Core.Common;

namespace Lotv.Tests;

/// <summary>The State box is free text, so the same state arrives many ways; the Cases Map needs one two-letter code per state.</summary>
public class UsStatesTests
{
    [Theory]
    [InlineData("IL", "IL")]
    [InlineData("il", "IL")]
    [InlineData(" tx ", "TX")]
    [InlineData("Illinois", "IL")]
    [InlineData("ILLINOIS", "IL")]
    [InlineData("Ill.", "IL")]
    [InlineData("Wisc", "WI")]
    [InlineData("new   york", "NY")]
    [InlineData("North Carolina", "NC")]
    [InlineData("District of Columbia", "DC")]
    [InlineData("California", "CA")]
    public void RecognisesTheWaysPeopleWriteAState(string typed, string code) => Assert.Equal(code, UsStates.ToCode(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ontario")]
    [InlineData("ZZ")]
    [InlineData("N/A")]
    [InlineData("Illinoise")]
    public void NeverGuessesAtSomethingItCannotRecognise(string? typed) => Assert.Null(UsStates.ToCode(typed));
}
