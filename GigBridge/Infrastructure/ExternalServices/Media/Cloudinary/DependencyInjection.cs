using Application.Common.Interfaces.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Media.Cloudinary;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCloudinaryExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .PostConfigure(options =>
            {
                var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
                if (!string.IsNullOrWhiteSpace(cloudName))
                {
                    options.CloudName = cloudName;
                }

                var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    options.ApiKey = apiKey;
                }

                var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");
                if (!string.IsNullOrWhiteSpace(apiSecret))
                {
                    options.ApiSecret = apiSecret;
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.CloudName) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ApiSecret),
                "Cloudinary configuration is missing. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET.")
            .ValidateOnStart();

        services.AddScoped<IMediaService, MediaService>();
        return services;
    }
}
