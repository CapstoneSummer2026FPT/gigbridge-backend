using Application.Common.InternalServices.Delivery.BackgroundJobs;
using Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.Delivery;

internal static class DependencyInjection
{
    internal static IServiceCollection AddDeliveryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<DeliveryOutboxOptions>, DeliveryOutboxOptionsValidator>();
        services
            .AddOptions<DeliveryOutboxOptions>()
            .Bind(
                configuration.GetSection(DeliveryOutboxOptions.SectionName),
                binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddSingleton<DeliveryOutboxService>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<DeliveryOutboxService>());
        }

        return services;
    }
}
