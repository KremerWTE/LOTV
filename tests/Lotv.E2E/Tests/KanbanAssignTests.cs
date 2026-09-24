using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>The Assign button on an unassigned Kanban card opens the picker and saves the assignment.</summary>
public class KanbanAssignTests : E2ETestBase
{
    public KanbanAssignTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task AssignButton_OpensPicker_AndAssigningMovesTheCardOffUnassigned()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        await Page.Locator(".kanban-card").First.WaitForAsync();

        var card = Page.Locator(".kanban-card-unassigned").First;
        var id = (await card.Locator(".kanban-card-id").InnerTextAsync()).Split('\n')[0].Trim();
        await card.Locator("button:has-text('Assign')").ClickAsync();

        await Page.Locator("button:has-text('Confirm Assignment')").WaitForAsync();
        Assert.True(await Page.Locator(".drawer select option").CountAsync() > 1);
        await Page.Locator(".drawer select").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.ClickAsync("button:has-text('Confirm Assignment')");

        await Page.Locator("button:has-text('Confirm Assignment')").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var assigned = Page.Locator($".kanban-card:has(.kanban-card-id:has-text('{id}')):not(.kanban-card-unassigned)");
        await assigned.First.WaitForAsync();
    }
}
