using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>The Unassigned Queue shows who the family is and how to reach them, in the table and in the Assign drawer.</summary>
public class QueueFamilyInfoTests : E2ETestBase
{
    public QueueFamilyInfoTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task Queue_ShowsFamilyContactAndLocation_AndTheDrawerShowsTheFullFamily()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/queue");
        await WaitForBlazorAsync();
        var table = Page.Locator("table.data-table");
        await table.WaitForAsync();

        foreach (var heading in new[] { "Family", "Contact", "Location" })
            Assert.True(await table.Locator($"th:text-is('{heading}')").CountAsync() == 1, heading);

        var row = table.Locator("tbody tr").First;
        Assert.Contains("@", await row.Locator("td").Nth(2).InnerTextAsync());

        await row.Locator("button:has-text('Assign')").ClickAsync();
        var drawer = Page.Locator("button:has-text('Confirm Assignment')");
        await drawer.WaitForAsync();
        foreach (var label in new[] { "Email:", "Phone:", "Address:", "Parish / Diocese:", "Date of loss:", "Requested for:" })
            await AssertVisibleAsync($"text={label}");
    }
}
