using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.ExternalServices.Cache;

public static class RedisExternalServiceExtensions
{
    public static IServiceCollection AddRedisExternalService(
        this IServiceCollection services,
        IConfiguration configuration,
        ISignalRServerBuilder? signalRBuilder = null)
    {
        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
            ?? configuration["Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 10000;
            redisOptions.KeepAlive = 60;
            redisOptions.SyncTimeout = 10000;

            var redisConnection = ConnectionMultiplexer.Connect(redisOptions);
            services.AddSingleton<IConnectionMultiplexer>(redisConnection);

            signalRBuilder?.AddStackExchangeRedis(options =>
            {
                options.ConnectionFactory = async writer => await Task.FromResult(redisConnection);
                options.Configuration.ChannelPrefix = RedisChannel.Literal("GigBridge_SignalR");
            });
        }
        else
        {
            using var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
            startupLoggerFactory.CreateLogger("Startup")
                .LogWarning("Redis connection string is empty or missing! SignalR backplane disabled.");
        }

        return services;
    }
}
