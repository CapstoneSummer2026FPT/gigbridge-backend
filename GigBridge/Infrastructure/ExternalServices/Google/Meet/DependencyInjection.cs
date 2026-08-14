using Application.Features.Chat.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Google.Meet;

internal static class DependencyInjection
{
    internal static IServiceCollection AddGoogleMeetExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowLocalHttp = ExternalServiceUriPolicy.IsLocalEnvironment(
            configuration["ASPNETCORE_ENVIRONMENT"]);

        services
            .AddOptions<GoogleMeetOptions>()
            .Bind(configuration.GetSection(GoogleMeetOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ClientId))
                {
                    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_MEET_CLIENT_ID")
                        ?? configuration["Authentication:Google:ClientId"]
                        ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                {
                    options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_MEET_CLIENT_SECRET")
                        ?? configuration["Authentication:Google:ClientSecret"]
                        ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(options.BackendCallbackUri))
                {
                    options.BackendCallbackUri = Environment.GetEnvironmentVariable("GOOGLE_MEET_BACKEND_CALLBACK_URI")
                        ?? (allowLocalHttp
                            ? "http://localhost:5222/api/integrations/google-meet/callback"
                            : string.Empty);
                }

                if (string.IsNullOrWhiteSpace(options.FrontendCallbackUri))
                {
                    var frontendBaseUrl = configuration["FrontendBaseUrl"]?.TrimEnd('/');
                    options.FrontendCallbackUri = Environment.GetEnvironmentVariable("GOOGLE_MEET_FRONTEND_CALLBACK_URI")
                        ?? (string.IsNullOrWhiteSpace(frontendBaseUrl)
                            ? string.Empty
                            : $"{frontendBaseUrl}/integrations/google-meet/callback");
                }

                if (string.IsNullOrWhiteSpace(options.MeetApiBaseUrl))
                {
                    options.MeetApiBaseUrl = "https://meet.googleapis.com";
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ClientSecret) &&
                    !string.IsNullOrWhiteSpace(options.BackendCallbackUri) &&
                    !string.IsNullOrWhiteSpace(options.FrontendCallbackUri),
                "Google Meet configuration is missing. Set client credentials and both backend/frontend callback URIs.")
            .Validate(
                options =>
                    ExternalServiceUriPolicy.IsAllowed(options.AuthorizationEndpoint, allowLocalHttp) &&
                    ExternalServiceUriPolicy.IsAllowed(options.TokenEndpoint, allowLocalHttp) &&
                    ExternalServiceUriPolicy.IsAllowed(options.RevocationEndpoint, allowLocalHttp) &&
                    ExternalServiceUriPolicy.IsAllowed(options.MeetApiBaseUrl, allowLocalHttp) &&
                    ExternalServiceUriPolicy.IsAllowed(options.BackendCallbackUri, allowLocalHttp) &&
                    ExternalServiceUriPolicy.IsAllowed(options.FrontendCallbackUri, allowLocalHttp),
                "Google Meet endpoints and callback URIs must use HTTPS, except for HTTP loopback URLs in local environments.")
            .ValidateOnStart();

        services.AddHttpClient("GoogleMeetOAuth")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<IGoogleMeetApiClient, GoogleMeetApiClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<GoogleMeetIdTokenValidator>();
        services.AddScoped<IGoogleMeetOAuthService, GoogleMeetOAuthService>();
        return services;
    }
}
