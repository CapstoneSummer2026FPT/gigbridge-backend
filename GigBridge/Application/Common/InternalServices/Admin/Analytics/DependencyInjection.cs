using Application.Common.Options;
using Application.Common.InternalServices.Admin.Analytics.BackgroundJobs;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Admin.Analytics;
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
