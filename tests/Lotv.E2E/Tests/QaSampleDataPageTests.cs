using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>An HQ admin can load the QA sample data, see it in the portal, and remove it again.</summary>
public class QaSampleDataPageTests : E2ETestBase
{
    public QaSampleDataPageTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task LoadSampleData_ShowsItAcrossThePortal_AndRemovingItLeavesNothing()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/qa-sample-data");
        await WaitForBlazorAsync();
        await AssertHeadingAsync("QA Sample Data");
        await Page.Locator("#qa-status").WaitForAsync();

        // Start clean whatever an earlier run left
        if (await Page.Locator("#qa-status[data-loaded='true']").CountAsync() > 0)
        {
            await Page.Locator("#qa-remove").ClickAsync();
            await Page.Locator("#qa-status[data-loaded='false']").WaitForAsync();
        }

        // The load button stays off until the admin confirms
        Assert.True(await Page.Locator("#qa-load").IsDisabledAsync());
        await Page.Locator("#qa-confirm").CheckAsync();
        await Page.Locator("#qa-load").ClickAsync();
        await Page.Locator("#qa-status[data-loaded='true']").WaitForAsync();
        Assert.Equal("12", (await Page.Locator("#qa-families").InnerTextAsync()).Trim());
        Assert.Equal("3", (await Page.Locator("#qa-volunteers").InnerTextAsync()).Trim());

        // The sample families are in the portal: on the board, and flagged in the Father's Day list
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        var sampleCard = Page.Locator(".kanban-card", new() { HasText = "Whitaker" }).First;
        await sampleCard.WaitForAsync();
        Assert.True(await sampleCard.Locator("[data-sample]").CountAsync() > 0);      // clearly marked as a sample
        await GoToAsync("/admin/fathers-day");
        await WaitForBlazorAsync();
        var row = Page.Locator("tr", new() { HasText = "Lindgren" });
        await row.First.WaitForAsync();
        Assert.Contains("Flagged", await row.First.InnerTextAsync());

        // Remove it all
        await GoToAsync("/admin/qa-sample-data");
        await WaitForBlazorAsync();
        await Page.Locator("#qa-remove").ClickAsync();
        await Page.Locator("#qa-status[data-loaded='false']").WaitForAsync();
        Assert.Equal("0", (await Page.Locator("#qa-families").InnerTextAsync()).Trim());

        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        await Page.Locator(".kanban-card").First.WaitForAsync();
        Assert.Equal(0, await Page.Locator(".kanban-card", new() { HasText = "Whitaker" }).CountAsync());
    }
}
