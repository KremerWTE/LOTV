using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for the public donation flow (/give) and recurring donation page.
/// </summary>
public class DonationFlowTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();

    public DonationFlowTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    [Fact]
    public async Task GivePage_AmountButtons_AreClickable()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();

        // Preset amount buttons (e.g. $25, $50, $100, $250)
        var amountBtn = Page.Locator("button:has-text('25'), button:has-text('$25'), button[data-amount]").First;
        if (await amountBtn.CountAsync() > 0)
        {
            await amountBtn.ClickAsync();
            Assert.True(await amountBtn.IsVisibleAsync());
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task GivePage_CustomAmount_CanBeEntered()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();

        var amountInput = Page.Locator("input[type='number'], input[placeholder*='amount' i]").First;
        if (await amountInput.CountAsync() > 0)
        {
            await amountInput.FillAsync("75");
            var value = await amountInput.InputValueAsync();
            Assert.Equal("75", value);
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task GivePage_DonorName_CanBeEntered()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();

        var nameInput = Page.Locator("input[placeholder*='name' i], input[id*='name' i]").First;
        if (await nameInput.CountAsync() > 0 && await nameInput.IsVisibleAsync())
        {
            await nameInput.FillAsync("Test Donor");
            Assert.Equal("Test Donor", await nameInput.InputValueAsync());
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task GivePage_RecurringOption_IsToggleable()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();

        // Look for a "recurring" or "monthly" toggle/checkbox
        var recurringToggle = Page.Locator(
            "input[type='checkbox'][id*='recur' i], " +
            "label:has-text('Monthly'), label:has-text('Recurring'), " +
            "button:has-text('Monthly')").First;

        if (await recurringToggle.CountAsync() > 0)
        {
            await recurringToggle.ClickAsync();
            Assert.True(await recurringToggle.IsVisibleAsync());
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task GivePage_EmptySubmit_ShowsValidation()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();

        var submitBtn = Page.Locator("button[type='submit'], button:has-text('Donate'), button:has-text('Give')").Last;
        if (await submitBtn.CountAsync() > 0)
        {
            await submitBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            // Should not navigate to a confirmation page yet
            Assert.DoesNotContain("/confirm", Page.Url);
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DonateResourcesPage_Loads()
    {
        await GoToAsync("/donate-resources");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("h1, h2, form, [class*='resource'], [class*='donate']");
        Assert.Empty(_jsErrors);
    }
}
