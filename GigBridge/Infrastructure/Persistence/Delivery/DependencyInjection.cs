using Application.Common.InternalServices.Delivery.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence.Delivery;

internal static class DependencyInjection
{
    internal static IServiceCollection AddDeliveryPersistence(this IServiceCollection services)
    {
        services.AddScoped<IDeliveryOutboxStore, DeliveryOutboxStore>();
        return services;
    }
}
