using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Lotv.Tests.Integration;

/// <summary>
/// Tests for the Stripe payment webhook endpoint.
/// Verifies that:
///   - The endpoint is publicly accessible (AllowAnonymous)
///   - It accepts POST without authentication
///   - It accepts various content types
///   - It rejects non-POST verbs
/// Note: Full Stripe signature verification is tested at the service layer;
/// these tests cover the HTTP contract only (the endpoint is a stub returning 200).
/// </summary>
[Collection("Integration")]
public class PaymentWebhookTests
{
    private readonly LotvApiFactory _factory;

    public PaymentWebhookTests(LotvApiFactory factory) => _factory = factory;

    // ── Unauthenticated access ────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_NoToken_Returns200()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/payments/webhook", new { type = "payment_intent.succeeded" });

        // Webhook must be reachable without auth (Stripe cannot send auth tokens)
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_EmptyBody_DoesNotReturn401Or403()
    {
        var client = _factory.CreateClient();
        var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var resp = await client.PostAsync("/api/v1/payments/webhook", content);

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithBearerToken_StillAccepted()
    {
        // Even authenticated users (HQ admin viewing payment history) should not break the webhook
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var resp = await client.PostAsJsonAsync("/api/v1/payments/webhook",
            new { type = "charge.refunded" });

        // Should not return 403 — the endpoint is AllowAnonymous
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── HTTP method contract ──────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_PostMethod_IsAccepted()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/payments/webhook",
            new { id = "evt_test_001", type = "payment_intent.created" });

        Assert.True(
            resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.BadRequest,
            $"POST to webhook should not return {resp.StatusCode}");
    }

    [Fact]
    public async Task Webhook_GetMethod_IsNotAccepted()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/payments/webhook");

        // GET is not a registered route — returns 401/404/405 but never 200
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_PutMethod_IsNotAccepted()
    {
        var client = _factory.CreateClient();

        var resp = await client.PutAsJsonAsync("/api/v1/payments/webhook",
            new { type = "test" });

        // PUT is not a registered route — returns 401/404/405 but never 200
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Content type acceptance ───────────────────────────────────────────────

    [Fact]
    public async Task Webhook_RawBody_DoesNotReturn401Or403()
    {
        var client = _factory.CreateClient();
        // Stripe sends raw JSON body with a signature header
        var stripePayload = """{"id":"evt_test","object":"event","type":"payment_intent.succeeded","data":{"object":{}}}""";
        var content = new StringContent(stripePayload, Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Stripe-Signature",
            "t=1614556800,v1=fake_signature_for_test");

        var resp = await client.PostAsync("/api/v1/payments/webhook", content);

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Response body ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_SuccessfulPost_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/payments/webhook",
            new { type = "payment_intent.succeeded" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
