using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// Smoke checks for the volunteer-training side (events such as trainings/workshops, and the
/// certification tracker) and the work queues, opened the way staff open them.
/// </summary>
public class TrainingAndQueuePagesTests : E2ETestBase
{
    private readonly List<string> _jsErrors = new();

    public TrainingAndQueuePagesTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    [Theory]
    [InlineData("/admin/events", "Ministry Events")]
    [InlineData("/admin/volunteer-certifications", "Volunteer Certifications")]
    [InlineData("/admin/queue", "Unassigned")]
    [InlineData("/admin/mothers-day", "Mother's Day Mailing")]
    [InlineData("/admin/follow-up-trackers", "Follow")]
    public async Task AdminPage_LoadsWithItsHeading_AndNoScriptErrors(string path, string headingContains)
    {
        await LoginAsAdminAsync();
        await GoToAsync(path);
        await WaitForBlazorAsync();

        await AssertHeadingAsync(headingContains);
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task MyWorkQueue_OpensForAStaffMember_FromTheLeftNav()
    {
        await LoginAsStaffAsync();
        await WaitForBlazorAsync();

        await Page.Locator("aside.sidebar a.sidebar-link[href='/admin/my-queue']").ClickAsync();
        await Page.WaitForURLAsync(u => u.EndsWith("/admin/my-queue"));
        await WaitForBlazorAsync();

        await AssertHeadingAsync("Work Queue");
        Assert.Empty(_jsErrors);
    }
}
