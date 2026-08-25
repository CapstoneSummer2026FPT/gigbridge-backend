using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Application.Features.Admin.SystemTracking.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Monitoring.Sentry;

internal sealed class SentryIssueErrorSource(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<SentryMonitoringOptions> options,
    ILogger<SentryIssueErrorSource> logger) : ISystemErrorSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SentryMonitoringOptions _options = options.Value;

    public async Task<SystemErrorSourceResult> GetErrorsAsync(
        int requestedLimit,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return new SystemErrorSourceResult(
                false,
                false,
                "sentry",
                "Set SentryMonitoring__Organization and SentryMonitoring__AuthToken to load production issues.",
                []);
        }

        if (!TryBuildRequestUri(requestedLimit, out var requestUri))
        {
            logger.LogWarning("SentryMonitoring:BaseUrl is invalid.");
            return Unavailable("Sentry monitoring has an invalid API base URL.");
        }

        var cacheKey = $"sentry-issues:{requestUri}";
        if (cache.TryGetValue<SystemErrorSourceResult>(cacheKey, out var cachedResult) &&
            cachedResult is not null)
        {
            return cachedResult;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AuthToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Sentry Issues API returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return Unavailable($"Sentry returned HTTP {(int)response.StatusCode}.");
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var issues = await JsonSerializer.DeserializeAsync<List<SentryIssueDto>>(
                content,
                JsonOptions,
                cancellationToken) ?? [];
            var errors = issues
                .Select(MapIssue)
                .Where(error => error is not null)
                .Cast<SystemErrorLog>()
                .OrderByDescending(error => error.Timestamp)
                .ToArray();

            var result = new SystemErrorSourceResult(
                true,
                true,
                "sentry",
                $"Loaded {errors.Length} grouped issue(s) from Sentry.",
                errors);
            cache.Set(
                cacheKey,
                result,
                TimeSpan.FromSeconds(Math.Clamp(_options.CacheSeconds, 10, 300)));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Sentry Issues API request timed out.");
            return Unavailable("The Sentry request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the Sentry Issues API.");
            return Unavailable("The Sentry Issues API is temporarily unavailable.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Sentry returned an unexpected response.");
            return Unavailable("Sentry returned an unexpected response.");
        }
    }

    private bool TryBuildRequestUri(int requestedLimit, out Uri? requestUri)
    {
        requestUri = null;
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !baseUri.IsLoopback))
        {
            return false;
        }

        var organization = Uri.EscapeDataString(_options.Organization.Trim());
        var endpoint = new Uri(baseUri, $"organizations/{organization}/issues/");
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("query", "is:unresolved issue.category:error"),
            new("sort", "date"),
            new("statsPeriod", NormalizeStatsPeriod(_options.StatsPeriod)),
            new("limit", Math.Clamp(requestedLimit, 1, 100).ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(_options.Environment))
        {
            parameters.Add(new("environment", _options.Environment.Trim()));
        }

        var projectIds = _options.ProjectIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projectIds.Length == 0)
        {
            parameters.Add(new("project", "-1"));
        }
        else
        {
            parameters.AddRange(projectIds.Select(projectId =>
                new KeyValuePair<string, string>("project", projectId)));
        }

        var query = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        requestUri = new UriBuilder(endpoint) { Query = query }.Uri;
        return true;
    }

    private SystemErrorSourceResult Unavailable(string message) =>
        new(true, false, "sentry", message, []);

    private SystemErrorLog? MapIssue(SentryIssueDto issue)
    {
        if (string.IsNullOrWhiteSpace(issue.Id) ||
            !DateTimeOffset.TryParse(
                issue.LastSeen,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var lastSeen))
        {
            return null;
        }

        _ = DateTimeOffset.TryParse(
            issue.FirstSeen,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var firstSeen);
        var count = int.TryParse(
            issue.Count,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedCount)
            ? Math.Max(1, parsedCount)
            : 1;
        var service = issue.Project?.Slug ?? issue.Project?.Name ?? "unknown-service";
        var message = !string.IsNullOrWhiteSpace(issue.Title)
            ? issue.Title
            : issue.Metadata?.Title ?? "Unhandled application error";

        return new SystemErrorLog(
            issue.Id,
            lastSeen,
            NormalizeLevel(issue.Level),
            service,
            message,
            issue.ShortId ?? issue.Id,
            count,
            "sentry",
            IsSafeSentryUrl(issue.Permalink) ? issue.Permalink : null,
            firstSeen == default ? null : firstSeen,
            issue.Status,
            _options.Environment,
            issue.Platform ?? issue.Project?.Platform);
    }

    private static string NormalizeLevel(string? level) => level?.ToLowerInvariant() switch
    {
        "fatal" => "critical",
        "warning" => "warning",
        "info" => "info",
        _ => "error"
    };

    private static string NormalizeStatsPeriod(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "1h" or "6h" or "12h" or "24h" or "7d" or "14d" or "30d" => value.Trim().ToLowerInvariant(),
        _ => "14d"
    };

    private static bool IsSafeSentryUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("sentry.io", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".sentry.io", StringComparison.OrdinalIgnoreCase));

    private sealed class SentryIssueDto
    {
        public string? Id { get; init; }
        public string? ShortId { get; init; }
        public string? Title { get; init; }
        public string? Level { get; init; }
        public string? Status { get; init; }
        public string? Platform { get; init; }
        public string? Count { get; init; }
        public string? FirstSeen { get; init; }
        public string? LastSeen { get; init; }
        public string? Permalink { get; init; }
        public SentryProjectDto? Project { get; init; }
        public SentryMetadataDto? Metadata { get; init; }
    }

    private sealed class SentryProjectDto
    {
        public string? Name { get; init; }
        public string? Slug { get; init; }
        public string? Platform { get; init; }
    }

    private sealed class SentryMetadataDto
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }
    }
}
