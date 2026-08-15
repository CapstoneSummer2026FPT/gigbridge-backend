using Application.Common.InternalServices.Auth.Services;
using Application.Common.InternalServices.Auth.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.Auth;
internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        return services;
    }
}
