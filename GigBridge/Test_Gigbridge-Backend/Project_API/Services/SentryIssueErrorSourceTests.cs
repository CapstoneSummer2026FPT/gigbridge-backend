using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Project_API.Services.SystemTracking;

namespace Test_Gigbridge_Backend.Project_API.Services;

public sealed class SentryIssueErrorSourceTests
{
    [Fact]
    public async Task GetErrorsAsync_WhenNotConfigured_ReturnsDisabledStatusWithoutCallingSentry()
    {
        var handler = new CapturingHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
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
        const string payload = """
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
                "permalink": "https://gigbridge.sentry.io/issues/12345/",
                "project": {
                  "name": "GigBridge Backend",
                  "slug": "gigbridge-backend",
                  "platform": "csharp"
                }
              }
            ]
            """;
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        var options = new SentryMonitoringOptions
        {
            Organization = "gigbridge",
            AuthToken = "read-token",
            Environment = "production",
            ProjectIds = ["42"]
        };
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

    private static SentryIssueErrorSource CreateSource(
        SentryMonitoringOptions options,
        HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            NullLogger<SentryIssueErrorSource>.Instance);

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
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
            return Task.FromResult(responseFactory(request));
        }
    }
}
