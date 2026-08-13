using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Templates;
using Application.Common.Interfaces.Time;
using Application.Features.Auth.Common.Interfaces;
using Application.Features.Chat.Common.Interfaces;
using Application.Features.ESign.Common.Interfaces;
using Application.Features.JobPosts.Common.ContentModeration;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Wallets.Common.Interfaces;
using Application.Common.Models;
using System.Net;
using System.Net.Sockets;
using Application.Features.Chat.Common.Schedules;
using Application.Features.Chat.Common.FinalOffers.Shared.Email;
using Application.Features.JobInvitations.Common.Email;
using Application.Features.Proposals.Common.Email;
using Application.Common.Options;
using Infrastructure.BackgroundJobs;
using Infrastructure.ExternalServices.Ai;
using Infrastructure.ExternalServices.GoogleMeet;
using Infrastructure.ExternalServices.Payments;
using Infrastructure.Persistence;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Common;
using Infrastructure.Services.ContentModerationService;
using Infrastructure.Services.Email;
using Infrastructure.Services.ESign;
using Infrastructure.Services.GoogleMeet;
using Infrastructure.Services.Media;
using Infrastructure.Services.Notification;
using Infrastructure.Services.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using PayOS;
using Resend;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var allowLocalHttp = IsLocalEnvironment(configuration["ASPNETCORE_ENVIRONMENT"]);

        var pooledConnectionString = DatabasePoolOptions.Apply(connectionString, configuration);
        services.AddDbContext<GigbridgeDbContext>(options =>
            options.UseNpgsql(pooledConnectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<GigbridgeDbContext>());
        services.AddScoped<IDeliveryOutboxStore, DeliveryOutboxStore>();

        services
            .AddOptions<PayOsOptions>()
            .Bind(configuration.GetSection(PayOsOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ClientId))
                {
                    options.ClientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
                }

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
                }

                if (string.IsNullOrWhiteSpace(options.ChecksumKey))
                {
                    options.ChecksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
                }

            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ChecksumKey),
                "PayOS configuration is missing. Set PAYOS_CLIENT_ID, PAYOS_API_KEY, and PAYOS_CHECKSUM_KEY.")
            .ValidateOnStart();

        var withdrawalsEnabled = configuration.GetValue<bool>("WalletWithdrawals:Enabled");
        services
            .AddOptions<PayOsPayoutOptions>()
            .Bind(configuration.GetSection(PayOsPayoutOptions.SectionName))
            .PostConfigure(options =>
            {
                options.ClientId = ReadFallback(options.ClientId, "PAYOS_PAYOUT_CLIENT_ID");
                options.ApiKey = ReadFallback(options.ApiKey, "PAYOS_PAYOUT_API_KEY");
                options.ChecksumKey = ReadFallback(options.ChecksumKey, "PAYOS_PAYOUT_CHECKSUM_KEY");
                options.ProxyUrl = ReadFallback(options.ProxyUrl, "PAYOS_PAYOUT_PROXY_URL");
            })
            .Validate(
                options => !withdrawalsEnabled ||
                    (!string.IsNullOrWhiteSpace(options.ClientId) &&
                        !string.IsNullOrWhiteSpace(options.ApiKey) &&
                        !string.IsNullOrWhiteSpace(options.ChecksumKey)),
                "PayOS payout configuration is missing. Set PAYOS_PAYOUT_CLIENT_ID, PAYOS_PAYOUT_API_KEY, and PAYOS_PAYOUT_CHECKSUM_KEY.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ProxyUrl) ||
                    Uri.TryCreate(options.ProxyUrl, UriKind.Absolute, out var proxyUri) &&
                    (proxyUri.Scheme == Uri.UriSchemeHttp || proxyUri.Scheme == Uri.UriSchemeHttps),
                "PayOS payout proxy URL must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services
            .AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    var baseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_BASE_URL");
                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        options.BaseUrl = baseUrl;
                    }
                }

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    var apiKey = Environment.GetEnvironmentVariable("AI_SERVICE_API_KEY");
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        options.ApiKey = apiKey;
                    }
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey),
                "AI Service configuration is missing. Set AI_SERVICE_BASE_URL and AI_SERVICE_API_KEY.")
            .Validate(
                options => IsAllowedServiceUri(options.BaseUrl, allowLocalHttp),
                "AI Service base URL must use HTTPS, except for HTTP loopback URLs in local environments.")
            .ValidateOnStart();

        services
            .AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .PostConfigure(options =>
            {
                var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
                if (!string.IsNullOrWhiteSpace(cloudName))
                {
                    options.CloudName = cloudName;
                }

                var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    options.ApiKey = apiKey;
                }

                var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");
                if (!string.IsNullOrWhiteSpace(apiSecret))
                {
                    options.ApiSecret = apiSecret;
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.CloudName) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ApiSecret),
                "Cloudinary configuration is missing. Set CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, and CLOUDINARY_API_SECRET.")
            .ValidateOnStart();

        // Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();

        var resendApiToken = configuration["Resend:ApiToken"]
            ?? Environment.GetEnvironmentVariable("RESEND_API_TOKEN");
        if (string.IsNullOrWhiteSpace(resendApiToken))
        {
            throw new InvalidOperationException(
                "Resend configuration is missing. Set Resend:ApiToken in appsettings or environment variable RESEND_API_TOKEN.");
        }

        services.AddResend(options =>
        {
            options.ApiToken = resendApiToken;
        });
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<ITemplateReader, FileSystemTemplateReader>();
        services.AddHttpClient<IContractEsignDocumentGenerator, ContractEsignDocumentGenerator>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IWordToPdfConverter, WordToPdfConverter>();
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddSingleton<IScheduleEmailRenderer, ScheduleEmailRenderer>();
        services.AddSingleton<ISignedEmailRenderer, SignedEmailRenderer>();
        services.AddSingleton<IProposalNegotiationEmailRenderer, ProposalNegotiationEmailRenderer>();
        services.AddSingleton<IJobAcceptanceEmailRenderer, JobAcceptanceEmailRenderer>();
        services.AddSingleton<IJobInvitationEmailRenderer, JobInvitationEmailRenderer>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddScoped<IContentModerationService, ContentModerationService>();
        services.AddScoped<IWalletTopUpPaymentService, PayOsWalletTopUpPaymentService>();
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();
        services.AddScoped<IBankAccountProtector, BankAccountProtector>();
        services.AddMemoryCache();
        services.AddHttpClient<ISupportedBankDirectory, VietQrBankDirectory>(client =>
        {
            client.BaseAddress = new Uri("https://api.vietqr.io/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IPayOsPaymentLinkClient>(provider =>
            new PayOsPaymentLinkClient(provider.GetRequiredKeyedService<PayOSClient>("OrderClient")));
        services.AddScoped<IPayoutProvider>(provider =>
            new PayOsPayoutProvider(
                provider.GetRequiredKeyedService<PayOSClient>("PayoutClient"),
                provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));

        // AI Service Client
        services.AddHttpClient<IAiServiceClient, AiServiceClient>();

        // External payment service
        services.AddKeyedSingleton("OrderClient", (sp, key) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOsOptions>>().Value;
            return new PayOSClient(new PayOSOptions
            {
                ClientId = options.ClientId,
                ApiKey = options.ApiKey,
                ChecksumKey = options.ChecksumKey,
                LogLevel = LogLevel.Warning,
            });
        });
        services.AddKeyedSingleton("PayoutClient", (sp, key) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOsPayoutOptions>>().Value;
            var clientOptions = new PayOSOptions
            {
                ClientId = options.ClientId,
                ApiKey = options.ApiKey,
                ChecksumKey = options.ChecksumKey,
                LogLevel = LogLevel.Warning,
                MaxRetries = 0,
                TimeoutMs = 20_000
            };
            clientOptions.HttpClient = string.IsNullOrWhiteSpace(options.ProxyUrl)
                ? new HttpClient(CreatePayoutDirectHandler())
                : new HttpClient(CreatePayoutProxyHandler(options.ProxyUrl));

            return new PayOSClient(clientOptions);
        });

        services
            .AddOptions<GoogleMeetOptions>()
            .Bind(configuration.GetSection(GoogleMeetOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ClientId))
                    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE_MEET_CLIENT_ID")
                        ?? configuration["Authentication:Google:ClientId"]
                        ?? string.Empty;

                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    options.ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_MEET_CLIENT_SECRET")
                        ?? configuration["Authentication:Google:ClientSecret"]
                        ?? string.Empty;

                if (string.IsNullOrWhiteSpace(options.BackendCallbackUri))
                    options.BackendCallbackUri = Environment.GetEnvironmentVariable("GOOGLE_MEET_BACKEND_CALLBACK_URI")
                        ?? (allowLocalHttp
                            ? "http://localhost:5222/api/integrations/google-meet/callback"
                            : string.Empty);

                if (string.IsNullOrWhiteSpace(options.FrontendCallbackUri))
                {
                    var frontendBaseUrl = configuration["FrontendBaseUrl"]?.TrimEnd('/');
                    options.FrontendCallbackUri = Environment.GetEnvironmentVariable("GOOGLE_MEET_FRONTEND_CALLBACK_URI")
                        ?? (string.IsNullOrWhiteSpace(frontendBaseUrl)
                            ? string.Empty
                            : $"{frontendBaseUrl}/integrations/google-meet/callback");
                }

                if (string.IsNullOrWhiteSpace(options.MeetApiBaseUrl))
                    options.MeetApiBaseUrl = "https://meet.googleapis.com";
            })
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret) &&
                !string.IsNullOrWhiteSpace(options.BackendCallbackUri) &&
                !string.IsNullOrWhiteSpace(options.FrontendCallbackUri),
                "Google Meet configuration is missing. Set client credentials and both backend/frontend callback URIs.")
            .Validate(options =>
                IsAllowedServiceUri(options.AuthorizationEndpoint, allowLocalHttp) &&
                IsAllowedServiceUri(options.TokenEndpoint, allowLocalHttp) &&
                IsAllowedServiceUri(options.RevocationEndpoint, allowLocalHttp) &&
                IsAllowedServiceUri(options.MeetApiBaseUrl, allowLocalHttp) &&
                IsAllowedServiceUri(options.BackendCallbackUri, allowLocalHttp) &&
                IsAllowedServiceUri(options.FrontendCallbackUri, allowLocalHttp),
                "Google Meet endpoints and callback URIs must use HTTPS, except for HTTP loopback URLs in local environments.")
            .ValidateOnStart();

        services.AddHttpClient("GoogleMeetOAuth")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

        services.AddHttpClient<IGoogleMeetApiClient, GoogleMeetApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<GoogleMeetIdTokenValidator>();
        services.AddScoped<IGoogleMeetOAuthService, GoogleMeetOAuthService>();
        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddHostedService<GoogleMeetProvisioningWorker>();
            services.AddHostedService<PremiumExpiryWorker>();
            services.AddHostedService<AnalyticsMaintenanceWorker>();
        }

        // Data Protection for encrypted tokens
        services.AddDataProtection()
            .SetApplicationName("GigBridge")
            .PersistKeysToDbContext<GigbridgeDbContext>();

        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<GigbridgeDbContext>("Database");

        return services;
    }

    private static string? ReadFallback(string? configuredValue, string environmentVariable) =>
        string.IsNullOrWhiteSpace(configuredValue)
            ? Environment.GetEnvironmentVariable(environmentVariable)
            : configuredValue;

    private static bool IsLocalEnvironment(string? environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedServiceUri(string? value, bool allowLocalHttp)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            || (allowLocalHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    internal static SocketsHttpHandler CreatePayoutDirectHandler() => new()
    {
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    internal static HttpClientHandler CreatePayoutProxyHandler(string proxyUrl)
    {
        var proxyUri = new Uri(proxyUrl);
        var address = new UriBuilder(proxyUri) { UserName = string.Empty, Password = string.Empty }.Uri;
        var proxy = new WebProxy(address);
        if (!string.IsNullOrWhiteSpace(proxyUri.UserInfo))
        {
            var credentials = proxyUri.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(
                Uri.UnescapeDataString(credentials[0]),
                credentials.Length == 2 ? Uri.UnescapeDataString(credentials[1]) : string.Empty);
        }

        return new HttpClientHandler { Proxy = proxy, UseProxy = true };
    }
}
