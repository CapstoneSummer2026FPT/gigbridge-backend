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
            options.ForwardLimit = 1;
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                IPAddress.Parse("172.16.0.0"), 12));
        });
        return services;
    }
}
