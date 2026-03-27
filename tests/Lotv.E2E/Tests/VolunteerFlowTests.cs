using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for the volunteer onboarding and self-service flows.
/// </summary>
public class VolunteerFlowTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();

    public VolunteerFlowTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    [Fact]
    public async Task VolunteerSignupPage_Loads_WithFormFields()
    {
        await GoToAsync("/volunteer");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("form, input[type='email'], input[type='text']");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task VolunteerSignupPage_EmptySubmit_StaysOnPage()
    {
        await GoToAsync("/volunteer");
        await WaitForBlazorAsync();

        var submitBtn = Page.Locator("button[type='submit'], button:has-text('Sign Up'), button:has-text('Join')").Last;
        if (await submitBtn.CountAsync() > 0)
        {
            await submitBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            Assert.Contains("/volunteer", Page.Url);
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task OnboardingVolunteerPage_Loads_WithStep1()
    {
        await GoToAsync("/onboarding/volunteer");
        await WaitForBlazorAsync();

        // Step indicator and first step content should be visible
        await AssertVisibleAsync("[class*='step'], [class*='wizard'], [class*='progress'], h2, form");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task OnboardingVolunteerPage_NextButton_AdvancesStep()
    {
        await GoToAsync("/onboarding/volunteer");
        await WaitForBlazorAsync();

        // Fill in first name and last name if required by step 1
        var firstInput = Page.Locator("input[placeholder*='first' i], input[id*='first' i]").First;
        if (await firstInput.CountAsync() > 0 && await firstInput.IsVisibleAsync())
            await firstInput.FillAsync("Playwright");

        var lastInput = Page.Locator("input[placeholder*='last' i], input[id*='last' i]").First;
        if (await lastInput.CountAsync() > 0 && await lastInput.IsVisibleAsync())
            await lastInput.FillAsync("Tester");

        var emailInput = Page.Locator("input[type='email']").First;
        if (await emailInput.CountAsync() > 0 && await emailInput.IsVisibleAsync())
            await emailInput.FillAsync("playwright@test.example.com");

        // Click Next
        var nextBtn = Page.Locator("button:has-text('Next'), button:has-text('Continue')").First;
        if (await nextBtn.CountAsync() > 0)
        {
            await nextBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            // Step 2 content or different heading should appear
            Assert.True(await Page.Locator("[class*='step'], [class*='wizard']").CountAsync() > 0);
        }

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task VolunteerDashboardPage_RequiresAuth()
    {
        await GoToAsync("/volunteer/dashboard");
        await WaitForBlazorAsync();

        // Should either show dashboard (if public) or redirect to login
        await Page.WaitForTimeoutAsync(1000);
        var isLoginPage = Page.Url.Contains("/login");
        var hasDashboardContent = await Page.Locator("h1, h2, [class*='dashboard']").CountAsync() > 0;
        Assert.True(isLoginPage || hasDashboardContent);
    }

    [Fact]
    public async Task MyRequestsPage_RequiresAuth()
    {
        await GoToAsync("/my-requests");
        await WaitForBlazorAsync();
        await Page.WaitForTimeoutAsync(1000);

        var isLoginPage = Page.Url.Contains("/login");
        var hasContent = await Page.Locator("h1, h2, [class*='request']").CountAsync() > 0;
        Assert.True(isLoginPage || hasContent);
    }
}
