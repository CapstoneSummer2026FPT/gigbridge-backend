using Application.Common.InternalServices.ESign.Email;
using Application.Common.InternalServices.ESign.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common.InternalServices.ESign;
internal static class DependencyInjection
{
    internal static IServiceCollection AddESignServices(this IServiceCollection services)
    {
        services.AddSingleton<ISignedEmailRenderer, SignedEmailRenderer>();
        return services;
    }
}
