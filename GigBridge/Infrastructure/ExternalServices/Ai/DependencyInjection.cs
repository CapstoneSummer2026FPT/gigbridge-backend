using Application.Common.Interfaces.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Ai;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAiExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowLocalHttp = ExternalServiceUriPolicy.IsLocalEnvironment(
            configuration["ASPNETCORE_ENVIRONMENT"]);

        services
            .AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    var baseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_BASE_URL");
                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        options.BaseUrl = baseUrl;
                    }
                }

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    var apiKey = Environment.GetEnvironmentVariable("AI_SERVICE_API_KEY");
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        options.ApiKey = apiKey;
                    }
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey),
                "AI Service configuration is missing. Set AI_SERVICE_BASE_URL and AI_SERVICE_API_KEY.")
            .Validate(
                options => ExternalServiceUriPolicy.IsAllowed(options.BaseUrl, allowLocalHttp),
                "AI Service base URL must use HTTPS, except for HTTP loopback URLs in local environments.")
            .ValidateOnStart();

        services.AddHttpClient<IAiServiceClient, AiServiceClient>();
        return services;
    }
}
