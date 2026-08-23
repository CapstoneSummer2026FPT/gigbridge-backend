using Application.Common.InternalServices.Realtime.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Realtime;

internal static class DependencyInjection
{
    internal static IServiceCollection AddRealtimeRevisionServices(this IServiceCollection services)
    {
        services.AddScoped<ISaveChangesInterceptor, RealtimeRevisionSaveChangesInterceptor>();
        return services;
    }
}
