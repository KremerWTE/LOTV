using Lotv.Core.Common;

namespace Lotv.Tests.Common;

public class PagedResultTests
{
    // ── TotalPages ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 10, 1)]   // exactly one page
    [InlineData(11, 10, 2)]   // one item overflows to page 2
    [InlineData(20, 10, 2)]   // exactly two pages
    [InlineData(21, 10, 3)]   // three pages
    [InlineData(0,  10, 0)]   // empty result
    [InlineData(1,  100, 1)]  // one item, large page size
    public void TotalPages_CalculatesCorrectly(int totalCount, int pageSize, int expected)
    {
        var result = new PagedResult<int>([], totalCount, page: 1, pageSize);
        Assert.Equal(expected, result.TotalPages);
    }

    // ── HasNextPage ───────────────────────────────────────────────────────────

    [Fact]
    public void HasNextPage_WhenNotOnLastPage_ReturnsTrue()
    {
        var result = new PagedResult<int>([], totalCount: 30, page: 1, pageSize: 10);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void HasNextPage_WhenOnLastPage_ReturnsFalse()
    {
        var result = new PagedResult<int>([], totalCount: 30, page: 3, pageSize: 10);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void HasNextPage_WhenOnlyOnePage_ReturnsFalse()
    {
        var result = new PagedResult<int>([], totalCount: 5, page: 1, pageSize: 10);
        Assert.False(result.HasNextPage);
    }

    // ── HasPreviousPage ───────────────────────────────────────────────────────

    [Fact]
    public void HasPreviousPage_WhenOnFirstPage_ReturnsFalse()
    {
        var result = new PagedResult<int>([], totalCount: 30, page: 1, pageSize: 10);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void HasPreviousPage_WhenOnSecondPage_ReturnsTrue()
    {
        var result = new PagedResult<int>([], totalCount: 30, page: 2, pageSize: 10);
        Assert.True(result.HasPreviousPage);
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Items_ArePreservedExactly()
    {
        var items  = new List<string> { "a", "b", "c" };
        var result = new PagedResult<string>(items, totalCount: 100, page: 1, pageSize: 10);

        Assert.Equal(items, result.Items);
    }

    // ── Properties exposed ────────────────────────────────────────────────────

    [Fact]
    public void Properties_AreSetFromConstructor()
    {
        var result = new PagedResult<int>([], totalCount: 55, page: 3, pageSize: 10);

        Assert.Equal(55, result.TotalCount);
        Assert.Equal(3,  result.Page);
        Assert.Equal(10, result.PageSize);
    }

    // ── Middle page ───────────────────────────────────────────────────────────

    [Fact]
    public void MiddlePage_HasBothPreviousAndNext()
    {
        var result = new PagedResult<int>([], totalCount: 30, page: 2, pageSize: 10);

        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }
}
