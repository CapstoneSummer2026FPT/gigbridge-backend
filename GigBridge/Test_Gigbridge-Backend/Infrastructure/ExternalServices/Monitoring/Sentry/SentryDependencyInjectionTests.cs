using Application.Common.Interfaces.Monitoring;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Infrastructure;
using Infrastructure.ExternalServices.Monitoring.Sentry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Project_API;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Monitoring.Sentry;

public sealed class SentryDependencyInjectionTests
{
    [Fact]
    public void AddSentryExternalService_AlwaysRegistersIssueSource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        services.AddSentryExternalService(new ConfigurationBuilder().Build());

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ISystemErrorSource) &&
                descriptor.ImplementationFactory is not null &&
                descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IExceptionReporter));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<SentryIssueErrorSource>(
            provider.GetRequiredService<ISystemErrorSource>());
    }

    [Fact]
    public async Task ConfiguredDsn_RegistersReporterAndSentryHub()
    {
        var builder = CreateBuilder();
        builder.Configuration["Sentry:Dsn"] = "https://public@example.com/1";
        builder.WebHost.UseInfrastructureMonitoring(builder.Configuration, builder.Environment);
        builder.Services.AddSentryExternalService(builder.Configuration);

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.IsType<SentryExceptionReporter>(
            scope.ServiceProvider.GetRequiredService<IExceptionReporter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<global::Sentry.IHub>());
    }

    [Fact]
    public async Task MissingDsn_AllowsHostToBuildWithoutReporter()
    {
        var builder = CreateBuilder();
        builder.WebHost.UseInfrastructureMonitoring(builder.Configuration, builder.Environment);
        builder.Services.AddSentryExternalService(builder.Configuration);

        await using var app = builder.Build();

        Assert.Null(app.Services.GetService<IExceptionReporter>());
    }

    private static WebApplicationBuilder CreateBuilder() =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
}
