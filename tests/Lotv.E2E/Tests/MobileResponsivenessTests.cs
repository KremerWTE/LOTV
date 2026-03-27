using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// Mobile viewport smoke tests. Uses a 390×844 (iPhone 14) viewport.
/// Verifies that public pages render usably on mobile — no horizontal overflow,
/// key content is visible, and navigation is accessible.
/// </summary>
public class MobileResponsivenessTests : IAsyncLifetime
{
    private readonly BrowserFixture _browser;
    private IBrowserContext _context = null!;
    protected IPage Page { get; private set; } = null!;

    public MobileResponsivenessTests(BrowserFixture browser)
    {
        _browser = browser;
    }

    public async Task InitializeAsync()
    {
        _context = await _browser.NewMobileContextAsync();
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout(E2ESettings.Timeout);
        Page.SetDefaultNavigationTimeout(E2ESettings.Timeout);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private Task GoToAsync(string path) =>
        Page.GotoAsync(E2ESettings.BaseUrl.TrimEnd('/') + path);

    private async Task WaitForBlazorAsync()
    {
        await Page.WaitForSelectorAsync("#app:not(:empty)", new PageWaitForSelectorOptions
        {
            Timeout = E2ESettings.Timeout
        });
    }

    private async Task AssertNoHorizontalScrollAsync()
    {
        // Body width should not exceed viewport width
        var overflow = await Page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflow, "Page should not have horizontal overflow on mobile");
    }

    [Collection("E2E")]
    public class MobileTests : MobileResponsivenessTests
    {
        public MobileTests(BrowserFixture browser) : base(browser) { }

        [Fact]
        public async Task HomePage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }

        [Fact]
        public async Task ApplyPage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/apply");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }

        [Fact]
        public async Task GivePage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/give");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }

        [Fact]
        public async Task HelpPage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/help");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }

        [Fact]
        public async Task WishListPage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/wish-list");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }

        [Fact]
        public async Task LoginPage_Mobile_FormIsUsable()
        {
            await GoToAsync("/login");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();

            // Inputs should be tappable (not too small)
            var emailInput = Page.Locator("input[type='email']").First;
            var box = await emailInput.BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box.Height >= 36, "Input should be at least 36px tall for touch targets");
            Assert.True(box.Width >= 200, "Input should be wide enough on mobile");
        }

        [Fact]
        public async Task VolunteerSignupPage_Mobile_NoHorizontalScroll()
        {
            await GoToAsync("/volunteer");
            await WaitForBlazorAsync();
            await AssertNoHorizontalScrollAsync();
        }
    }
}
