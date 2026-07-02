using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Features.Chat.Common.Schedules;
using Application.Features.JobInvitations.Common.Email;
using Application.Features.Proposals.Common.Email;
using Infrastructure.BackgroundJobs;
using Infrastructure.ExternalServices.Ai;
using Infrastructure.ExternalServices.GoogleMeet;
using Infrastructure.ExternalServices.Payments;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Common;
using Infrastructure.Services.Email;
using Infrastructure.Services.GoogleMeet;
using Infrastructure.Services.Media;
using Infrastructure.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayOS;
using Resend;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<GigbridgeDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<GigbridgeDbContext>());

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

                if (string.IsNullOrWhiteSpace(options.PayoutBaseUrl))
                {
                    options.PayoutBaseUrl = Environment.GetEnvironmentVariable("PAYOS_PAYOUT_BASE_URL");
                }

                if (string.IsNullOrWhiteSpace(options.PayoutCreatePath))
                {
                    options.PayoutCreatePath = Environment.GetEnvironmentVariable("PAYOS_PAYOUT_CREATE_PATH");
                }

                if (string.IsNullOrWhiteSpace(options.PayoutStatusPath))
                {
                    options.PayoutStatusPath = Environment.GetEnvironmentVariable("PAYOS_PAYOUT_STATUS_PATH");
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ChecksumKey),
                "PayOS configuration is missing. Set PAYOS_CLIENT_ID, PAYOS_API_KEY, and PAYOS_CHECKSUM_KEY.")
            .ValidateOnStart();

        services
            .AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    options.BaseUrl = Environment.GetEnvironmentVariable("AI_SERVICE_BASE_URL");
                }

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = Environment.GetEnvironmentVariable("AI_SERVICE_API_KEY");
                }
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey),
                "AI Service configuration is missing. Set AI_SERVICE_BASE_URL and AI_SERVICE_API_KEY.")
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
        services.AddResend(options =>
        {
            options.ApiToken = configuration["Resend:ApiToken"] 
                ?? Environment.GetEnvironmentVariable("RESEND_API_TOKEN") 
                ?? string.Empty;
        });
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddSingleton<IScheduleEmailRenderer, ScheduleEmailRenderer>();
        services.AddSingleton<IProposalNegotiationEmailRenderer, ProposalNegotiationEmailRenderer>();
        services.AddSingleton<IJobInvitationEmailRenderer, JobInvitationEmailRenderer>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddScoped<IContentModerationService, ContentModerationService>();
        services.AddScoped<IWalletTopUpPaymentService, PayOsWalletTopUpPaymentService>();
        services.AddScoped<IBankAccountProtector, BankAccountProtector>();
        services.AddScoped<IPayOsPaymentLinkClient>(provider =>
            new PayOsPaymentLinkClient(provider.GetRequiredKeyedService<PayOSClient>("OrderClient")));
        services.AddHttpClient<IPayoutProvider, PayOsPayoutProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

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
                LogLevel = LogLevel.Debug,
            });
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
                        ?? (string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase)
                            ? "http://localhost:5222/api/integrations/google-meet/callback"
                            : string.Empty);

                if (string.IsNullOrWhiteSpace(options.FrontendCallbackUri))
                    options.FrontendCallbackUri = Environment.GetEnvironmentVariable("GOOGLE_MEET_FRONTEND_CALLBACK_URI")
                        ?? (string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase)
                            ? $"{configuration["FrontendBaseUrl"]?.TrimEnd('/')}/integrations/google-meet/callback"
                            : string.Empty);

                if (string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
                    options.DataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(options.MeetApiBaseUrl))
                    options.MeetApiBaseUrl = "https://meet.googleapis.com";
            })
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret) &&
                !string.IsNullOrWhiteSpace(options.BackendCallbackUri),
                "Google Meet configuration is missing. Set GOOGLE_MEET_CLIENT_ID, GOOGLE_MEET_CLIENT_SECRET, and GOOGLE_MEET_BACKEND_CALLBACK_URI.")
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

        services.AddScoped<IGoogleMeetOAuthService, GoogleMeetOAuthService>();
        services.AddHostedService<GoogleMeetProvisioningWorker>();

        // Data Protection for encrypted tokens
        services.AddDataProtection();

        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<GigbridgeDbContext>("Database");

        return services;
    }
}
