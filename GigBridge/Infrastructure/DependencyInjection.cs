using Infrastructure.Adapters;
using Infrastructure.ExternalServices;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddInfrastructureAdapters(configuration);
        services.AddExternalServices(configuration);
        return services;
    }
}
