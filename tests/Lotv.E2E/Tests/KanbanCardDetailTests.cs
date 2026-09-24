using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>Clicking a Kanban card opens the full case page: request info, family, Mother's Day card, follow-up, box contents.</summary>
public class KanbanCardDetailTests : E2ETestBase
{
    public KanbanCardDetailTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task ClickingACard_OpensTheFullCasePage_WithAllSections()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        var card = Page.Locator(".kanban-card").First;
        await card.WaitForAsync();
        var id = (await card.Locator(".kanban-card-id").InnerTextAsync()).Split('\n')[0].Trim().TrimStart('#');

        await card.Locator(".kanban-card-id").ClickAsync();
        await Page.WaitForURLAsync(u => u.EndsWith($"/admin/cases/{id}"));

        foreach (var panel in new[] { "request-info", "mothers-day", "follow-up", "family-requests" })
            await Page.Locator($"[data-panel='{panel}']").WaitForAsync();
        await AssertVisibleAsync("text=Packing List");
        await AssertVisibleAsync("text=Notes Thread");
        await AssertVisibleAsync("text=Activity Log");
        await AssertVisibleAsync("text=Family");
    }
}
