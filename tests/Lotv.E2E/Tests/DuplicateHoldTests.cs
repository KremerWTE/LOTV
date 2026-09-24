using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>A request that looks like a duplicate family stays off the board and the unassigned queue until staff review it.</summary>
public class DuplicateHoldTests : E2ETestBase
{
    public DuplicateHoldTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task PossibleDuplicate_IsOnlyOnThePossibleDuplicatesPage()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var name = $"Dupe{tag}";
        await using var api = await _browser.Playwright.APIRequest.NewContextAsync(new() { BaseURL = E2ESettings.ApiUrl });
        var resp = await api.PostAsync("/api/v1/public/apply", new()
        {
            DataObject = new
            {
                Family = new
                {
                    Parent1FirstName = "Tom", Parent1LastName = name, Parent2FirstName = "Ann", Parent2LastName = name,
                    Email = "d.park@example.com", Phone = "", StreetAddress = "1 Test St", City = "Chicago", State = "IL",
                    Zip = "60601", Reason = "Infertility", ChapterId = 1,
                },
                ForSelf = true, PackageType = "Comfort",
            }
        });
        Assert.Equal(201, resp.Status);

        await LoginAsAdminAsync();

        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        await Page.Locator(".kanban-card").First.WaitForAsync();
        Assert.Equal(0, await Page.Locator($".kanban-card:has-text('{name}')").CountAsync());

        await GoToAsync("/admin/queue");
        await WaitForBlazorAsync();
        await Page.Locator("table.data-table").WaitForAsync();
        Assert.Equal(0, await Page.Locator($"table.data-table:has-text('{name}')").CountAsync());

        await GoToAsync("/admin/families/duplicate-review");
        await WaitForBlazorAsync();
        await Page.Locator($"text={name}").First.WaitForAsync();
    }
}
