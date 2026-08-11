using Application;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Test_Gigbridge_backend.Infrastructure;

public sealed class DependencyInjectionConfigurationTests
{
    [Theory]
    [InlineData("Development", null, 0)]
    [InlineData("Production", null, 4)]
    [InlineData("Production", "false", 0)]
    [InlineData("Development", "true", 4)]
    public void ApplicationWorkers_RespectEnvironmentAndExplicitOverride(
        string environment,
        string? enabled,
        int expectedHostedServices)
    {
        var services = new ServiceCollection();

        services.AddApplicationServices(Configuration(environment, enabled));

        Assert.Equal(
            expectedHostedServices,
            services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void ApplicationWorkers_DefaultToProductionWhenEnvironmentIsNotConfigured()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices(new ConfigurationBuilder().Build());

        Assert.Equal(
            4,
            services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [Theory]
    [InlineData("Development", null, 0)]
    [InlineData("Production", null, 3)]
    [InlineData("Production", "false", 0)]
    [InlineData("Development", "true", 3)]
    public void InfrastructureWorkers_RespectEnvironmentAndExplicitOverride(
        string environment,
        string? enabled,
        int expectedHostedServices)
    {
        var services = new ServiceCollection();

        global::Infrastructure.DependencyInjection.AddInfrastructureServices(
            services,
            Configuration(environment, enabled));

        Type[] gigBridgeWorkerTypes =
        [
            typeof(GoogleMeetProvisioningWorker),
            typeof(PremiumExpiryWorker),
            typeof(AnalyticsMaintenanceWorker)
        ];
        Assert.Equal(
            expectedHostedServices,
            services.Count(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType is not null &&
                gigBridgeWorkerTypes.Contains(descriptor.ImplementationType)));
    }

    [Fact]
    public void DatabasePool_AppliesSafeDefaults()
    {
        const string connectionString =
            "Host=localhost;Database=gigbridge;Username=postgres;Password=test";

        var configured = DatabasePoolOptions.Apply(
            connectionString,
            Configuration("Production"));
        var builder = new NpgsqlConnectionStringBuilder(configured);

        Assert.Equal(5, builder.MaxPoolSize);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(60, builder.ConnectionIdleLifetime);
        Assert.Equal(10, builder.ConnectionPruningInterval);
        Assert.Equal("GigBridge-Production", builder.ApplicationName);
    }

    [Fact]
    public void DatabasePool_AppliesExplicitLimits()
    {
        const string connectionString =
            "Host=localhost;Database=gigbridge;Username=postgres;Password=test";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["DatabasePool:MaxPoolSize"] = "3",
                ["DatabasePool:ConnectionIdleLifetimeSeconds"] = "30",
                ["DatabasePool:ApplicationName"] = "GigBridge-Worker"
            })
            .Build();

        var configured = DatabasePoolOptions.Apply(connectionString, configuration);
        var builder = new NpgsqlConnectionStringBuilder(configured);

        Assert.Equal(3, builder.MaxPoolSize);
        Assert.Equal(30, builder.ConnectionIdleLifetime);
        Assert.Equal("GigBridge-Worker", builder.ApplicationName);
    }

    [Fact]
    public void DatabasePool_RejectsInvalidLimits()
    {
        const string connectionString =
            "Host=localhost;Database=gigbridge;Username=postgres;Password=test";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabasePool:MaxPoolSize"] = "0"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabasePoolOptions.Apply(connectionString, configuration));

        Assert.Contains("MaxPoolSize", exception.Message);
    }

    private static IConfiguration Configuration(
        string environment,
        string? workersEnabled = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environment,
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Database=gigbridge;Username=postgres;Password=test",
            ["Resend:ApiToken"] = "test-token"
        };
        if (workersEnabled is not null)
        {
            values["BackgroundWorkers:Enabled"] = workersEnabled;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
