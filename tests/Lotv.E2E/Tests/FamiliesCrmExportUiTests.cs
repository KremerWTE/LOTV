using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// Export Data page: the "Families for GiveButter / CRM" button downloads a CSV with the
/// requested headings. Needs the API and Web apps running with the dev seed data.
/// </summary>
public class FamiliesCrmExportUiTests : E2ETestBase
{
    private const string Header =
        "Family name,Moms first name,Moms last name,Email address,Phone number,Street address,City,State,Zip,Country";

    protected override bool AcceptDownloads => true;

    public FamiliesCrmExportUiTests(BrowserFixture browser) : base(browser) { }

    [Fact]
    public async Task ExportPage_DownloadsTheFamiliesCsv_WithTheRequestedHeadings()
    {
        await LoginAsAdminAsync();
        await GoToAsync("/admin/export");
        await WaitForBlazorAsync();
        await AssertVisibleAsync("text=Families for GiveButter / CRM");

        var download = await Page.RunAndWaitForDownloadAsync(() =>
            Page.Locator("button:has-text('Download families CSV')").ClickAsync());

        Assert.StartsWith("lotv-families-", download.SuggestedFilename);
        Assert.EndsWith(".csv", download.SuggestedFilename);

        var path = await download.PathAsync();
        var text = (await File.ReadAllTextAsync(path!)).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(Header, lines[0]);
        Assert.True(lines.Length > 1, "expected at least one family row");
        Assert.All(lines.Skip(1), l => Assert.EndsWith(",United States", l));
    }
}
