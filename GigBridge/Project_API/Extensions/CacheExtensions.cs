using Application.Common.Interfaces.IService;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Project_API.Extensions;

public static class CacheExtensions
{
    public static IServiceCollection AddHybridCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(redisConnection))
        {
            throw new InvalidOperationException(
                "Redis connection string 'Redis' is not configured. " +
                "Set ConnectionStrings:Redis in appsettings or environment variables.");
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "GigBridge_";
        });

        services.AddSingleton<ICacheService, HybridCacheService>();
        return services;
    }
}
