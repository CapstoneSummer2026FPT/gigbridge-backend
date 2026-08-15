using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.InternalServices.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Notifications;
internal static class DependencyInjection
{
    internal static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
