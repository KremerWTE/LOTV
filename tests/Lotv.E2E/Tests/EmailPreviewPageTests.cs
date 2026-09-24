using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>Request Emails page: staff can see every email a request sends, to the family and to the team.</summary>
public class EmailPreviewPageTests : E2ETestBase
{
    public EmailPreviewPageTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task Page_ShowsEveryEmail_GroupedByWhoGetsIt_WithTheTeamRecipients()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/email-previews");
        await WaitForBlazorAsync();
        await Page.Locator("[data-email]").First.WaitForAsync();

        Assert.Equal(13, await Page.Locator("[data-email]").CountAsync());
        await AssertVisibleAsync("text=To the family (or the person who referred them)");
        await AssertVisibleAsync("text=To Whitney and the team");
        await AssertVisibleAsync("text=To the volunteer");
        await AssertVisibleAsync("text=Team emails go to:");
        foreach (var name in new[] { "Package shipped", "Package delivered", "New request", "Package completed", "New assignment", "Assignment removed", "Bereavement follow-ups due", "Please confirm your details" })
            Assert.True(await Page.Locator($"[data-email] h3:text-is('{name}')").CountAsync() >= 1, name);

        // Each email is rendered in its own frame with a subject line
        Assert.Equal(13, await Page.Locator("[data-email] iframe").CountAsync());
        Assert.Contains("Your Prayer Care Package Has Been Delivered", await Page.Locator("[data-email='completed']").InnerTextAsync());
    }

    [Fact]
    public async Task LeftNav_HasARequestEmailsLink()
    {
        await LoginAsAdminAsync();
        await WaitForBlazorAsync();

        await Page.Locator("aside.sidebar a.sidebar-link[href='/admin/email-previews']").ClickAsync();
        await Page.WaitForURLAsync(u => u.EndsWith("/admin/email-previews"));
        await AssertHeadingAsync("Request Emails");
    }
}
