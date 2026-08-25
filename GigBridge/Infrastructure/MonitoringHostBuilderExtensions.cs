using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class MonitoringHostBuilderExtensions
{
    public static IWebHostBuilder UseInfrastructureMonitoring(
        this IWebHostBuilder webHostBuilder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var sentryDsn = configuration["Sentry:Dsn"]?.Trim();
        if (string.IsNullOrWhiteSpace(sentryDsn))
        {
            return webHostBuilder;
        }

        var configuredEnvironment = configuration["Sentry:Environment"]?.Trim();
        var configuredRelease = configuration["Sentry:Release"]?.Trim();
        webHostBuilder.UseSentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = string.IsNullOrWhiteSpace(configuredEnvironment)
                ? environment.EnvironmentName.ToLowerInvariant()
                : configuredEnvironment;
            options.Release = string.IsNullOrWhiteSpace(configuredRelease)
                ? null
                : configuredRelease;
            options.SendDefaultPii = false;
            options.MinimumEventLevel = LogLevel.None;
            options.MinimumBreadcrumbLevel = LogLevel.Information;
            options.TracesSampleRate = Math.Clamp(
                configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0d,
                0d,
                1d);
        });

        return webHostBuilder;
    }
}
