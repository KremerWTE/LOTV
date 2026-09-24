using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>Assignment rules page, the repeat-request banner, the per-family request list, and the Cases list.</summary>
public class AssignmentWorkflowPagesTests : E2ETestBase
{
    public AssignmentWorkflowPagesTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task AssignmentRules_CanBeCreatedListedAndDeleted()
    {
        var name = $"Losses to a specialist {Guid.NewGuid():N}"[..40];
        await LoginAsAdminAsync();
        await GoToAsync("/admin/assignment-rules");
        await WaitForBlazorAsync();
        await AssertHeadingAsync("Assignment Rules");

        await Page.Locator("#rule-new").ClickAsync();
        await Page.Locator("#rule-form").WaitForAsync();

        // Saving with nothing chosen explains what is missing.
        await Page.FillAsync("#rule-name", name);
        await Page.Locator("#rule-save").ClickAsync();
        await Page.Locator("#rule-form .alert-danger").WaitForAsync();

        await Page.Locator("input[data-reason='Stillbirth']").CheckAsync();
        await Page.Locator("input[data-reason='InfantLoss']").CheckAsync();
        await Page.Locator("input[data-volunteer]").First.CheckAsync();
        await Page.Locator("#rule-save").ClickAsync();

        var row = Page.Locator("tr[data-rule]", new() { HasText = name });
        await row.WaitForAsync();
        var text = await row.InnerTextAsync();
        Assert.Contains("reason is Stillbirth or Infant Loss", text);
        Assert.Contains("On", text);

        await row.Locator("button:has-text('Delete')").ClickAsync();
        await Page.Locator("tr[data-rule]", new() { HasText = name }).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task ApplyingRules_ReportsHowManyWaitingRequestsWereChecked()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/assignment-rules");
        await WaitForBlazorAsync();

        await Page.Locator("#rule-apply").ClickAsync();
        var message = Page.Locator("#rule-message");
        await message.WaitForAsync();
        Assert.Contains("waiting request(s)", await message.InnerTextAsync());
    }

    [Fact]
    public async Task ACaseWhoseFamilyHasAnotherOpenRequest_ShowsTheRepeatBanner_AndOnlyThatFamilysRequests()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/cases/3");        // David & Jenny Park have requests #3 and #10 in the seed data
        await WaitForBlazorAsync();

        var banner = Page.Locator("[data-alert='repeat-request']");
        await banner.WaitForAsync();
        Assert.Contains("#10", await banner.InnerTextAsync());

        var panel = Page.Locator("[data-panel='family-requests']");
        await panel.WaitForAsync();
        Assert.Equal(2, await panel.Locator("div:has(> span > a), div:has(> span > strong)").CountAsync());
    }

    [Fact]
    public async Task TheCasesList_LeavesOutPossibleDuplicates()
    {
        var couple = TestPeople.NewCouple();
        await using var api = await _browser.Playwright.APIRequest.NewContextAsync(new() { BaseURL = E2ESettings.ApiUrl });
        var resp = await api.PostAsync("/api/v1/public/apply", new()
        {
            DataObject = new
            {
                Family = new
                {
                    Parent1FirstName = couple.Husband, Parent1LastName = couple.Last, Parent2FirstName = couple.Wife, Parent2LastName = couple.Last,
                    Email = "d.park@example.com", Phone = "", StreetAddress = "9 Cedar Ln", City = "Chicago", State = "IL",
                    Zip = "60601", Reason = "Infertility", ChapterId = 1,
                },
                ForSelf = true, PackageType = "Comfort",
            }
        });
        Assert.Equal(201, resp.Status);

        await LoginAsAdminAsync();
        await GoToAsync("/admin/cases");
        await WaitForBlazorAsync();
        await Page.Locator("table.data-table").WaitForAsync();
        Assert.Equal(0, await Page.Locator($"table.data-table:has-text('{couple.Last}')").CountAsync());
    }
}
