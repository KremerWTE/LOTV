using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// End-to-end: staff change the public prayer care form from the dashboard
/// (Operations &amp; Content &gt; Intake Form Editor) and visitors see the change.
/// Needs the API and Web apps running (see tests/Lotv.E2E/README.md); each test
/// resets the form to its default afterwards so it leaves nothing behind.
/// </summary>
public class FormEditorTests : E2ETestBase
{
    private const string EditorPath = "/admin/forms/prayer-care-intake";
    private readonly List<string> _jsErrors = new();

    public FormEditorTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
        // The editor's "Reset to default" uses a browser confirm() — accept it.
        Page.Dialog += async (_, d) => await d.AcceptAsync();
    }

    private async Task OpenEditorAsync()
    {
        await LoginAsAdminAsync();
        await GoToAsync(EditorPath);
        await WaitForBlazorAsync();
        await Page.Locator("text=Wording").First.WaitForAsync();
    }

    private ILocator Input(string label) =>
        Page.Locator($"xpath=//label[normalize-space()='{label}']/following-sibling::input").First;

    private async Task SaveAsync()
    {
        await Page.Locator("button:has-text('Save changes')").First.ClickAsync();
        await Page.Locator("text=Saved — the public form is updated").WaitForAsync();
    }

    private async Task ResetAsync()
    {
        await GoToAsync(EditorPath);
        await WaitForBlazorAsync();
        var reset = Page.Locator("button:has-text('Reset to default')").First;
        await reset.WaitForAsync();
        if (await reset.IsEnabledAsync())
        {
            await reset.ClickAsync();
            await Page.Locator("text=Reset to the built-in default").WaitForAsync();
        }
    }

    private async Task<IPage> OpenPublicFormAsync()
    {
        var pub = await Context.NewPageAsync();
        await pub.GotoAsync(E2ESettings.BaseUrl.TrimEnd('/') + "/prayer-care-intake.html");
        await pub.Locator("#lotv-intake-form").WaitForAsync();
        return pub;
    }

    [Fact]
    public async Task Sidebar_HasLinksToTheEditorAndTheLivePublicForm()
    {
        await LoginAsAdminAsync();
        await WaitForBlazorAsync();

        await AssertVisibleAsync("a.sidebar-link[href='/admin/forms/prayer-care-intake']");
        await AssertVisibleAsync("a.sidebar-link[href='/prayer-care-intake.html']");
    }

    [Fact]
    public async Task Staff_CanRewordTheForm_AndTheChangeGoesLive()
    {
        await OpenEditorAsync();
        try
        {
            await Input("Form title").FillAsync("E2E: Ask for a Comfort Package");
            await Input("Form title").PressAsync("Tab");
            await SaveAsync();

            var pub = await OpenPublicFormAsync();
            Assert.Equal("E2E: Ask for a Comfort Package", await pub.Locator("#lotv-intake-form h2").TextContentAsync());
        }
        finally { await ResetAsync(); }

        var after = await OpenPublicFormAsync();
        Assert.Equal("Request a Prayer Care Package", await after.Locator("#lotv-intake-form h2").TextContentAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task Staff_CanAddARequiredQuestion_AndVisitorsMustAnswerIt()
    {
        await OpenEditorAsync();
        try
        {
            await Page.Locator("button:has-text('+ Add a new question')").ClickAsync();

            var card = Page.Locator("[data-field-id='custom-1']");
            await card.WaitForAsync();
            await card.Locator("xpath=.//label[normalize-space()='Question']/following-sibling::input").FillAsync("What is the best time to reach you?");
            await card.Locator("xpath=.//label[normalize-space()='Question']/following-sibling::input").PressAsync("Tab");
            await card.Locator("label:has-text('Required') input").CheckAsync();
            await SaveAsync();

            var pub = await OpenPublicFormAsync();
            Assert.Contains("What is the best time to reach you?", await pub.Locator("#lotv-item-custom-1 label").TextContentAsync());

            // Submitting without answering it is refused, naming the question
            await pub.Locator("#lotv-submit-btn").ClickAsync();
            var error = await pub.Locator("#lotv-error").TextContentAsync();
            Assert.Contains("What is the best time to reach you?", error);
        }
        finally { await ResetAsync(); }

        var after = await OpenPublicFormAsync();
        Assert.Equal(0, await after.Locator("#lotv-item-custom-1").CountAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task Staff_CanChangeAShowHideRule_AndTheFormFollowsIt()
    {
        await OpenEditorAsync();
        try
        {
            // Grief support currently shows for Stillbirth / Infant Loss. Add "Infertility" to its rule.
            var card = Page.Locator("[data-field-id='grief-support']");
            await card.Locator("button:has-text('Edit')").ClickAsync();
            await card.Locator("label:has-text('Infertility') input").CheckAsync();
            await SaveAsync();

            var pub = await OpenPublicFormAsync();
            await pub.SelectOptionAsync("#lotv-reason", "Infertility");
            Assert.True(await pub.Locator("#lotv-grief-support").IsVisibleAsync());
            await pub.SelectOptionAsync("#lotv-reason", "Miscarriage");
            Assert.False(await pub.Locator("#lotv-grief-support").IsVisibleAsync());
        }
        finally { await ResetAsync(); }

        Assert.Empty(_jsErrors);
    }
}
