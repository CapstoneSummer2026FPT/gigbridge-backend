using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Auditing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Auditing;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuditingServices(this IServiceCollection services)
    {
        services.AddScoped<IUserAuditLogService, UserAuditLogService>();
        return services;
    }
}
