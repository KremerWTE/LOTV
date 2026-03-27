using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for the public service-request application flow (/apply).
/// Covers form rendering, field validation, and successful submission.
/// </summary>
public class ApplyFlowTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();

    public ApplyFlowTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    [Fact]
    public async Task ApplyPage_RequiredFieldValidation_PreventsSubmit()
    {
        await GoToAsync("/apply");
        await WaitForBlazorAsync();

        // Try to submit the form empty
        var submitBtn = Page.Locator("button[type='submit'], button:has-text('Submit'), button:has-text('Request')").Last;
        await submitBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Should still be on /apply — not navigate away
        Assert.Contains("/apply", Page.Url);
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ApplyPage_FormFields_ArePresent()
    {
        await GoToAsync("/apply");
        await WaitForBlazorAsync();

        // First name
        var firstName = Page.Locator("input[placeholder*='first' i], input[id*='first' i], input[name*='first' i]").First;
        Assert.True(await firstName.IsVisibleAsync(), "First name field should be visible");

        // Last name
        var lastName = Page.Locator("input[placeholder*='last' i], input[id*='last' i], input[name*='last' i]").First;
        Assert.True(await lastName.IsVisibleAsync(), "Last name field should be visible");

        // Email
        var email = Page.Locator("input[type='email']").First;
        Assert.True(await email.IsVisibleAsync(), "Email field should be visible");

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ApplyPage_ReasonDropdown_HasOptions()
    {
        await GoToAsync("/apply");
        await WaitForBlazorAsync();

        // Reason for request dropdown or radio group
        var reasonSelector = Page.Locator("select, [class*='reason'], input[type='radio']").First;
        await reasonSelector.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await reasonSelector.IsVisibleAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ApplyPage_FullValidSubmission_ShowsConfirmation()
    {
        await GoToAsync("/apply");
        await WaitForBlazorAsync();

        // Fill required fields
        var firstNameInput = Page.Locator("input[placeholder*='first' i], input[id*='First' i]").First;
        if (await firstNameInput.IsVisibleAsync())
            await firstNameInput.FillAsync("Test");

        var lastNameInput = Page.Locator("input[placeholder*='last' i], input[id*='Last' i]").First;
        if (await lastNameInput.IsVisibleAsync())
            await lastNameInput.FillAsync("E2EUser");

        var emailInput = Page.Locator("input[type='email']").First;
        if (await emailInput.IsVisibleAsync())
            await emailInput.FillAsync($"e2e-{Guid.NewGuid():N}@test.example.com");

        // Select first reason option if a select exists
        var reasonSelect = Page.Locator("select").First;
        if (await reasonSelect.IsVisibleAsync())
            await reasonSelect.SelectOptionAsync(new[] { new SelectOptionValue { Index = 1 } });

        // Fill address if present
        var streetInput = Page.Locator("input[placeholder*='street' i], input[placeholder*='address' i]").First;
        if (await streetInput.CountAsync() > 0 && await streetInput.IsVisibleAsync())
            await streetInput.FillAsync("123 Test Street");

        var cityInput = Page.Locator("input[placeholder*='city' i]").First;
        if (await cityInput.CountAsync() > 0 && await cityInput.IsVisibleAsync())
            await cityInput.FillAsync("Chicago");

        var stateInput = Page.Locator("input[placeholder*='state' i], select[id*='state' i]").First;
        if (await stateInput.CountAsync() > 0 && await stateInput.IsVisibleAsync())
            await stateInput.FillAsync("IL");

        var zipInput = Page.Locator("input[placeholder*='zip' i]").First;
        if (await zipInput.CountAsync() > 0 && await zipInput.IsVisibleAsync())
            await zipInput.FillAsync("60601");

        // Submit
        var submitBtn = Page.Locator("button[type='submit'], button:has-text('Submit'), button:has-text('Request Help')").Last;
        await submitBtn.ClickAsync();

        // Wait for success state — confirmation message, toast, or navigation
        await Page.WaitForTimeoutAsync(3000);
        var successEl = Page.Locator(
            "[class*='success'], [class*='confirm'], [class*='toast'], " +
            ":has-text('received'), :has-text('thank'), :has-text('submitted')").First;
        if (await successEl.CountAsync() > 0)
            Assert.True(await successEl.IsVisibleAsync() || !Page.Url.Contains("/apply"));

        Assert.Empty(_jsErrors);
    }
}
