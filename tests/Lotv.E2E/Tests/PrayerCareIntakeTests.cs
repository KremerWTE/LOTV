using System.Text.Json;
using System.Text.Json.Nodes;
using Lotv.E2E.Infrastructure;

namespace Lotv.E2E.Tests;

/// <summary>
/// E2E tests for the standalone Duda embed form at
/// docs/duda-embed/prayer-care-intake.html. This page is NOT part of the
/// Blazor app (it's pasted directly into a Duda "Embed Code" widget and
/// posts to Lotv.Api's public /api/v1/public/apply endpoint from wherever
/// it's hosted), so these tests load the file directly via a file:// URL
/// instead of going through E2ETestBase.GoToAsync's Blazor BaseUrl, and
/// intercept the outbound fetch() instead of hitting a real API — loaded
/// from file://, the form's API_BASE_URL resolves to the production host
/// (https://lotv_api.wte.net), so that's the host the tests intercept.
/// </summary>
public class PrayerCareIntakeTests : E2ETestBase
{
    private const string ApiOrigin = "https://lotv_api.wte.net";
    private readonly List<string> _jsErrors = new();

    public PrayerCareIntakeTests(BrowserFixture browser) : base(browser) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        SetupErrorCapture(Page, _jsErrors);
    }

    /// <summary>Walks up from the test binary's output dir to find the repo root.</summary>
    private static string GetFormFileUrl()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "duda-embed", "prayer-care-intake.html")))
            dir = dir.Parent;

        if (dir == null)
            throw new FileNotFoundException("Could not locate docs/duda-embed/prayer-care-intake.html above " + AppContext.BaseDirectory);

        return new Uri(Path.Combine(dir.FullName, "docs", "duda-embed", "prayer-care-intake.html")).AbsoluteUri;
    }

    private static string FindRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray())))
            dir = dir.Parent;
        if (dir == null) throw new FileNotFoundException(string.Join("/", parts));
        return Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
    }

    /// <summary>The built-in default definition the API serves until staff customise the form.</summary>
    private static readonly string DefaultDefinitionJson =
        File.ReadAllText(FindRepoFile("src", "Lotv.Api", "Data", "FormDefaults", "prayer-care-intake.json"));

    /// <summary>Default definition with an edit applied — stands in for "staff changed the form in the dashboard".</summary>
    private static string Edited(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(DefaultDefinitionJson)!;
        mutate(node);
        return node.ToJsonString();
    }

    private static JsonNode FieldById(JsonNode def, string id) =>
        def["fields"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == id)!;

    /// <summary>Opens the form with the API's definition endpoint answered by <paramref name="definitionJson"/> (default when null).</summary>
    private async Task GoToFormAsync(string? definitionJson = null, int status = 200)
    {
        await Page.RouteAsync($"{ApiOrigin}/api/v1/public/forms/prayer-care-intake", route =>
            route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                Body = definitionJson ?? DefaultDefinitionJson,
            }));
        await Page.GotoAsync(GetFormFileUrl());
    }

    /// <summary>Intercepts the form's POST to the API and fulfills it with 200 OK, capturing the JSON body sent.</summary>
    private async Task<JsonElement> InterceptSubmitAsync(Func<Task> triggerSubmit)
    {
        JsonElement? captured = null;
        await Page.RouteAsync($"{ApiOrigin}/api/v1/public/apply", async route =>
        {
            var body = route.Request.PostData ?? "{}";
            captured = JsonDocument.Parse(body).RootElement.Clone();
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"id\":1}"
            });
        });

        await triggerSubmit();
        await Page.WaitForTimeoutAsync(500);

        Assert.True(captured.HasValue, "Expected the form to POST to the API");
        return captured!.Value;
    }

    // ── Fill helpers ─────────────────────────────────────────────────────────

    private async Task FillRequiredFamilyFieldsAsync()
    {
        await Page.FillAsync("#lotv-husband-first", "John Smith");
        await Page.FillAsync("#lotv-wife-first", "Jane Smith");
        await Page.FillAsync("#lotv-husband-email", "john@example.com");
        await Page.FillAsync("#lotv-wife-email", "jane@example.com");
        await Page.FillAsync("#lotv-street", "123 Test Street");
        await Page.FillAsync("#lotv-city", "Chicago");
        await Page.FillAsync("#lotv-state", "IL");
        await Page.FillAsync("#lotv-zip", "60601");
        await Page.SelectOptionAsync("#lotv-how-heard", "Friend");
        await Page.FillAsync(".lotv-bracelet-initial >> nth=0", "E.M.");
    }

    // ── Basic rendering / default branch ────────────────────────────────────

    [Fact]
    public async Task Form_Loads_WithForMeActiveByDefault()
    {
        await GoToFormAsync();

        await AssertVisibleAsync("#lotv-intake-form");
        Assert.True(await Page.Locator(".lotv-toggle[data-forwho='me']").GetAttributeAsync("class") is string cls && cls.Contains("active"));
        Assert.Equal("About You", await Page.Locator("#lotv-family-label").TextContentAsync());

        // "Someone else" only sections should be hidden by default
        foreach (var el in await Page.Locator("[data-someone-only]").AllAsync())
            Assert.False(await el.IsVisibleAsync());

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ForSomeoneElseToggle_RevealsRequesterAndMentionFields()
    {
        await GoToFormAsync();

        await Page.ClickAsync(".lotv-toggle[data-forwho='someone']");

        Assert.Equal("About the Recipient", await Page.Locator("#lotv-family-label").TextContentAsync());
        await AssertVisibleAsync("#lotv-req-first");
        await AssertVisibleAsync("#lotv-req-last");
        await AssertVisibleAsync("#lotv-req-email");
        await AssertVisibleAsync("#lotv-mention");

        Assert.Empty(_jsErrors);
    }

    // ── Reason-driven reveals, incl. the grief-support >20wk/infant-loss gate ─

    [Theory]
    [InlineData("Infertility", false, false, true)]
    [InlineData("PrenatalDiagnosis", false, false, true)]
    [InlineData("Miscarriage", true, false, true)]
    [InlineData("Stillbirth", true, true, true)]
    [InlineData("InfantLoss", true, true, true)]
    [InlineData("PostnatalMedical", false, false, true)]
    public async Task ReasonSelection_RevealsExpectedSections(
        string reason, bool expectDateOfLoss, bool expectGriefSupport, bool expectFaithTradition)
    {
        await GoToFormAsync();

        await Page.SelectOptionAsync("#lotv-reason", reason);

        Assert.Equal(expectDateOfLoss, await Page.Locator("#lotv-date-of-loss").IsVisibleAsync());
        Assert.Equal(expectGriefSupport, await Page.Locator("#lotv-grief-support").IsVisibleAsync());
        Assert.Equal(expectFaithTradition, await Page.Locator("#lotv-faith").IsVisibleAsync());

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task FaithTradition_Catholic_RevealsDioceseAndParish()
    {
        await GoToFormAsync();

        await Page.SelectOptionAsync("#lotv-reason", "Infertility");
        await Page.SelectOptionAsync("#lotv-faith", "Catholic");

        await AssertVisibleAsync("#lotv-diocese");
        await AssertVisibleAsync("#lotv-parish");

        await Page.SelectOptionAsync("#lotv-faith", "Christian");
        Assert.False(await Page.Locator("#lotv-diocese").IsVisibleAsync());
        Assert.False(await Page.Locator("#lotv-parish").IsVisibleAsync());

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task HowHeard_Other_RevealsFreeTextField()
    {
        await GoToFormAsync();

        await Page.SelectOptionAsync("#lotv-how-heard", "Other");
        await AssertVisibleAsync("#lotv-how-heard-other");

        Assert.Empty(_jsErrors);
    }

    // ── Bracelet rows ────────────────────────────────────────────────────────

    [Fact]
    public async Task BraceletRows_AddAndRemove_KeepsAtLeastOneRow()
    {
        await GoToFormAsync();

        Assert.Equal(1, await Page.Locator(".lotv-bracelet-row").CountAsync());

        await Page.ClickAsync("#lotv-bracelet-add");
        Assert.Equal(2, await Page.Locator(".lotv-bracelet-row").CountAsync());

        // With 2 rows, remove buttons should be enabled
        Assert.False(await Page.Locator(".lotv-bracelet-remove").First.IsDisabledAsync());

        await Page.Locator(".lotv-bracelet-remove").First.ClickAsync();
        Assert.Equal(1, await Page.Locator(".lotv-bracelet-row").CountAsync());

        // Down to 1 row, the remaining remove button should be disabled
        Assert.True(await Page.Locator(".lotv-bracelet-remove").First.IsDisabledAsync());

        Assert.Empty(_jsErrors);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptySubmit_ShowsValidationError_AndDoesNotPost()
    {
        await GoToFormAsync();

        var posted = false;
        await Page.RouteAsync($"{ApiOrigin}/api/v1/public/apply", route =>
        {
            posted = true;
            return route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = "{}" });
        });

        await Page.ClickAsync("#lotv-submit-btn");
        await Page.WaitForTimeoutAsync(300);

        await AssertVisibleAsync("#lotv-error");
        Assert.False(posted, "Form should not POST when required fields are missing");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ForSomeoneElse_MissingRequesterInfo_ShowsValidationError()
    {
        await GoToFormAsync();

        await Page.ClickAsync(".lotv-toggle[data-forwho='someone']");
        await FillRequiredFamilyFieldsAsync();
        await Page.SelectOptionAsync("#lotv-reason", "Infertility");
        // Deliberately leave requester first/last/email blank

        await Page.ClickAsync("#lotv-submit-btn");
        await Page.WaitForTimeoutAsync(300);

        await AssertVisibleAsync("#lotv-error");
        Assert.Empty(_jsErrors);
    }

    // ── Full submission ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForMeSubmission_PostsExpectedPayload_ShowsConfirmation_NoDonationBox()
    {
        await GoToFormAsync();

        await FillRequiredFamilyFieldsAsync();
        await Page.SelectOptionAsync("#lotv-reason", "Infertility");

        var payload = await InterceptSubmitAsync(() => Page.ClickAsync("#lotv-submit-btn"));

        Assert.True(payload.GetProperty("forSelf").GetBoolean());
        Assert.Equal("Comfort", payload.GetProperty("packageType").GetString());
        var family = payload.GetProperty("family");
        Assert.Equal("John", family.GetProperty("parent1FirstName").GetString());
        Assert.Equal("Smith", family.GetProperty("parent1LastName").GetString());
        Assert.Equal("Infertility", family.GetProperty("reason").GetString());
        Assert.Equal("Anonymous", family.GetProperty("privacyPreference").GetString());

        await AssertVisibleAsync("#lotv-confirm");
        Assert.False(await Page.Locator(".lotv-donation").IsVisibleAsync(), "Donation box should stay hidden on the 'For Me' branch");

        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task ForSomeoneElseSubmission_ShowsDonationBox_AndSendsReferrerInfo()
    {
        await GoToFormAsync();

        await Page.ClickAsync(".lotv-toggle[data-forwho='someone']");
        await FillRequiredFamilyFieldsAsync();
        await Page.SelectOptionAsync("#lotv-reason", "Stillbirth");
        await Page.FillAsync("#lotv-req-first", "Ref");
        await Page.FillAsync("#lotv-req-last", "Errer");
        await Page.FillAsync("#lotv-req-email", "referrer@example.com");

        var payload = await InterceptSubmitAsync(() => Page.ClickAsync("#lotv-submit-btn"));

        Assert.False(payload.GetProperty("forSelf").GetBoolean());
        Assert.Equal("Ref", payload.GetProperty("referrerFirstName").GetString());
        Assert.Equal("Errer", payload.GetProperty("referrerLastName").GetString());
        Assert.Equal("referrer@example.com", payload.GetProperty("referrerEmail").GetString());

        await AssertVisibleAsync("#lotv-confirm");
        await AssertVisibleAsync(".lotv-donation");

        Assert.Empty(_jsErrors);
    }

    // ── The form is driven by its definition (what the dashboard editor saves) ─

    [Fact]
    public async Task DefinitionEdit_ChangesTitleAndChoiceWording()
    {
        await GoToFormAsync(Edited(d =>
        {
            d["title"] = "Ask for a Comfort Package";
            FieldById(d, "how-heard")["options"]![0]!["label"] = "A friend told me";
        }));

        Assert.Equal("Ask for a Comfort Package", await Page.Locator("#lotv-intake-form h2").TextContentAsync());
        Assert.Contains("A friend told me", await Page.Locator("#lotv-how-heard").InnerTextAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DefinitionEdit_HiddenQuestion_IsNotRendered()
    {
        await GoToFormAsync(Edited(d => FieldById(d, "wife-phone")["visible"] = false));

        await AssertVisibleAsync("#lotv-husband-phone");
        Assert.Equal(0, await Page.Locator("#lotv-wife-phone").CountAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DefinitionEdit_ShowWhenRule_IsFollowed()
    {
        // Staff move the grief-support question from Stillbirth/InfantLoss to Infertility only.
        await GoToFormAsync(Edited(d =>
        {
            var rule = FieldById(d, "grief-support")["showWhen"]![0]!;
            rule["in"] = new JsonArray("Infertility");
        }));

        await Page.SelectOptionAsync("#lotv-reason", "Infertility");
        Assert.True(await Page.Locator("#lotv-grief-support").IsVisibleAsync());

        await Page.SelectOptionAsync("#lotv-reason", "Stillbirth");
        Assert.False(await Page.Locator("#lotv-grief-support").IsVisibleAsync());
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DefinitionEdit_LabelChangesWithWhoItIsFor()
    {
        await GoToFormAsync();

        Assert.DoesNotContain("recipient", await Page.Locator("#lotv-item-story label").TextContentAsync() ?? "");
        await Page.ClickAsync(".lotv-toggle[data-forwho='someone']");
        Assert.Contains("recipient's story", await Page.Locator("#lotv-item-story label").TextContentAsync() ?? "");
        Assert.Contains("+ Add New", await Page.Locator("#lotv-bracelet-add").TextContentAsync() ?? "");
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task DefinitionEdit_CustomQuestion_IsRequiredAndAnswerGoesToContactNotes()
    {
        var json = Edited(d => d["fields"]!.AsArray().Insert(0, JsonNode.Parse(
            """{ "id":"custom-1","key":"custom1","type":"text","label":"Best time to call?","required":true,"visible":true,"width":"full","standard":false }""")));
        await GoToFormAsync(json);
        await FillRequiredFamilyFieldsAsync();
        await Page.SelectOptionAsync("#lotv-reason", "Infertility");

        // Required custom question blocks submit until answered
        await Page.ClickAsync("#lotv-submit-btn");
        await AssertVisibleAsync("#lotv-error");
        Assert.Contains("Best time to call?", await Page.Locator("#lotv-error").TextContentAsync() ?? "");

        await Page.FillAsync("#lotv-custom-1", "Mornings");
        var payload = await InterceptSubmitAsync(() => Page.ClickAsync("#lotv-submit-btn"));

        var notes = payload.GetProperty("family").GetProperty("contactNotes").GetString();
        Assert.Contains("Best time to call?: Mornings", notes);
        Assert.Empty(_jsErrors);
    }

    [Fact]
    public async Task PostnatalMedical_IsAcceptedReason()
    {
        await GoToFormAsync();
        await FillRequiredFamilyFieldsAsync();
        await Page.SelectOptionAsync("#lotv-reason", "PostnatalMedical");

        var payload = await InterceptSubmitAsync(() => Page.ClickAsync("#lotv-submit-btn"));
        Assert.Equal("PostnatalMedical", payload.GetProperty("family").GetProperty("reason").GetString());
        await AssertVisibleAsync("#lotv-confirm");
    }

    [Fact]
    public async Task DefinitionUnavailable_ShowsFriendlyMessage()
    {
        await GoToFormAsync("{}", status: 503);

        await AssertVisibleAsync("#lotv-load-error");
        Assert.False(await Page.Locator("#lotv-intake-form").IsVisibleAsync());
    }
}
