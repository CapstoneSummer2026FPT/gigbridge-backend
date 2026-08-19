using Application.Common.Options;
using Application.Common.InternalServices.JobPosts.BackgroundJobs;
using Application.Common.InternalServices.JobPosts.Interfaces;
using Application.Common.InternalServices.JobPosts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Common.InternalServices.JobPosts;
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
