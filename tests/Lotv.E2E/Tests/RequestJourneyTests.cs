using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// Follows one real request through the dashboard: submitted on the public form, then found on the
/// Kanban board, in the right queue, on the Mother's Day mailing list and in Bereavement Follow-Up;
/// plus the Mother's Day CSV import. Needs the API and Web apps running with the dev seed data.
/// Each run adds a uniquely named family to the local dev database.
/// </summary>
public class RequestJourneyTests : E2ETestBase
{
    public RequestJourneyTests(BrowserFixture browser) : base(browser) { }

    private async Task SubmitPublicRequestAsync(TestPeople.Couple couple, string tag)
    {
        await Page.GotoAsync(E2ESettings.BaseUrl.TrimEnd('/') + "/request-prayer-care-package");
        await Page.Locator("#lotv-intake-form").WaitForAsync();
        await Page.ClickAsync(".lotv-toggle[data-forwho='me']");
        await Page.FillAsync("#lotv-husband-first", couple.HusbandFull);
        await Page.FillAsync("#lotv-wife-first", couple.WifeFull);
        await Page.FillAsync("#lotv-husband-email", $"jrny-{tag}@example.com");
        await Page.FillAsync("#lotv-wife-email", $"jrny-wife-{tag}@example.com");
        await Page.FillAsync("#lotv-street", $"{Random.Shared.Next(100, 999)} Maple Ridge Ln");
        await Page.FillAsync("#lotv-city", "Chicago");
        await Page.FillAsync("#lotv-state", "IL");
        await Page.FillAsync("#lotv-zip", $"6{Random.Shared.Next(1000, 9999)}");
        await Page.SelectOptionAsync("#lotv-reason", "Stillbirth");
        await Page.FillAsync("#lotv-date-of-loss", DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"));
        await Page.SelectOptionAsync("#lotv-how-heard", "Friend");
        await Page.FillAsync(".lotv-bracelet-initial >> nth=0", "J.J.");
        await Page.PressAsync(".lotv-bracelet-initial >> nth=0", "Tab");
        await Page.ClickAsync("#lotv-submit-btn");
        await Page.Locator("#lotv-confirm").WaitForAsync();
    }

    [Fact]
    public async Task ARequest_ShowsUpOnTheBoard_InTheRightQueue_OnTheMothersDayList_AndInFollowUp()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var couple = TestPeople.NewCouple();
        await SubmitPublicRequestAsync(couple, tag);
        await LoginAsAdminAsync();

        // 1. The Kanban board has the card. It is in "New" only while nobody has been assigned.
        await GoToAsync("/admin/kanban");
        await WaitForBlazorAsync();
        var card = Page.Locator(".kanban-col", new() { Has = Page.Locator($"text={couple.Last}") });
        await card.First.WaitForAsync();
        var column = (await card.First.Locator(".kanban-col-hd span").First.TextContentAsync())!.Trim();

        // 2. Unassigned Queue lists it exactly when it sits in the New column.
        await GoToAsync("/admin/queue");
        await WaitForBlazorAsync();
        await Page.Locator("table, .empty-state").First.WaitForAsync();
        var inUnassignedQueue = await Page.Locator($"table >> text={couple.Last}").CountAsync() > 0;
        Assert.Equal(column.Equals("New", StringComparison.OrdinalIgnoreCase), inUnassignedQueue);

        // 3. Mother's Day list (current cycle): one card, addressed to the mother, at the family's address.
        await GoToAsync("/admin/mothers-day");
        await WaitForBlazorAsync();
        var row = Page.Locator("tr", new() { HasText = couple.WifeFull });
        await row.First.WaitForAsync();
        Assert.Contains("Maple Ridge Ln", await row.First.InnerTextAsync());
        Assert.Equal(0, await Page.Locator("tr", new() { HasText = couple.HusbandFull }).CountAsync());

        // ...and Father's Day list: a separate card, addressed to the father.
        await GoToAsync("/admin/fathers-day");
        await WaitForBlazorAsync();
        var dadRow = Page.Locator("tr", new() { HasText = couple.HusbandFull });
        await dadRow.First.WaitForAsync();
        Assert.Contains("Maple Ridge Ln", await dadRow.First.InnerTextAsync());

        // 4. Bereavement Follow-Up (a stillbirth with a date of loss gets the four touchpoints).
        await GoToAsync("/admin/follow-up-trackers");
        await WaitForBlazorAsync();
        await Page.FillAsync("input[aria-label='Search families']", couple.Last);
        await Page.Locator("tr", new() { HasText = couple.Last }).First.WaitForAsync();
    }

