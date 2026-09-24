using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>Mother's Day and Father's Day each have their own card list: one recipient per family, buildable from the last year's requests.</summary>
public class FathersDayPageTests : E2ETestBase
{
    public FathersDayPageTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task FathersDayPage_ListsFathers_AndBuildsFromRequests()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/fathers-day");
        await WaitForBlazorAsync();
        await AssertHeadingAsync("Father's Day Mailing");

        var table = Page.Locator("table.data-table");
        await table.WaitForAsync();
        Assert.Equal(1, await table.Locator("th:text-is('Father')").CountAsync());
        Assert.Equal(0, await table.Locator("th:text-is('Mother')").CountAsync());

        await Page.Locator("#md-build").ClickAsync();
        await Page.Locator("#md-build-result").WaitForAsync();
        await AssertVisibleAsync("text=skipped (no father on record)");
    }

    [Fact]
    public async Task MothersDayPage_ShowsOneMotherPerFamily_WithNoFatherColumn()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/mothers-day");
        await WaitForBlazorAsync();
        await AssertHeadingAsync("Mother's Day Mailing");

        var table = Page.Locator("table.data-table");
        await table.WaitForAsync();
        Assert.Equal(1, await table.Locator("th:text-is('Mother')").CountAsync());
        Assert.Equal(0, await table.Locator("th:text-is('Father')").CountAsync());
    }

    [Fact]
    public async Task CasesHub_HasATabForEachCardList()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/cases-hub");
        await WaitForBlazorAsync();

        await Page.Locator("button.tab-btn:has-text(\"Father's Day Mailing\")").ClickAsync();
        await Page.Locator("h2:has-text(\"Father's Day Mailing\")").WaitForAsync();
        await Page.Locator("table.data-table th:text-is('Father')").WaitForAsync();

        await Page.Locator("button.tab-btn:has-text(\"Mother's Day Mailing\")").ClickAsync();
        await Page.Locator("h2:has-text(\"Mother's Day Mailing\")").WaitForAsync();
        await Page.Locator("table.data-table th:text-is('Mother')").WaitForAsync();
    }
}
