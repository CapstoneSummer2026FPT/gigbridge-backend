using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Elo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Elo;
internal static class DependencyInjection
{
    internal static IServiceCollection AddEloServices(this IServiceCollection services)
    {
        services.AddScoped<IUserEloService, UserEloService>();
        return services;
    }
}
