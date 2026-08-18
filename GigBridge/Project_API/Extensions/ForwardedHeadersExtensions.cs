using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Project_API.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedProxyForwarding(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = null;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
        });
        return services;
    }
}
