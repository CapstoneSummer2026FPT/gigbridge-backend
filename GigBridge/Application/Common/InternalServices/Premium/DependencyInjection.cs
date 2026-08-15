using Application.Common.Options;
using Application.Common.InternalServices.Premium.BackgroundJobs;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Common.InternalServices.Premium.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Premium;
internal static class DependencyInjection
{
    internal static IServiceCollection AddPremiumServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IPremiumAccessService, PremiumAccessService>();

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddHostedService<PremiumExpiryWorker>();
        }

        return services;
    }
}
