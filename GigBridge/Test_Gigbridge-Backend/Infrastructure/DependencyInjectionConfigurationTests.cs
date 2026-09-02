using Application;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Delivery.Interfaces;
using Application.Common.InternalServices.Receipts.BackgroundJobs;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Files;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Templates;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.Interfaces;
using Application.Common.InternalServices.Admin.Analytics.BackgroundJobs;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.Chat.Email;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Chat.Models;
using Application.Common.InternalServices.Chat.Services;
using Application.Features.Chat.Common.Schedules;
using Application.Common.InternalServices.Chat.BackgroundJobs;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.ESign.Interfaces;
using Application.Common.InternalServices.JobInvitations.Email;
using Application.Common.InternalServices.JobInvitations.Interfaces;
using Application.Common.InternalServices.JobInvitations.Models;
using Application.Common.InternalServices.JobPosts.Interfaces;
using Application.Common.InternalServices.JobPosts.Models;
using Application.Common.InternalServices.MarketplaceAnalytics.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Common.InternalServices.Premium.BackgroundJobs;
using Application.Common.InternalServices.Proposals.Email;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Models;
using Application.Common.InternalServices.Reviews.Interfaces;
using Application.Common.InternalServices.Wallets.Interfaces;
using Infrastructure.Adapters.Caching;
using Infrastructure.Adapters.Delivery;
using Infrastructure.Adapters.Files;
using Infrastructure.Adapters.Security.Auth;
using Infrastructure.Adapters.Security.Wallets;
using Infrastructure.Adapters.Templates;
using Infrastructure.Adapters.Time;
using Infrastructure.Adapters.Wallets;
using Infrastructure.ExternalServices.Ai;
using Infrastructure.ExternalServices.Banking.VietQr;
using Infrastructure.ExternalServices.Email.Resend;
using Infrastructure.ExternalServices.Google.Meet;
using Infrastructure.ExternalServices.Media.Cloudinary;
using Infrastructure.ExternalServices.Payments.PayOs;
using Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Npgsql;
using Application.Common.InternalServices.Wallets.BackgroundJobs;

namespace Test_Gigbridge_backend.Infrastructure;

public sealed class DependencyInjectionConfigurationTests
{
    [Theory]
    [InlineData("Development", null, 0)]
    [InlineData("Production", null, 9)]
    [InlineData("Production", "false", 0)]
    [InlineData("Development", "true", 9)]
    public void ApplicationWorkers_RespectEnvironmentAndExplicitOverride(
        string environment,
        string? enabled,
        int expectedHostedServices)
    {
        var services = new ServiceCollection();

        services.AddApplicationServices(Configuration(environment, enabled));

        Assert.Equal(expectedHostedServices, CountWorkers(services));
    }

    [Fact]
    public void ApplicationWorkers_DefaultToProductionWhenEnvironmentIsNotConfigured()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices(new ConfigurationBuilder().Build());

