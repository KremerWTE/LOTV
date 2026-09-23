using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>An assigned Kanban card can be unassigned, which returns it to New and shows the Assign button again.</summary>
public class KanbanUnassignTests : E2ETestBase
{
    public KanbanUnassignTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task UnassignButton_ReturnsTheCardToUnassigned()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        await Page.Locator(".kanban-card").First.WaitForAsync();

        var card = Page.Locator(".kanban-card:not(.kanban-card-unassigned):has(button[data-action='unassign'])").First;
        var id = (await card.Locator(".kanban-card-id").InnerTextAsync()).Split('\n')[0].Trim();
        await card.Locator("button[data-action='unassign']").ClickAsync();

        var same = Page.Locator($".kanban-card-unassigned:has(.kanban-card-id:has-text('{id}'))");
        await same.First.WaitForAsync();
        Assert.True(await same.Locator("button:has-text('Assign')").CountAsync() >= 1);
    }
}
