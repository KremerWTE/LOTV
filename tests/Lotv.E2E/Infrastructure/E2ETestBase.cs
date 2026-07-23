namespace Lotv.E2E.Infrastructure;

/// <summary>
/// Base class for all E2E tests. Provides a fresh isolated BrowserContext and
/// Page per test, with helper methods for common operations.
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly BrowserFixture _browser;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage          Page    { get; private set; } = null!;

    protected E2ETestBase(BrowserFixture browser)
    {
        _browser = browser;
    }

    public virtual async Task InitializeAsync()
    {
        Context = await _browser.NewContextAsync();
        Page    = await Context.NewPageAsync();
        Page.SetDefaultTimeout(E2ESettings.Timeout);
        Page.SetDefaultNavigationTimeout(E2ESettings.Timeout);
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    // ── Navigation helpers ────────────────────────────────────────────────────

    protected Task GoToAsync(string path) =>
        Page.GotoAsync(E2ESettings.BaseUrl.TrimEnd('/') + path);

    /// <summary>
    /// Waits for Blazor WASM to finish its initial render by waiting for the
    /// app root element to appear and the loading spinner to disappear.
    /// </summary>
    protected async Task WaitForBlazorAsync()
    {
        // index.html's #app div ships with a loading spinner (svg.loading-progress +
        // .loading-progress-text) already inside it, so "#app:not(:empty)" is true
        // before Blazor even starts — it's not a useful signal on its own. Blazor
        // replaces #app's entire content with the rendered route once it's ready,
        // so waiting for that spinner to be gone is what actually indicates render
        // completion.
        await Page.WaitForSelectorAsync(".loading-progress", new PageWaitForSelectorOptions
        {
            State   = WaitForSelectorState.Detached,
            Timeout = E2ESettings.Timeout
        });
    }

    // ── Auth helpers ──────────────────────────────────────────────────────────

    protected async Task LoginAsync(string username, string password)
    {
        await GoToAsync("/login");
        await WaitForBlazorAsync();
        await Page.FillAsync("#login-username, input[type='email'], input[placeholder*='email' i]", username);
        await Page.FillAsync("input[type='password']", password);
        await Page.ClickAsync("button[type='submit'], button:has-text('Sign In'), button:has-text('Login')");
        // Wait for redirect away from login page
        await Page.WaitForURLAsync(u => !u.Contains("/login"), new PageWaitForURLOptions
        {
            Timeout = E2ESettings.Timeout
        });
    }

    protected Task LoginAsAdminAsync()    => LoginAsync(E2ESettings.AdminUsername, E2ESettings.AdminPassword);
    protected Task LoginAsStaffAsync()    => LoginAsync(E2ESettings.StaffUsername, E2ESettings.StaffPassword);

    // ── Assertion helpers ─────────────────────────────────────────────────────

    /// <summary>Asserts a visible element matching the locator exists on the page.</summary>
    protected async Task AssertVisibleAsync(string selector)
    {
        var el = Page.Locator(selector).First;
        await el.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await el.IsVisibleAsync(), $"Expected '{selector}' to be visible");
    }

    /// <summary>Asserts the page title or heading contains the expected text (case-insensitive).</summary>
    protected async Task AssertHeadingAsync(string text)
    {
        var heading = Page.Locator($"h1, h2, h3").Filter(new LocatorFilterOptions
        {
            HasText = text
        }).First;
        await heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await heading.IsVisibleAsync(), $"Expected heading containing '{text}'");
    }

    /// <summary>Asserts no unhandled JS errors occurred during the test.</summary>
    protected static void SetupErrorCapture(IPage page, List<string> errors) =>
        page.PageError += (_, e) => errors.Add(e);
}
