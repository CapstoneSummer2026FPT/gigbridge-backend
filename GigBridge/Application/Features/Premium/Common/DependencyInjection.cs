using Application.Features.Premium.Common.Interfaces;
using Application.Features.Premium.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Premium.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddPremiumServices(this IServiceCollection services)
    {
        services.AddScoped<IPremiumAccessService, PremiumAccessService>();
        return services;
    }
}
