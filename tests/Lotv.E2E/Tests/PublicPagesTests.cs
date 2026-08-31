using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// Smoke tests for all publicly-accessible pages (no login required).
/// Verifies that each page loads, renders a meaningful heading, and produces
/// no unhandled JavaScript errors.
/// </summary>
public class PublicPagesTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();

    public PublicPagesTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    [Fact]
    public async Task HomePage_Loads_WithHeroContent()
    {
        await GoToAsync("/");
        await WaitForBlazorAsync();

        // Hero section or main heading should be present
        var hero = Page.Locator("h1, .hero, [class*='hero']").First;
        await hero.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await hero.IsVisibleAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ApplyPage_Loads_WithRequestForm()
    {
        await GoToAsync("/apply");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("form, [class*='form'], input, select");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task GivePage_Loads_WithDonationOptions()
    {
        await GoToAsync("/give");
        await WaitForBlazorAsync();
        // Should show donation form or amount buttons
        await AssertVisibleAsync("input[type='number'], button[class*='amount'], [class*='donate'], form");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task VolunteerSignupPage_Loads_WithSignupForm()
    {
        await GoToAsync("/volunteer");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("form, input[type='email'], input[type='text']");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task HelpPage_Loads_WithFaqItems()
    {
        await GoToAsync("/help");
        await WaitForBlazorAsync();
        // FAQ items or search should be visible
        await AssertVisibleAsync("input[placeholder*='search' i], [class*='faq'], [class*='accordion'], details, summary");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task WishListPublicPage_Loads_WithItems()
    {
        await GoToAsync("/wish-list");
        await WaitForBlazorAsync();
        // Should show category filter chips or wish list cards
        await AssertVisibleAsync("[class*='chip'], [class*='card'], [class*='wish'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task EventsPage_Loads()
    {
        await GoToAsync("/events");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("h1, h2, [class*='event'], [class*='card']");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task TransparencyPage_Loads()
    {
        await GoToAsync("/transparency");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("h1, h2, [class*='stat'], [class*='impact']");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task LoginPage_Loads_WithEmailAndPasswordFields()
    {
        await GoToAsync("/login");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("#login-username, input[type='email'], input[placeholder*='email' i]");
        await AssertVisibleAsync("input[type='password']");
        await AssertVisibleAsync("button[type='submit'], button:has-text('Sign In'), button:has-text('Login')");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DonorImpactPage_Loads()
    {
        await GoToAsync("/my-impact");
        await WaitForBlazorAsync();
        // Public page (or redirect to login) — just verify no crash
        Assert.Empty(_jsErrors);
    }
}
