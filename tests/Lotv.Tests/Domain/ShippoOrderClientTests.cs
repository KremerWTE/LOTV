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
}
