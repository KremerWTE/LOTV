using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for authentication flows: login, logout, protected route redirect,
/// and invalid credential handling.
/// </summary>
public class AuthFlowTests : E2ETestBase
{
    public AuthFlowTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await GoToAsync("/login");
        await WaitForBlazorAsync();

        await Page.FillAsync("#login-username, input[type='email'], input[placeholder*='email' i]", "nobody@example.com");
        await Page.FillAsync("input[type='password']", "WrongPassword!");
        await Page.ClickAsync("button[type='submit'], button:has-text('Sign In'), button:has-text('Login')");

        // Should stay on login page and show an error message
        await Page.WaitForTimeoutAsync(2000);
        var url = Page.Url;
        Assert.Contains("/login", url);

        // Error message or alert should appear
        var errorEl = Page.Locator("[class*='error'], [class*='alert'], [role='alert'], .text-danger").First;
        await errorEl.WaitForAsync(new LocatorWaitForOptions
        {
            State   = WaitForSelectorState.Visible,
            Timeout = 5000
        });
        Assert.True(await errorEl.IsVisibleAsync());
    }

    [Fact]
    public async Task ProtectedAdminRoute_RedirectsToLogin_WhenUnauthenticated()
    {
        await GoToAsync("/admin/dashboard");
        await WaitForBlazorAsync();

        // Should redirect to login
        await Page.WaitForURLAsync(u => u.Contains("/login"),
            new PageWaitForURLOptions { Timeout = E2ESettings.Timeout });
        Assert.Contains("/login", Page.Url);
    }

    [Fact]
    public async Task Login_EmptyFields_PreventSubmit()
    {
        await GoToAsync("/login");
        await WaitForBlazorAsync();

        // Click submit without filling anything
        await Page.ClickAsync("button[type='submit'], button:has-text('Sign In'), button:has-text('Login')");
        await Page.WaitForTimeoutAsync(500);

        // Should still be on login page
        Assert.Contains("/login", Page.Url);
    }

    [Fact]
    public async Task LoginPage_HasExpectedElements()
    {
        await GoToAsync("/login");
        await WaitForBlazorAsync();

        // Username field
        var email = Page.Locator("#login-username, input[type='email'], input[placeholder*='email' i]").First;
        Assert.True(await email.IsVisibleAsync());

        // Password field
        var password = Page.Locator("input[type='password']").First;
        Assert.True(await password.IsVisibleAsync());

        // Submit button
        var submit = Page.Locator("button[type='submit'], button:has-text('Sign In'), button:has-text('Login')").First;
        Assert.True(await submit.IsVisibleAsync());
    }
}
