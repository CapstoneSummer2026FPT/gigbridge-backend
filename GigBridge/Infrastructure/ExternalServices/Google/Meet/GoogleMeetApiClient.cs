using Application.Features.Chat.Common.Models;
using System.Text;
using System.Text.Json;
using Application.Features.Chat.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Google.Meet;

public class GoogleMeetApiClient : IGoogleMeetApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GoogleMeetOptions _options;
    private readonly ILogger<GoogleMeetApiClient> _logger;

    public GoogleMeetApiClient(
        HttpClient httpClient,
        IOptions<GoogleMeetOptions> options,
        ILogger<GoogleMeetApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CreateMeetSpaceResult> CreateSpaceAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        // Access policy changes are unavailable to some consumer and Workspace
        // accounts. An empty space request lets Google apply the organizer's
        // supported default access policy.
        var requestBody = new { };
        var json = JsonSerializer.Serialize(requestBody, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.MeetApiBaseUrl}/v2/spaces")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var isServerError = (int)response.StatusCode >= 500;
                var failureCode = isServerError ? "meet_server_error" : "meet_api_error";

                _logger.LogWarning(
                    "Meet API returned {Status} for space creation",
                    response.StatusCode);

                return new CreateMeetSpaceResult(
                    false,
                    isServerError,
                    null,
                    null,
                    failureCode);
            }

            var space = JsonSerializer.Deserialize<GoogleMeetSpaceResponse>(responseBody, JsonOptions);
            if (space is null || string.IsNullOrEmpty(space.Name) || string.IsNullOrEmpty(space.MeetingUri))
            {
                _logger.LogWarning("Meet API returned an invalid response for space creation");
                return new CreateMeetSpaceResult(false, true, null, null, "invalid_response");
            }

            // Validate the meeting URI
            if (!Uri.TryCreate(space.MeetingUri, UriKind.Absolute, out var uri) ||
                uri.Scheme != "https" ||
                uri.Host != "meet.google.com")
            {
                _logger.LogWarning("Meet API returned an invalid meeting URI");
                return new CreateMeetSpaceResult(false, true, null, null, "invalid_meeting_uri");
            }

            return new CreateMeetSpaceResult(true, false, space.Name, space.MeetingUri, null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Meet API request timed out after 15 seconds");
            return new CreateMeetSpaceResult(false, true, null, null, "timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Meet API request failed due to network error");
            return new CreateMeetSpaceResult(false, true, null, null, "network_error");
        }
    }

    private record GoogleMeetSpaceResponse(string? Name, string? MeetingUri, string? MeetingCode);
}
