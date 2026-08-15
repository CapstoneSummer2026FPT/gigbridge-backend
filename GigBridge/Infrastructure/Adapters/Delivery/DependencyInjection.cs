using Application.Common.InternalServices.Delivery.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Adapters.Delivery;

internal static class DependencyInjection
{
    internal static IServiceCollection AddDeliveryAdapter(this IServiceCollection services)
    {
        services.AddScoped<IDeliveryOutboxStore, DeliveryOutboxStore>();
        return services;
    }
}
