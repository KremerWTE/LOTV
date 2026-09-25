using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// The left nav has three groups: Overview (just the two dashboards), Prayer Request Package
/// (the day-to-day request tools) and System Admin (everything else, under sub-headings).
/// </summary>
public class SidebarLayoutTests : E2ETestBase
{
    public SidebarLayoutTests(BrowserFixture browser) : base(browser) { }

    /// <summary>Link names (icons and count badges stripped) of a top-level group, in order.</summary>
    private static async Task<string[]> GroupLinksAsync(ILocator sidebar, string label) =>
        (await sidebar.EvaluateAsync<string[]>(@"(el, label) => {
            const out = [];
            for (const s of el.querySelectorAll(':scope > .sidebar-section')) {
                const l = s.querySelector(':scope > .sidebar-section-label');
                if (!l || l.textContent.trim() !== label) continue;
                s.querySelectorAll('.sidebar-link').forEach(a => {
                    const c = a.cloneNode(true);
                    c.querySelectorAll('.icon, .sidebar-badge').forEach(n => n.remove());
                    out.push(c.textContent.replace(/\s+/g, ' ').replace('↗', '').trim());
                });
            }
            return out;
        }", label))!;

    private static async Task<string[]> TopLevelLabelsAsync(ILocator sidebar) =>
        (await sidebar.EvaluateAsync<string[]>(
            "el => [...el.querySelectorAll(':scope > .sidebar-section > .sidebar-section-label')].map(l => l.textContent.trim())"))!;

    private async Task<ILocator> OpenSidebarAsync()
    {
        await LoginAsAdminAsync();
        await WaitForBlazorAsync();
        var sidebar = Page.Locator("aside.sidebar");
        await sidebar.Locator(".sidebar-section-label:text-is('System Admin')").WaitForAsync();
        return sidebar;
    }

    [Fact]
    public async Task ThereAreOnlyThreeGroups_InOrder()
    {
        var sidebar = await OpenSidebarAsync();

        Assert.Equal(new[] { "Overview", "Prayer Request Package", "System Admin" }, await TopLevelLabelsAsync(sidebar));
    }

    [Fact]
    public async Task Overview_HasOnlyTheTwoDashboards()
    {
        var sidebar = await OpenSidebarAsync();

        Assert.Equal(new[] { "Chapter Dashboard", "HQ Dashboard" }, await GroupLinksAsync(sidebar, "Overview"));
    }

    [Fact]
    public async Task PrayerRequestPackage_HoldsTheRequestTools()
    {
        var sidebar = await OpenSidebarAsync();

        Assert.Equal(new[]
        {
            "Package Pipeline", "Possible Duplicates", "Cases", "Unassigned Queue",
            "My Work Queue", "Request Form", "Edit Request Form",
        }, await GroupLinksAsync(sidebar, "Prayer Request Package"));
    }

    [Fact]
    public async Task SystemAdmin_HoldsEverythingElse_UnderSubHeadings()
    {
        var sidebar = await OpenSidebarAsync();

        var subs = await sidebar.EvaluateAsync<string[]>(
            "el => [...el.querySelectorAll('.sidebar-sublabel')].map(l => l.textContent.trim())");
        Assert.Equal(new[] { "Families & Cases", "Donations", "Programs", "Retreats", "Reports", "Operations & Content" }, subs);

        var links = await GroupLinksAsync(sidebar, "System Admin");
        foreach (var expected in new[]
        {
            "System Admin", "Families", "By State", "By Status", "New Case", "Workload View", "HQ Operations Board",
            "All Donations", "Volunteers", "Retreat Manager", "Reports", "Communications",
        })
            Assert.Contains(expected, links);
    }

    [Fact]
    public async Task PromotedLinks_GoToTheRightPages()
    {
        var sidebar = await OpenSidebarAsync();

        foreach (var path in new[] { "/admin/kanban", "/admin/queue", "/admin/my-queue", "/admin/cases-hub", "/admin/families" })
        {
            await sidebar.Locator($"a.sidebar-link[href='{path}']").First.ClickAsync();
            await Page.WaitForURLAsync(u => u.EndsWith(path));
            await WaitForBlazorAsync();
        }
    }
}
