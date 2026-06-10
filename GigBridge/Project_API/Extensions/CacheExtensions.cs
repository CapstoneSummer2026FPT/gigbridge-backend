using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Application.Common.Interfaces.IService;
namespace Project_API.Extensions;


public static class CacheExtensions {
    public static IServiceCollection AddHybridCache(this IServiceCollection services, IConfiguration configuration) {
        services.AddMemoryCache();

        var redisConnectionString = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnectionString)) {
            redisConnectionString = "localhost:6379";
        }

        services.AddStackExchangeRedisCache(options => {
            options.Configuration = redisConnectionString;
            options.InstanceName = "GigBridge_";
        });

        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}