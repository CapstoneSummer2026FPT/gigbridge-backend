using Application.Features.Auth.Common.Email;
using Application.Features.Auth.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Auth.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        return services;
    }
}
