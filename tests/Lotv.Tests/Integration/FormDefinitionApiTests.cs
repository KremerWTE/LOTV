using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lotv.Api.Data;
using Lotv.Core.Models;

namespace Lotv.Tests.Integration;

/// <summary>
/// The staff-editable public intake form: anyone can read it, only HQAdmin can
/// change it, and a saved definition must never break the public form or the
/// intake endpoint behind it.
/// </summary>
[Collection("Integration")]
public class FormDefinitionApiTests
{
    private const string Key = "prayer-care-intake";
    private readonly LotvApiFactory _factory;

    public FormDefinitionApiTests(LotvApiFactory factory) => _factory = factory;

    // ── Public read ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PublicGet_NoToken_ServesTheDefaultDefinition()
    {
        var resp = await _factory.CreateClient().GetAsync($"/api/v1/public/forms/{Key}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var def = await resp.Content.ReadFromJsonAsync<IntakeFormDefinition>(FormDefinitions.Json);
        Assert.Equal("Request a Prayer Care Package", def!.Title);
        Assert.Contains(def.Fields, f => f.Key == "reason");
    }

    [Fact]
    public async Task PublicGet_UnknownForm_Returns404()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/v1/public/forms/not-a-form");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Who can edit ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminGet_NoToken_Returns401()
    {
        var resp = await _factory.CreateClient().GetAsync($"/api/v1/forms/{Key}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("ChapterStaff")]
    [InlineData("ChapterAdmin")]
    public async Task AdminEndpoints_NonHqAdmin_Return403(string role)
    {
        var client = await AuthedClientAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/forms/{Key}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/v1/forms/{Key}", FormDefinitions.Default(Key), FormDefinitions.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/forms/{Key}/reset", null)).StatusCode);
    }

    // ── Save / read back / reset ──────────────────────────────────────────────

