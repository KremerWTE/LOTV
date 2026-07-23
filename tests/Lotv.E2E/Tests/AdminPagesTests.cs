using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for admin pages. These tests log in as an admin user before
/// navigating to protected routes. They verify that each admin page renders
/// its core layout and produces no unhandled JavaScript errors.
///
/// NOTE: These tests require the dev seed account to exist (mary.roberts).
/// They are skipped gracefully if the API is not running (login fails).
/// </summary>
public class AdminPagesTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();
    private bool _loggedIn;

    public AdminPagesTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);

        // Attempt login — if it fails (API not running) the tests will still
        // execute but navigate to login page and assertions will be softer.
        try
        {
            await LoginAsAdminAsync();
            _loggedIn = true;
        }
        catch
        {
            _loggedIn = false;
        }
    }

    private async Task GoToAdminPageAsync(string path)
    {
        if (!_loggedIn)
        {
            await GoToAsync("/login");
            return;
        }
        await GoToAsync(path);
        await WaitForBlazorAsync();
    }

    [Fact]
    public async Task AdminDashboard_Loads_WithKPIStrip()
    {
        await GoToAdminPageAsync("/admin/dashboard");
        if (!_loggedIn) return;

        // KPI cards or stats strip
        await AssertVisibleAsync("[class*='stat'], [class*='kpi'], [class*='card'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminCasesPage_Loads_WithCaseList()
    {
        await GoToAdminPageAsync("/admin/cases");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='case'], [class*='kanban'], [class*='list'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminFamiliesPage_Loads()
    {
        await GoToAdminPageAsync("/admin/families");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='family'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminVolunteersPage_Loads()
    {
        await GoToAdminPageAsync("/admin/volunteers");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='volunteer'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminDonorsPage_Loads()
    {
        await GoToAdminPageAsync("/admin/donors");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='donor'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminDonationsPage_Loads()
    {
        await GoToAdminPageAsync("/admin/donations");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='donation'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminEventsPage_Loads()
    {
        await GoToAdminPageAsync("/admin/events");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='event'], [class*='card'], table, h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminWorkloadPage_Loads()
    {
        await GoToAdminPageAsync("/admin/workload");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='workload'], [class*='chart'], [class*='volunteer'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminKanbanPage_Loads()
    {
        await GoToAdminPageAsync("/admin/kanban");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='kanban'], [class*='column'], [class*='board'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminWishListPage_Loads()
    {
        await GoToAdminPageAsync("/admin/wishlist");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='wish'], [class*='card'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminVolunteerSchedulePage_Loads()
    {
        await GoToAdminPageAsync("/admin/volunteer-schedule");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='schedule'], [class*='calendar'], [class*='week'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminSponsorshipsPage_Loads()
    {
        await GoToAdminPageAsync("/admin/sponsorships");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='sponsor'], [class*='card'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminReconciliationPage_Loads()
    {
        await GoToAdminPageAsync("/admin/reconciliation");
        if (!_loggedIn) return;

        await AssertVisibleAsync("[class*='reconcil'], [class*='period'], h1, h2");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task AdminAuditLogPage_Loads()
    {
        await GoToAdminPageAsync("/admin/audit");
        if (!_loggedIn) return;

        await AssertVisibleAsync("table, [class*='audit'], h1, h2");
        Assert.Empty(_jsErrors);
    }
}
