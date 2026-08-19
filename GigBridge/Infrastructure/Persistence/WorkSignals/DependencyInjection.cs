using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Persistence.WorkSignals;

internal static class DependencyInjection
{
    internal static IServiceCollection AddWorkSignalListener(this IServiceCollection services)
    {
        services.AddHostedService<PostgresWorkSignalListener>();
        return services;
    }
}
