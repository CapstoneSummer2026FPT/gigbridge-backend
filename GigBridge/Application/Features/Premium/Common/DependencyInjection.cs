using Application.Common.Options;
using Application.Features.Premium.Common.BackgroundJobs;
using Application.Features.Premium.Common.Interfaces;
using Application.Features.Premium.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Premium.Common;

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
