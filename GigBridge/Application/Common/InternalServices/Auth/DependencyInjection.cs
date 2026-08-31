using Application.Common.InternalServices.Auth.Services;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.Auth;
internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<AuthSessionOptions>, AuthSessionOptionsValidator>();
        services.AddOptions<AuthSessionOptions>()
            .Bind(configuration.GetSection(AuthSessionOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        return services;
    }
}
