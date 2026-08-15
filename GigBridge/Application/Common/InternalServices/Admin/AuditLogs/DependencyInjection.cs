using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.AuditLogs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Admin.AuditLogs;
internal static class DependencyInjection
{
    internal static IServiceCollection AddAdminAuditLogServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        return services;
    }
}
