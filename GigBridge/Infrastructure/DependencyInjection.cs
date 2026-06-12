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
using Microsoft.Extensions.Options;
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

        services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));
        services.PostConfigure<PayOsOptions>(options =>
        {
            options.ClientId ??= Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
            options.ApiKey ??= Environment.GetEnvironmentVariable("PAYOS_API_KEY");
            options.ChecksumKey ??= Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
        });

        // Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddTransient<IDateTimeService, DateTimeService>();
        services.AddScoped<IWalletTopUpPaymentService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PayOsOptions>>().Value;
            if (options.UseMock)
            {
                return ActivatorUtilities.CreateInstance<MockWalletTopUpPaymentService>(provider);
            }

            return ActivatorUtilities.CreateInstance<PayOsWalletTopUpPaymentService>(provider);
        });
        services.AddScoped<IPayOsPaymentLinkClient>(provider =>
            new PayOsPaymentLinkClient(provider.GetRequiredKeyedService<PayOSClient>("OrderClient")));


        // External payment service
        services.AddKeyedSingleton("OrderClient", (sp, key) =>
        {
            var options = sp.GetRequiredService<IOptions<PayOsOptions>>().Value;
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
