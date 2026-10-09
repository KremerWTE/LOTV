using System.Net;
using System.Text;
using System.Text.Json;
using Lotv.Api.Services;
using Lotv.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lotv.Tests.Domain;

public class ShippoOrderClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static ShippoOptions Configured() => new()
    {
        ApiToken = "shippo_test_abc", FromName = "LOTV", FromStreet1 = "1 Main St", FromCity = "Springfield", FromState = "IL", FromZip = "62701"
    };

    private static (PackageRequest, Family) Case() => (
        new PackageRequest { Id = 42, CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), InternalNotes = "SECRET NOTE", ChildrenInitials = "XY" },
        new Family { Parent1FirstName = "Ann", Parent1LastName = "Lee", StreetAddress = "9 Oak Ave", City = "Austin", State = "TX", Zip = "78701", Email = "a@x.com", Phone = "555-0100" });

    private static ShippoOrderClient Client(StubHandler h, ShippoOptions o) =>
        new(new HttpClient(h), Options.Create(o), NullLogger<ShippoOrderClient>.Instance);

    [Fact]
    public async Task NotConfigured_ReturnsFailureWithoutCallingShippo()
    {
        var h = new StubHandler(HttpStatusCode.OK, "{}");
        var c = Client(h, new ShippoOptions());
        Assert.False(c.IsConfigured);
        var (r, f) = Case();
        var result = await c.CreateOrderAsync(r, f);
        Assert.False(result.Success);
        Assert.Null(h.Request);
    }

    [Fact]
    public async Task Success_PostsToOrdersWithTokenAndShippingEssentialsOnly()
    {
        var h = new StubHandler(HttpStatusCode.Created, "{\"object_id\":\"ord_123\"}");
        var (r, f) = Case();
        var result = await Client(h, Configured()).CreateOrderAsync(r, f);

        Assert.True(result.Success);
        Assert.Equal("ord_123", result.OrderId);
        Assert.Equal("https://api.goshippo.com/orders/", h.Request!.RequestUri!.ToString());
        Assert.Equal("ShippoToken", h.Request.Headers.Authorization!.Scheme);
        Assert.Equal("shippo_test_abc", h.Request.Headers.Authorization.Parameter);

        using var doc = JsonDocument.Parse(h.RequestBody!);
        Assert.Equal("LOTV-42", doc.RootElement.GetProperty("order_number").GetString());
        Assert.Equal("9 Oak Ave", doc.RootElement.GetProperty("to_address").GetProperty("street1").GetString());
        Assert.Equal("62701", doc.RootElement.GetProperty("from_address").GetProperty("zip").GetString());
        Assert.DoesNotContain("SECRET NOTE", h.RequestBody);
        Assert.DoesNotContain("children", h.RequestBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShippoError_IsReturnedNotThrown()
    {
        var h = new StubHandler(HttpStatusCode.BadRequest, "{\"detail\":\"bad address\"}");
        var (r, f) = Case();
        var result = await Client(h, Configured()).CreateOrderAsync(r, f);
        Assert.False(result.Success);
        Assert.Contains("400", result.Error);
    }

    // ── Return address read from the Shippo account on every order ──────────────────────────────────────────────────
    private sealed class RoutingHandler(string addressesJson) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public string? OrderBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(addressesJson, Encoding.UTF8, "application/json") };
            OrderBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"object_id\":\"ord_9\"}", Encoding.UTF8, "application/json") };
        }
    }

    private static string Addr(string name, string zip, string company = "", bool isDefault = false) =>
        $"{{\"name\":\"{name}\",\"company\":\"{company}\",\"street1\":\"5 Lily Ln\",\"city\":\"Chicago\",\"state\":\"IL\",\"zip\":\"{zip}\",\"country\":\"US\"{(isDefault ? ",\"is_default_sender\":true" : "")}}}";
    private static string Results(params string[] items) => "{\"results\":[" + string.Join(",", items) + "]}";
    private static ShippoOrderClient Routed(RoutingHandler h, ShippoOptions o) =>
        new(new HttpClient(h), Options.Create(o), NullLogger<ShippoOrderClient>.Instance);

    [Fact]
    public async Task ReturnAddress_IsReadFromShippo_EvenWhenSettingsHaveOne()
    {
        var h = new RoutingHandler(Results(Addr("Ministry", "60601")));
        var (r, f) = Case();
        var result = await Routed(h, Configured()).CreateOrderAsync(r, f);   // Configured() has zip 62701 as a fallback

        Assert.True(result.Success);
        Assert.Equal(["GET /addresses/", "POST /orders/"], h.Calls);
        using var doc = JsonDocument.Parse(h.OrderBody!);
        Assert.Equal("60601", doc.RootElement.GetProperty("from_address").GetProperty("zip").GetString());
    }

    [Fact]
    public async Task OnlyTheTokenIsNeeded_WhenShippoHasOneSavedAddress()
    {
        var h = new RoutingHandler(Results(Addr("Ministry", "60601")));
        var c = Routed(h, new ShippoOptions { ApiToken = "shippo_test_abc" });
        Assert.True(c.IsConfigured);
        var (r, f) = Case();
        Assert.True((await c.CreateOrderAsync(r, f)).Success);
    }

    [Fact]
    public async Task SeveralSavedAddresses_FailWithoutGuessing_AndSendNoOrder()
    {
        var h = new RoutingHandler(Results(Addr("Home", "11111"), Addr("Ministry", "60601")));
        var (r, f) = Case();
        var result = await Routed(h, new ShippoOptions { ApiToken = "shippo_test_abc" }).CreateOrderAsync(r, f);

        Assert.False(result.Success);
        Assert.Contains("SHIPPO_RETURN_ADDRESS_NAME", result.Error);
        Assert.DoesNotContain("POST /orders/", h.Calls);
    }

    [Fact]
    public async Task NameHint_PicksTheRightAddress_AndADefaultFlagCanDecide()
    {
        var (r, f) = Case();
        var hinted = new RoutingHandler(Results(Addr("Home", "11111"), Addr("Ministry", "60601", company: "Lily of the Valley")));
        Assert.True((await Routed(hinted, new ShippoOptions { ApiToken = "t", ReturnAddressName = "lily" }).CreateOrderAsync(r, f)).Success);
        Assert.Equal("60601", JsonDocument.Parse(hinted.OrderBody!).RootElement.GetProperty("from_address").GetProperty("zip").GetString());

        var flagged = new RoutingHandler(Results(Addr("Home", "11111"), Addr("Ministry", "60601", isDefault: true)));
        Assert.True((await Routed(flagged, new ShippoOptions { ApiToken = "t" }).CreateOrderAsync(r, f)).Success);
        Assert.Equal("60601", JsonDocument.Parse(flagged.OrderBody!).RootElement.GetProperty("from_address").GetProperty("zip").GetString());
    }

    [Fact]
    public async Task NoSavedAddress_FallsBackToSettings_OrFailsClearly()
    {
        var (r, f) = Case();
        var withSettings = new RoutingHandler(Results());
        Assert.True((await Routed(withSettings, Configured()).CreateOrderAsync(r, f)).Success);
        Assert.Equal("62701", JsonDocument.Parse(withSettings.OrderBody!).RootElement.GetProperty("from_address").GetProperty("zip").GetString());

        var none = new RoutingHandler(Results());
        var result = await Routed(none, new ShippoOptions { ApiToken = "t" }).CreateOrderAsync(r, f);
        Assert.False(result.Success);
        Assert.Contains("No return address found", result.Error);
        Assert.DoesNotContain("POST /orders/", none.Calls);
    }

    // ── Reading the bought label back from Shippo ─────────────────────────────────────────────────────────────────
    private sealed class LabelHandler(string orderJson, string rateJson) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add(request.RequestUri!.AbsolutePath);
            var body = request.RequestUri.AbsolutePath.StartsWith("/rates/") ? rateJson : orderJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task GetLabel_BeforeAnyLabelIsBought_ReportsNotPurchased()
    {
        var h = new LabelHandler("{\"object_id\":\"o1\",\"transactions\":[]}", "{}");
        var r = await Client(new StubHandler(HttpStatusCode.OK, "{}"), Configured()).GetLabelAsync("o1");
        Assert.True(r.Success);
        Assert.False(r.Purchased);

        var r2 = await new ShippoOrderClient(new HttpClient(h), Options.Create(Configured()), NullLogger<ShippoOrderClient>.Instance).GetLabelAsync("o1");
        Assert.True(r2.Success);
        Assert.False(r2.Purchased);
    }

    [Fact]
    public async Task GetLabel_AfterPurchase_ReturnsTrackingCarrierAndLabelLink()
    {
        var order = "{\"object_id\":\"o1\",\"transactions\":[{\"status\":\"ERROR\",\"tracking_number\":\"\"},{\"status\":\"SUCCESS\",\"tracking_number\":\"9400111\",\"label_url\":\"https://shippo.example/l.pdf\",\"rate\":\"rate_1\"}]}";
        var rate = "{\"provider\":\"USPS\",\"servicelevel\":{\"name\":\"Priority Mail\"}}";
        var h = new LabelHandler(order, rate);
        var r = await new ShippoOrderClient(new HttpClient(h), Options.Create(Configured()), NullLogger<ShippoOrderClient>.Instance).GetLabelAsync("o1");

        Assert.True(r.Success);
        Assert.True(r.Purchased);
        Assert.Equal("9400111", r.TrackingNumber);
        Assert.Equal("USPS", r.Carrier);
        Assert.Equal("Priority Mail", r.ServiceLevel);
        Assert.Equal("https://shippo.example/l.pdf", r.LabelUrl);
        Assert.Equal(["/orders/o1", "/rates/rate_1"], h.Calls);
    }

    [Fact]
    public async Task GetLabel_WhenShippoFails_ReturnsAnErrorInsteadOfThrowing()
    {
        var h = new StubHandler(HttpStatusCode.InternalServerError, "{}");
        var r = await Client(h, Configured()).GetLabelAsync("o1");
        Assert.False(r.Success);
        Assert.NotNull(r.Error);
    }
}
