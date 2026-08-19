using Application.Common.InternalServices.WorkSignals.Interfaces;
using Application.Common.InternalServices.WorkSignals.Models;
using Application.Common.InternalServices.WorkSignals.Services;
using Application.Common.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.WorkSignals;

internal static class DependencyInjection
{
    internal static IServiceCollection AddWorkSignalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<WorkSignalOptions>()
            .Bind(
                configuration.GetSection(WorkSignalOptions.SectionName),
                binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();

        foreach (var channel in WorkSignalChannels.All)
        {
            services.AddKeyedSingleton<WorkSignalGate>(channel);
            services.AddKeyedSingleton<IWorkSignalSource>(
                channel,
                (provider, key) => provider.GetRequiredKeyedService<WorkSignalGate>(key!));
        }

        services.AddScoped<IWorkSignalPublisher, WorkSignalPublisher>();
        services.AddSingleton<ISaveChangesInterceptor, WorkSignalSaveChangesInterceptor>();

        return services;
    }
}
