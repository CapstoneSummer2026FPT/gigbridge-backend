using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Application.Common.Interfaces.IService;
namespace Project_API.Extensions;


public static class CacheExtensions {
    public static IServiceCollection AddHybridCache(this IServiceCollection services, IConfiguration configuration) {
        services.AddMemoryCache();

        // Ưu tiên env var, fallback xuống config, cuối cùng là localhost cho dev
        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
            ?? configuration["Redis:ConnectionString"]
            ?? "localhost:6379";

        // Không throw nếu Redis không available ở dev/testing
        services.AddStackExchangeRedisCache(options => {
            options.Configuration = redisConnectionString;
            options.InstanceName = "GigBridge_";
        });

        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}