    [Fact]
    public async Task HqAdmin_Save_GoesLiveOnThePublicEndpoint_AndResetRestoresDefault()
    {
        var client = await AuthedClientAsync("HQAdmin");
        try
        {
            var def = FormDefinitions.Default(Key);
            def.Title = "Ask for a Comfort Package";
            def.Fields.First(f => f.Key == "howHeard").Options[0].Label = "A friend told me";
            def.Fields.Add(new IntakeFormField { Id = "custom-1", Key = "custom1", Type = "text", Label = "Best time to call?", Width = "full", Visible = true });

            var save = await client.PutAsJsonAsync($"/api/v1/forms/{Key}", def, FormDefinitions.Json);
            Assert.Equal(HttpStatusCode.OK, save.StatusCode);

            var admin = await (await client.GetAsync($"/api/v1/forms/{Key}")).Content.ReadFromJsonAsync<IntakeFormEnvelope>(FormDefinitions.Json);
            Assert.False(admin!.IsDefault);
            Assert.Equal("Ask for a Comfort Package", admin.Definition.Title);

            // No token: visitors now get the edited form
            var pub = await (await _factory.CreateClient().GetAsync($"/api/v1/public/forms/{Key}"))
                .Content.ReadFromJsonAsync<IntakeFormDefinition>(FormDefinitions.Json);
            Assert.Equal("Ask for a Comfort Package", pub!.Title);
            Assert.Contains(pub.Fields, f => f.Key == "custom1");
            Assert.Equal("A friend told me", pub.Fields.First(f => f.Key == "howHeard").Options[0].Label);
        }
        finally
        {
            var reset = await client.PostAsync($"/api/v1/forms/{Key}/reset", null);
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        var after = await (await _factory.CreateClient().GetAsync($"/api/v1/public/forms/{Key}"))
            .Content.ReadFromJsonAsync<IntakeFormDefinition>(FormDefinitions.Json);
        Assert.Equal("Request a Prayer Care Package", after!.Title);
    }

    // ── Bad edits are refused and never reach the public form ─────────────────

    [Fact]
    public async Task HqAdmin_SaveThatRemovesAStandardQuestion_Returns400_AndPublicFormIsUntouched()
    {
        var client = await AuthedClientAsync("HQAdmin");
        var def = FormDefinitions.Default(Key);
        def.Fields.RemoveAll(f => f.Key == "reason");

        var resp = await client.PutAsJsonAsync($"/api/v1/forms/{Key}", def, FormDefinitions.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var pub = await (await _factory.CreateClient().GetAsync($"/api/v1/public/forms/{Key}"))
            .Content.ReadFromJsonAsync<IntakeFormDefinition>(FormDefinitions.Json);
        Assert.Contains(pub!.Fields, f => f.Key == "reason");
    }

    [Fact]
    public async Task HqAdmin_SaveWithAReasonTheSystemDoesNotKnow_Returns400()
    {
        var client = await AuthedClientAsync("HQAdmin");
        var def = FormDefinitions.Default(Key);
        def.Fields.First(f => f.Key == "reason").Options.Add(new FormOption { Value = "SomethingNew", Label = "Something new" });

        var resp = await client.PutAsJsonAsync($"/api/v1/forms/{Key}", def, FormDefinitions.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task HqAdmin_SaveThatMakesTheContactEmailOptional_Returns400()
    {
        // /api/v1/public/apply refuses a request without a first name, last name and email.
        var client = await AuthedClientAsync("HQAdmin");
        var def = FormDefinitions.Default(Key);
        def.Fields.First(f => f.Key == "husbandEmail").Required = false;

        var resp = await client.PutAsJsonAsync($"/api/v1/forms/{Key}", def, FormDefinitions.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Validation rules (no HTTP) ────────────────────────────────────────────

    [Fact]
    public void DefaultDefinition_IsValid()
    {
        Assert.Empty(FormDefinitions.Default(Key).Validate(FormDefinitions.ReasonValues));
    }

    [Fact]
    public void Validate_RejectsDuplicateIds_SelfReferencingRules_AndUnsafeDonationIds()
    {
        var def = FormDefinitions.Default(Key);
        def.Fields.Add(new IntakeFormField { Id = "custom-1", Key = "custom1", Type = "text", Label = "A", Width = "full", Visible = true });
        def.Fields.Add(new IntakeFormField { Id = "custom-1", Key = "custom2", Type = "text", Label = "B", Width = "full", Visible = true });
        def.Fields.First(f => f.Id == "custom-1").ShowWhen.Add(new FormCondition { Field = "custom1", NotEmpty = true });
        def.Donation.WidgetId = "abc\"><script>";

        var errors = def.Validate(FormDefinitions.ReasonValues);

        Assert.Contains(errors, e => e.Contains("Duplicate item id"));
        Assert.Contains(errors, e => e.Contains("can't depend on itself"));
        Assert.Contains(errors, e => e.Contains("GiveButter widget id"));
    }

    // ── The intake endpoint accepts every reason the form offers ──────────────

    [Theory]
    [InlineData("Infertility")]
    [InlineData("PrenatalDiagnosis")]
    [InlineData("Miscarriage")]
    [InlineData("Stillbirth")]
    [InlineData("InfantLoss")]
    [InlineData("PostnatalMedical")]
    public async Task PublicApply_AcceptsEveryReasonTheDefaultFormOffers(string reason)
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/v1/public/apply", new
        {
            Family = new
            {
                Parent1FirstName = "Test", Parent1LastName = "Family", Email = $"{reason}@test.example.com",
                Reason = reason, ChapterId = 1
            },
            ForSelf = true, PackageType = "Comfort"
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public void DefaultFormReasons_AreAllValidPackageReasons()
    {
        var reasons = FormDefinitions.Default(Key).Fields.First(f => f.Key == "reason").Options.Select(o => o.Value);
        Assert.All(reasons, r => Assert.Contains(r, FormDefinitions.ReasonValues));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HttpClient> AuthedClientAsync(string role)
    {
        var client = _factory.CreateClient();
        var email = $"form-test-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass1Form!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FirstName = "Form", LastName = "Tester", Role = role, ChapterId = 1
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { Username = email, Password = password });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }
}
