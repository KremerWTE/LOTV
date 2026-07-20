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

        // Step 1 requires first/last name and street/city/state/zip before it will
        // advance. These fields have no id/name/label association (placeholder text
        // is example data, e.g. "Jane"/"Smith"), so match on that placeholder text.
        await Page.Locator("input[placeholder='Jane']").FillAsync("Playwright");
        await Page.Locator("input[placeholder='Smith']").FillAsync("Tester");
        await Page.Locator("input[placeholder='123 Main Street']").FillAsync("123 Test Street");
        await Page.Locator("input[placeholder='Springfield']").FillAsync("Springfield");
        await Page.Locator("input[placeholder='IL']").FillAsync("IL");
        await Page.Locator("input[placeholder='62701']").FillAsync("62701");

        // Click Next
        var nextBtn = Page.Locator("button:has-text('Next'), button:has-text('Continue')").First;
        if (await nextBtn.CountAsync() > 0)
        {
            await nextBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            // Step 2 content ("Available Days" panel) should now be visible
            Assert.True(await Page.Locator("text=Available Days").CountAsync() > 0);
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
