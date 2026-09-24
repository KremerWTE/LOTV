using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>A request with a wrong detail shows a call/email alert on the case, a badge on the card and queue, and a filter.</summary>
public class DataQualityAlertTests : E2ETestBase
{
    public DataQualityAlertTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task ARequestWithABadZip_ShowsTheAlert_TheBadge_AndIsFoundByTheFilter()
    {
        var couple = TestPeople.NewCouple();
        await using var api = await _browser.Playwright.APIRequest.NewContextAsync(new() { BaseURL = E2ESettings.ApiUrl });
        var resp = await api.PostAsync("/api/v1/public/apply", new()
        {
            DataObject = new
            {
                Family = new
                {
                    Parent1FirstName = couple.Husband, Parent1LastName = couple.Last, Parent2FirstName = couple.Wife, Parent2LastName = couple.Last,
                    Email = $"{couple.Husband}.{couple.Last}.{Guid.NewGuid():N}@example.org".ToLowerInvariant(), Phone = "",
                    StreetAddress = $"{Random.Shared.Next(100, 999)} Elm Court", City = "Naperville", State = "IL",
                    Zip = "606", Reason = "Infertility", ChapterId = 1,
                },
                ForSelf = true, PackageType = "Comfort",
            }
        });
        Assert.Equal(201, resp.Status);
        var requestId = (await resp.JsonAsync())!.Value.GetProperty("requestId").GetInt32();

        await LoginAsAdminAsync();

        // Case page: the alert says what is wrong and to email (there is an email but no phone).
        await GoToAsync($"/admin/cases/{requestId}");
        await WaitForBlazorAsync();
        var alert = Page.Locator("[data-alert='data-quality']");
        await alert.WaitForAsync();
        var text = await alert.InnerTextAsync();
        Assert.Contains("isn't a valid 5-digit zip", text);
        Assert.Contains("Email the family", text);

        // Queue (this request is auto-assigned, so it is not in it): the filter keeps only rows that carry the badge.
        await GoToAsync("/admin/queue");
        await WaitForBlazorAsync();
        await Page.Locator("#needs-info-filter").ClickAsync();
        Assert.Equal(0, await Page.Locator("table.data-table tbody tr:not(:has([data-needs-info]))").CountAsync());

        // Board: the card is marked, and the filter still shows it.
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        var card = Page.Locator(".kanban-card", new() { HasText = couple.Last });
        await card.First.WaitForAsync();
        Assert.True(await card.First.Locator("[data-needs-info]").CountAsync() > 0);
        await Page.Locator("#needs-info-filter").ClickAsync();
        await card.First.WaitForAsync();
        Assert.Equal(0, await Page.Locator(".kanban-card:not(:has([data-needs-info]))").CountAsync());
    }
}
