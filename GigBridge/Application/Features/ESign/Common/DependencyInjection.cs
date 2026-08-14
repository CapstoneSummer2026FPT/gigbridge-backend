using Application.Features.ESign.Common.Email;
using Application.Features.ESign.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.ESign.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddESignServices(this IServiceCollection services)
    {
        services.AddSingleton<ISignedEmailRenderer, SignedEmailRenderer>();
        return services;
    }
}
