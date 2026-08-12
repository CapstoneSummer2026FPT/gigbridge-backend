using Application.Features.Admin.AuditLogs.Common.Interfaces;
using Application.Features.Admin.AuditLogs.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Admin.AuditLogs.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAdminAuditLogServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        return services;
    }
}
