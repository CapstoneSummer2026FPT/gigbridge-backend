using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Accounts;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAccountServices(this IServiceCollection services)
    {
        services.AddScoped<IUserAccountStatusService, UserAccountStatusService>();
        return services;
    }
}
