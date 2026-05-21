using Application.Common.Interfaces;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace Project_API.Extensions;
public static class CacheExtensions {
    public static IServiceCollection AddHybridCache(this IServiceCollection services, IConfiguration configuration) {
        services.AddMemoryCache();
        services.AddStackExchangeRedisCache(options => {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.InstanceName = "GigBridge_";
        });
        services.AddSingleton<ICacheService, HybridCacheService>();
        return services;
    }
}