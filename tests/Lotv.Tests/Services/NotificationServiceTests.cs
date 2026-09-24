using System.Net;
using System.Text;
using System.Text.Json;
using Lotv.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lotv.Tests.Services;

/// <summary>Email goes out through SocketLabs when it is configured, else SMTP, else it is only logged.</summary>
public class NotificationServiceTests
{
    private sealed class FakeSocketLabs : HttpMessageHandler
    {
        public readonly List<(Uri Url, JsonElement Body)> Requests = [];
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string ResponseBody = "{\"ErrorCode\":\"Success\",\"MessageResults\":[],\"TransactionReceipt\":null}";
        public Exception? Throw;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw is not null) throw Throw;
            var text = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!, JsonDocument.Parse(text).RootElement.Clone()));
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }

    private static NotificationService Create(Dictionary<string, string?> settings, FakeSocketLabs handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        return new NotificationService(config, NullLogger<NotificationService>.Instance, factory.Object);
    }

    private static Dictionary<string, string?> SocketLabsSettings() => new()
    {
        ["SocketLabs:ServerId"] = "12345",
        ["SocketLabs:ApiKey"] = "secret-key-value",
        ["SocketLabs:FromEmail"] = "hello@lotvministry.org",
        ["SocketLabs:FromName"] = "Lily of the Valley",
        ["SocketLabs:ReplyTo"] = "info@lotvministry.org",
    };

    private const string Html = "<html><head><style>p{color:red}</style></head><body><h2>Hello</h2><p>Dear Mary,</p><p>Your <a href=\"https://lotv.wte.net/x\">package</a> &amp; card.</p></body></html>";

    // ── SocketLabs ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenConfigured_TheEmailIsPostedToSocketLabs_WithTheAddressesSubjectAndBothBodies()
    {
        var handler = new FakeSocketLabs();
        var result = await Create(SocketLabsSettings(), handler).SendEmailAsync("mary@example.org", "Mary Example", "Your package", Html);

        Assert.True(result.IsSuccess);
        var (url, body) = Assert.Single(handler.Requests);
        Assert.Equal(NotificationService.DefaultSocketLabsEndpoint, url.ToString());
        Assert.Equal(12345, body.GetProperty("serverId").GetInt32());
        Assert.Equal("secret-key-value", body.GetProperty("apiKey").GetString());

        var message = body.GetProperty("messages")[0];
        Assert.Equal("mary@example.org", message.GetProperty("to")[0].GetProperty("emailAddress").GetString());
        Assert.Equal("Mary Example", message.GetProperty("to")[0].GetProperty("friendlyName").GetString());
        Assert.Equal("hello@lotvministry.org", message.GetProperty("from").GetProperty("emailAddress").GetString());
        Assert.Equal("Lily of the Valley", message.GetProperty("from").GetProperty("friendlyName").GetString());
        Assert.Equal("info@lotvministry.org", message.GetProperty("replyTo").GetProperty("emailAddress").GetString());
        Assert.Equal("Your package", message.GetProperty("subject").GetString());
        Assert.Equal(Html, message.GetProperty("htmlBody").GetString());
        var text = message.GetProperty("textBody").GetString()!;
        Assert.Contains("Dear Mary,", text);
        Assert.Contains("package (https://lotv.wte.net/x) & card.", text);
        Assert.DoesNotContain("<", text);
        Assert.DoesNotContain("color:red", text);
    }

    [Fact]
    public async Task WithoutAReplyTo_NoReplyToIsSent()
    {
        var settings = SocketLabsSettings();
        settings.Remove("SocketLabs:ReplyTo");
        var handler = new FakeSocketLabs();

        await Create(settings, handler).SendEmailAsync("a@example.org", "A", "S", Html);

        Assert.False(handler.Requests[0].Body.GetProperty("messages")[0].TryGetProperty("replyTo", out _));
    }

    [Theory]
    [InlineData("InvalidAuthentication")]
    [InlineData("InvalidData")]
    public async Task ASocketLabsErrorCode_IsAFailure_ThatNeverExposesTheKey(string code)
    {
        var handler = new FakeSocketLabs { ResponseBody = $"{{\"ErrorCode\":\"{code}\"}}" };
        var result = await Create(SocketLabsSettings(), handler).SendEmailAsync("a@example.org", "A", "S", Html);

        Assert.False(result.IsSuccess);
        Assert.Contains(code, result.Error);
        Assert.DoesNotContain("secret-key-value", result.Error);
    }

    [Fact]
    public async Task AWarning_CountsAsSent()
    {
        var handler = new FakeSocketLabs { ResponseBody = "{\"ErrorCode\":\"Warning\"}" };
        Assert.True((await Create(SocketLabsSettings(), handler).SendEmailAsync("a@example.org", "A", "S", Html)).IsSuccess);
    }

    [Fact]
    public async Task AnHttpErrorOrUnreadableResponse_IsAFailure()
    {
        var httpError = await Create(SocketLabsSettings(), new FakeSocketLabs { Status = HttpStatusCode.InternalServerError })
            .SendEmailAsync("a@example.org", "A", "S", Html);
        Assert.False(httpError.IsSuccess);
        Assert.Contains("500", httpError.Error);

        var garbage = await Create(SocketLabsSettings(), new FakeSocketLabs { ResponseBody = "not json" })
            .SendEmailAsync("a@example.org", "A", "S", Html);
        Assert.False(garbage.IsSuccess);
    }

    [Fact]
    public async Task ANetworkFailure_IsAFailureNotACrash()
    {
        var handler = new FakeSocketLabs { Throw = new HttpRequestException("connection refused") };
        var result = await Create(SocketLabsSettings(), handler).SendEmailAsync("a@example.org", "A", "S", Html);
        Assert.False(result.IsSuccess);
        Assert.Contains("connection refused", result.Error);
    }

    // ── Which provider ────────────────────────────────────────────────────────

    [Fact]
    public async Task SocketLabsIsPreferredOverSmtp_WhenBothAreSet()
    {
        var settings = SocketLabsSettings();
        settings["Smtp:Host"] = "smtp.invalid.example";   // would fail if it were tried
        var handler = new FakeSocketLabs();

        Assert.True((await Create(settings, handler).SendEmailAsync("a@example.org", "A", "S", Html)).IsSuccess);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task WithNeitherConfigured_TheEmailIsOnlyLogged_AndNothingIsSent()
    {
        var handler = new FakeSocketLabs();
        var result = await Create([], handler).SendEmailAsync("a@example.org", "A", "S", Html);
        Assert.True(result.IsSuccess);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("12345", "key", null, "SocketLabs")]
    [InlineData("12345", "", null, "Log only")]        // needs both the id and the key
    [InlineData("", "key", null, "Log only")]
    [InlineData("abc", "key", null, "Log only")]       // the id must be a number
    [InlineData("12345", "key", "smtp.example.org", "SocketLabs")]
    [InlineData(null, null, "smtp.example.org", "SMTP")]
    [InlineData(null, null, null, "Log only")]
    public void ActiveProvider_ReportsWhatWillBeUsed(string? serverId, string? apiKey, string? smtpHost, string expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SocketLabs:ServerId"] = serverId, ["SocketLabs:ApiKey"] = apiKey, ["Smtp:Host"] = smtpHost,
        }).Build();
        Assert.Equal(expected, NotificationService.ActiveProvider(config));
    }

    // ── Plain-text version ────────────────────────────────────────────────────

    [Fact]
    public void ToPlainText_KeepsParagraphsAndLinks_AndDropsMarkup()
    {
        var text = NotificationService.ToPlainText("<p>One</p><p>Two<br>three</p><ul><li>a</li><li>b</li></ul><a href=\"https://x.example/y\">link</a>");
        Assert.Equal("One\nTwo\nthree\na\nb\nlink (https://x.example/y)", text);
    }
}
