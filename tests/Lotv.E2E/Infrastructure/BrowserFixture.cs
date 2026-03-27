namespace Lotv.E2E.Infrastructure;

/// <summary>
/// xUnit collection fixture — creates one Playwright browser instance shared
/// across all E2E tests in the "E2E" collection. Each test gets its own
/// BrowserContext (and therefore isolated cookies/storage).
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser    Browser    { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser    = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = E2ESettings.Headless,
            SlowMo   = E2ESettings.SlowMo,
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    /// <summary>Creates a fresh browser context with sensible defaults.</summary>
    public async Task<IBrowserContext> NewContextAsync(bool acceptDownloads = false)
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL          = E2ESettings.BaseUrl,
            ViewportSize     = new ViewportSize { Width = 1280, Height = 800 },
            AcceptDownloads  = acceptDownloads,
            IgnoreHTTPSErrors= true,
        });
    }

    /// <summary>Creates a fresh context with a mobile viewport.</summary>
    public async Task<IBrowserContext> NewMobileContextAsync()
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL         = E2ESettings.BaseUrl,
            ViewportSize    = new ViewportSize { Width = 390, Height = 844 }, // iPhone 14
            IsMobile        = true,
            HasTouch        = true,
            IgnoreHTTPSErrors = true,
        });
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<BrowserFixture> { }
