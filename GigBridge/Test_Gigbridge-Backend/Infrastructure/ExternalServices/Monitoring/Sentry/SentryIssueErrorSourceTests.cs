using System.Net;
using System.Text;
using Infrastructure.ExternalServices.Monitoring.Sentry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Monitoring.Sentry;

public sealed class SentryIssueErrorSourceTests
{
    [Fact]
    public async Task GetErrorsAsync_WhenNotConfigured_ReturnsDisabledStatusWithoutCallingSentry()
    {
        var handler = new CapturingHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called."));
        var source = CreateSource(new SentryMonitoringOptions(), handler);

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.False(result.Configured);
        Assert.False(result.Available);
        Assert.Empty(result.Errors);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenConfigured_MapsGroupedIssuesAndUsesBearerToken()
    {
        var handler = Respond(HttpStatusCode.OK, ValidPayload(
            "https://gigbridge.sentry.io/issues/12345/"));
        var options = ConfiguredOptions();
        var source = CreateSource(options, handler);

        var result = await source.GetErrorsAsync(25, CancellationToken.None);
        var cachedResult = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.True(result.Configured);
        Assert.True(result.Available);
        var error = Assert.Single(result.Errors);
        Assert.Equal("critical", error.Level);
        Assert.Equal("gigbridge-backend", error.Service);
        Assert.Equal("sentry", error.Source);
        Assert.Equal(9, error.Count);
        Assert.Equal("BACKEND-7", error.RequestId);
        Assert.Equal("https://gigbridge.sentry.io/issues/12345/", error.ExternalUrl);
        Assert.NotNull(handler.Request);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization?.Scheme);
        Assert.Equal("read-token", handler.Request.Headers.Authorization?.Parameter);
        Assert.Contains("environment=production", handler.Request.RequestUri?.Query);
        Assert.Contains("project=42", handler.Request.RequestUri?.Query);
        Assert.Same(result, cachedResult);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenPermalinkIsOutsideSentry_DropsExternalUrl()
    {
        var source = CreateSource(
            ConfiguredOptions(),
            Respond(HttpStatusCode.OK, ValidPayload("https://malicious.example/issues/12345/")));

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.Null(Assert.Single(result.Errors).ExternalUrl);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenSentryReturnsFailure_ReturnsUnavailableStatus()
    {
        var source = CreateSource(
            ConfiguredOptions(),
            Respond(HttpStatusCode.ServiceUnavailable, "unavailable"));

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Available);
        Assert.Equal("Sentry returned HTTP 503.", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenSentryReturnsMalformedJson_ReturnsUnavailableStatus()
    {
        var source = CreateSource(
            ConfiguredOptions(),
            Respond(HttpStatusCode.OK, "{not-json"));

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Available);
        Assert.Equal("Sentry returned an unexpected response.", result.Message);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenRequestTimesOut_ReturnsUnavailableStatus()
    {
        var handler = new CapturingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        var source = CreateSource(ConfiguredOptions(), handler);

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.True(result.Configured);
        Assert.False(result.Available);
        Assert.Equal("The Sentry request timed out.", result.Message);
    }

    [Fact]
    public async Task GetErrorsAsync_WhenBaseUrlIsUnsafe_DoesNotCallSentry()
    {
        var options = ConfiguredOptions();
        options.BaseUrl = "http://sentry.example/api/0/";
        var handler = new CapturingHandler((_, _) =>
            throw new InvalidOperationException("HTTP should not be called."));
        var source = CreateSource(options, handler);

        var result = await source.GetErrorsAsync(25, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Equal("Sentry monitoring has an invalid API base URL.", result.Message);
        Assert.Null(handler.Request);
    }

    private static SentryMonitoringOptions ConfiguredOptions() => new()
    {
        Organization = "gigbridge",
        AuthToken = "read-token",
        Environment = "production",
        ProjectIds = ["42"]
    };

    private static CapturingHandler Respond(HttpStatusCode statusCode, string payload) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));

    private static string ValidPayload(string permalink) => $$"""
        [
          {
            "id": "12345",
            "shortId": "BACKEND-7",
            "title": "InvalidOperationException: failed",
            "level": "fatal",
            "status": "unresolved",
            "platform": "csharp",
            "count": "9",
            "firstSeen": "2026-08-20T10:00:00Z",
            "lastSeen": "2026-08-21T10:00:00Z",
            "permalink": "{{permalink}}",
            "project": {
              "name": "GigBridge Backend",
              "slug": "gigbridge-backend",
              "platform": "csharp"
            }
          }
        ]
        """;

    private static SentryIssueErrorSource CreateSource(
        SentryMonitoringOptions options,
        HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            NullLogger<SentryIssueErrorSource>.Instance);

    private sealed class CapturingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return responseFactory(request, cancellationToken);
        }
    }
}
