using Application.Common.Options;
using Application.Features.Admin.Analytics.Common.BackgroundJobs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Admin.Analytics.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAdminAnalyticsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddHostedService<AnalyticsMaintenanceWorker>();
        }

        return services;
    }
}
