using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>Grief support list, "Email the family", "Create my volunteer record", and the CRM grief column option.</summary>
public class OperationsPagesTests : E2ETestBase
{
    public OperationsPagesTests(BrowserFixture browser) : base(browser) { }

    protected override bool AcceptDownloads => true;

    private async Task<int> ApplyAsync(TestPeople.Couple couple, string reason, string zip, bool? grief = null)
    {
        await using var api = await _browser.Playwright.APIRequest.NewContextAsync(new() { BaseURL = E2ESettings.ApiUrl });
        var resp = await api.PostAsync("/api/v1/public/apply", new()
        {
            DataObject = new
            {
                Family = new
                {
                    Parent1FirstName = couple.Husband, Parent1LastName = couple.Last, Parent2FirstName = couple.Wife, Parent2LastName = couple.Last,
                    Email = $"{couple.Husband}.{couple.Last}.{Guid.NewGuid():N}@example.org".ToLowerInvariant(), Phone = "",
                    StreetAddress = $"{Random.Shared.Next(100, 999)} Willow Way", City = "Evanston", State = "IL",
                    Zip = zip, Reason = reason, ChapterId = 1, GriefSupportRequested = grief,
                },
                ForSelf = true, PackageType = "Comfort",
            }
        });
        Assert.Equal(201, resp.Status);
        return (await resp.JsonAsync())!.Value.GetProperty("requestId").GetInt32();
    }

    [Fact]
    public async Task GriefSupportList_ShowsFamiliesWhoSaidYes_AndDownloadsACsv()
    {
        var couple = TestPeople.NewCouple();
        await ApplyAsync(couple, "Stillbirth", $"6{Random.Shared.Next(1000, 9999)}", grief: true);

        await LoginAsAdminAsync();
        await GoToAsync("/admin/grief-support");
        await WaitForBlazorAsync();
        await AssertHeadingAsync("Grief Support List");

        await Page.Locator("#grief-table tr", new() { HasText = couple.Last }).First.WaitForAsync();

        var download = await Page.RunAndWaitForDownloadAsync(() => Page.Locator("#grief-download").ClickAsync());
        Assert.StartsWith("grief-support-", download.SuggestedFilename);
    }

    [Fact]
    public async Task TheCrmExportPage_OffersTheGriefSupportColumn()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/export");
        await WaitForBlazorAsync();
        await Page.Locator("#crm-grief").WaitForAsync();
    }

    [Fact]
    public async Task EmailTheFamily_SendsFromTheCaseAlert()
    {
        var couple = TestPeople.NewCouple();
        var requestId = await ApplyAsync(couple, "Infertility", zip: "606");   // a wrong zip makes the details look wrong

        await LoginAsAdminAsync();
        await GoToAsync($"/admin/cases/{requestId}");
        await WaitForBlazorAsync();

        await Page.Locator("#email-family-details").ClickAsync();
        var result = Page.Locator("#email-family-result");
        await result.WaitForAsync();
        Assert.Contains("Emailed", await result.InnerTextAsync());
    }

    [Fact]
    public async Task MyWorkQueue_OffersToCreateMyVolunteerRecord_WhenIHaveNone()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/my-queue");
        await WaitForBlazorAsync();
        await Page.Locator(".kpi-grid").WaitForAsync();

        var panel = Page.Locator("#no-volunteer-record");
        if (await panel.CountAsync() > 0)
        {
            await Page.Locator("#create-volunteer").ClickAsync();
            await panel.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
        Assert.Equal(0, await Page.Locator("#no-volunteer-record").CountAsync());
    }
}
