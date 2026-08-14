using Application.Features.Auth.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Google.Auth;

internal static class DependencyInjection
{
    internal static IServiceCollection AddGoogleAuthExternalService(this IServiceCollection services)
    {
        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
        return services;
    }
}
