using Application.Common.Options;
using Application.Features.JobPosts.Common.BackgroundJobs;
using Application.Features.JobPosts.Common.ContentModeration;
using Application.Features.JobPosts.Common.ContentModeration.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Features.JobPosts.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddJobPostServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IContentModerationService, ContentModerationService>();

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddSingleton<DeadlineWarningService>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<DeadlineWarningService>());
        }

        return services;
    }
}