        Assert.Equal(9, CountWorkers(services));
    }

    [Theory]
    [InlineData("Development", null)]
    [InlineData("Production", "false")]
    public void PayoutConfigurationReporter_IsRegisteredEvenWhenWorkersAreDisabled(
        string environment,
        string? enabled)
    {
        var services = new ServiceCollection();

        services.AddApplicationServices(Configuration(environment, enabled));

        // Every node reports its own withdrawal configuration at startup, including nodes that do
        // not run the background workers.
        Assert.Single(services.Where(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(PayoutConfigurationReporter)));
    }

    /// <summary>
    /// Counts background workers only. PayoutConfigurationReporter is a hosted service too, but it
    /// is registered unconditionally, so it is not part of what these assertions measure.
    /// </summary>
    private static int CountWorkers(IServiceCollection services) =>
        services.Count(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType != typeof(PayoutConfigurationReporter));

    [Fact]
    public void ApplicationServices_ResolveFromModularRegistrations()
    {
        var services = new ServiceCollection();
        var configuration = Configuration("Development");
        services.AddSingleton(Substitute.For<IApplicationDbContext>());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Substitute.For<IDateTimeService>());
        services.AddSingleton(Substitute.For<ICacheService>());
        services.AddSingleton(Substitute.For<IEmailService>());
        services.AddSingleton(Substitute.For<ITemplateReader>());
        services.AddSingleton(Substitute.For<IChatRealtimeNotifier>());
        services.AddSingleton(Substitute.For<IGoogleMeetOAuthService>());
        services.AddSingleton(Substitute.For<INotificationSender>());
        services.AddSingleton(Substitute.For<IRequestMetadataAccessor>());
        services.AddLogging();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IUserAccountStatusService>());
        Assert.NotNull(provider.GetRequiredService<IUserAuditLogService>());
        Assert.NotNull(provider.GetRequiredService<IAdminAuditService>());
        Assert.NotNull(provider.GetRequiredService<IAdminAnalyticsService>());
        Assert.NotNull(provider.GetRequiredService<IUserEloService>());
        Assert.NotNull(provider.GetRequiredService<IPremiumAccessService>());
        Assert.NotNull(provider.GetRequiredService<IProposalQuestionTimerService>());
        Assert.NotNull(provider.GetRequiredService<IProposalInterviewReviewService>());
        Assert.NotNull(provider.GetRequiredService<IAuthEmailSender>());
        Assert.NotNull(provider.GetRequiredService<INotificationService>());
        Assert.NotNull(provider.GetRequiredService<IContentModerationService>());
        Assert.NotNull(provider.GetRequiredService<IMarketplaceAnalyticsRecorder>());
        Assert.NotNull(provider.GetRequiredService<IReviewModerationService>());
        Assert.NotNull(provider.GetRequiredService<ScheduleWorkflowService>());
        Assert.NotNull(provider.GetRequiredService<IMilestoneSubmissionEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<IWorkItemDeliveryEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<IScheduleEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<IJobAcceptanceEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<ISignedEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<IProposalNegotiationEmailRenderer>());
        Assert.NotNull(provider.GetRequiredService<IJobInvitationEmailRenderer>());
    }

    [Fact]
    public void Infrastructure_DoesNotRegisterApplicationWorkers()
    {
        var services = new ServiceCollection();

        global::Infrastructure.DependencyInjection.AddInfrastructureServices(
            services,
            Configuration("Production"));

        Type[] gigBridgeWorkerTypes =
        [
            typeof(GoogleMeetProvisioningWorker),
            typeof(PremiumExpiryWorker),
            typeof(AnalyticsMaintenanceWorker),
            typeof(ProjectReceiptWorker)
        ];
        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType is not null &&
                gigBridgeWorkerTypes.Contains(descriptor.ImplementationType));
    }

    [Fact]
    public void Infrastructure_RegistersAdaptersWithOriginalLifetimes()
    {
        var services = new ServiceCollection();

        global::Infrastructure.DependencyInjection.AddInfrastructureServices(
            services,
            Configuration("Development"));

        AssertRegistration<IDeliveryOutboxStore, DeliveryOutboxStore>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<ICacheService, HybridCacheService>(
            services,
            ServiceLifetime.Singleton);
        AssertRegistration<IWorkspaceUploadFilePolicy, WorkspaceUploadFilePolicy>(
            services,
            ServiceLifetime.Singleton);
        AssertRegistration<ITemplateReader, FileSystemTemplateReader>(
            services,
            ServiceLifetime.Singleton);
        AssertRegistration<IDateTimeService, DateTimeService>(
            services,
            ServiceLifetime.Transient);
        AssertRegistration<IPasswordHasher, BCryptPasswordHasher>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IJwtService, JwtService>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IBankAccountProtector, BankAccountProtector>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IWalletLedgerService, WalletLedgerService>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IEmailService, EmailService>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IMediaService, MediaService>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IWalletTopUpPaymentService, PayOsWalletTopUpPaymentService>(
            services,
            ServiceLifetime.Scoped);
        AssertRegistration<IGoogleMeetOAuthService, GoogleMeetOAuthService>(
            services,
            ServiceLifetime.Scoped);
        AssertFactoryRegistration<IAiServiceClient>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IGoogleAuthService>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IGoogleMeetApiClient>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IContractEsignDocumentGenerator>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IPayoutProvider>(services, ServiceLifetime.Scoped);
        AssertFactoryRegistration<ISupportedBankDirectory>(services, ServiceLifetime.Transient);
        Assert.Single(services.Where(descriptor => descriptor.ServiceType == typeof(ICacheService)));
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
            ["Analytics:HashKey"] = "test-analytics-hash-key",
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

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(
            services.Where(candidate => candidate.ServiceType == typeof(TService)));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static void AssertFactoryRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(
            services.Where(candidate => candidate.ServiceType == typeof(TService)));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }
}
