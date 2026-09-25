namespace Lotv.Core.Common;

/// <summary>Where a county or a town sits in the diocesan map.</summary>
/// <param name="DioceseName">The diocese's name as it is in the U.S. diocese list.</param>
/// <param name="DioceseState">The state of that diocese's seat (tells apart two dioceses called "Portland").</param>
/// <param name="AlsoIn">When a county is split between two dioceses, the other one; a parish there can't be placed by county alone.</param>
/// <param name="How">Plain-language reason, for the review screen.</param>
public sealed record GeographyHit(string DioceseName, string DioceseState, string? AlsoIn, string How);

/// <summary>
/// The diocesan map of the United States: which diocese covers a county, and which covers a town. It answers only when the
/// map is unambiguous; a county split between two dioceses, or a town name that exists in several dioceses, gives no answer.
/// </summary>
public interface IDioceseGeography
{
    /// <summary>The diocese covering a county (name without "County"), or null when unknown.</summary>
    GeographyHit? ByCounty(string? stateCode, string? county);

    /// <summary>The diocese covering a town, only when every place with that name in the state is in the same diocese.</summary>
    GeographyHit? ByPlace(string? stateCode, string? city);
}
