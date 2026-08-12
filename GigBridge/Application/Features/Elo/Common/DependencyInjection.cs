using Application.Features.Elo.Common.Interfaces;
using Application.Features.Elo.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Elo.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddEloServices(this IServiceCollection services)
    {
        services.AddScoped<IUserEloService, UserEloService>();
        return services;
    }
}