    [Fact]
    public async Task MothersDayPage_ImportsARecipientCsv_WithACheckBeforeSaving()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var mother1 = TestPeople.NewMother();
        var mother2 = TestPeople.NewMother();
        while (mother2 == mother1) mother2 = TestPeople.NewMother();
        var csvPath = Path.Combine(Path.GetTempPath(), $"mothers-day-{tag}.csv");
        await File.WriteAllTextAsync(csvPath,
            "Mother Name,Street Address,Apt,City,State,Zip,Country,Mothers Day Only\n" +
            $"{mother1},1 Import St,,Chicago,IL,60601,United States,\n" +
            $"{mother2},2 Import St,Apt 3,Chicago,IL,60601,United States,yes\n" +
            ",3 Import St,,Chicago,IL,60601,,\n");                       // no mother's name -> reported, not imported
        try
        {
            await LoginAsAdminAsync();
            await GoToAsync("/admin/mothers-day");
            await WaitForBlazorAsync();
            // Import into an older cycle so the current list stays clean.
            var years = await Page.Locator("#md-year option").AllInnerTextsAsync();
            await Page.SelectOptionAsync("#md-year", years.Last());
            await Page.ClickAsync("button:has-text('Import from CSV')");
            await Page.SetInputFilesAsync("#md-import-file", csvPath);
            await Page.Locator("text=Chosen file").WaitForAsync();

            // Check first: nothing saved yet, the bad line is called out
            await Page.ClickAsync("button:has-text('Check file')");
            var result = Page.Locator("#md-import-result");
            await result.WaitForAsync();
            var checkText = await result.InnerTextAsync();
            Assert.Contains("nothing saved yet", checkText);
            Assert.Contains("2 would be added", checkText);
            Assert.Contains("Line 4", checkText);
            Assert.Equal(0, await Page.Locator($"td:has-text('{mother1}')").CountAsync());

            // Then import for real: both good rows land on the list
            await Page.ClickAsync("button:has-text('Import'):not(:has-text('from CSV'))");
            await Page.Locator("#md-import-result:has-text('Import complete')").WaitForAsync();
            await Page.Locator($"tr:has-text('{mother1}')").First.WaitForAsync();
            await Page.Locator($"tr:has-text('{mother2}')").First.WaitForAsync();

            // Importing the same file again adds nothing
            await Page.ClickAsync("button:has-text('Import'):not(:has-text('from CSV'))");
            await Page.Locator("#md-import-result:has-text('0 added')").WaitForAsync();
        }
        finally { File.Delete(csvPath); }
    }

    [Fact]
    public async Task MothersDayPage_HasAYearPicker_DefaultingToTheCurrentCycle()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/mothers-day");
        await WaitForBlazorAsync();

        var selected = await Page.Locator("#md-year").InputValueAsync();
        var mothersDay = new DateTime(DateTime.Today.Year, 5, 1);
        while (mothersDay.DayOfWeek != DayOfWeek.Sunday) mothersDay = mothersDay.AddDays(1);
        mothersDay = mothersDay.AddDays(7);
        var expected = DateTime.Today > mothersDay ? DateTime.Today.Year + 1 : DateTime.Today.Year;
        Assert.Equal(expected.ToString(), selected);
    }
}
