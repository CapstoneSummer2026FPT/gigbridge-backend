using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Notifications.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Notifications.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
