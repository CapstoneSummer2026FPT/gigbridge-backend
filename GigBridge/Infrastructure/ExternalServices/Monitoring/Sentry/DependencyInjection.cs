using Application.Common.Interfaces.Monitoring;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.ExternalServices.Monitoring.Sentry;

internal static class DependencyInjection
{
    internal static IServiceCollection AddSentryExternalService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SentryMonitoringOptions>(
            configuration.GetSection(SentryMonitoringOptions.SectionName));
        services.AddHttpClient<ISystemErrorSource, SentryIssueErrorSource>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });

        if (!string.IsNullOrWhiteSpace(configuration["Sentry:Dsn"]))
        {
            services.AddScoped<IExceptionReporter, SentryExceptionReporter>();
        }

        return services;
    }
}
