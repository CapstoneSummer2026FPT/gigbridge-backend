using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Infrastructure.ExternalServices.Payments;
using Infrastructure.Persistence;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Common;
using Infrastructure.Services.Email;
using Infrastructure.Services.Media;
using Infrastructure.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayOS;

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
            })
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ClientId) &&
                    !string.IsNullOrWhiteSpace(options.ApiKey) &&
                    !string.IsNullOrWhiteSpace(options.ChecksumKey),
                "PayOS configuration is missing. Set PAYOS_CLIENT_ID, PAYOS_API_KEY, and PAYOS_CHECKSUM_KEY.")
            .ValidateOnStart();

        // Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddScoped<IWalletTopUpPaymentService, PayOsWalletTopUpPaymentService>();
        services.AddScoped<IPayOsPaymentLinkClient>(provider =>
            new PayOsPaymentLinkClient(provider.GetRequiredKeyedService<PayOSClient>("OrderClient")));


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

        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<GigbridgeDbContext>("Database");

        return services;
    }
}
