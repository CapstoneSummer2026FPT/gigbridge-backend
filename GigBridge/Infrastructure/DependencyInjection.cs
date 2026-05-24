using Application.Common.Interfaces;
using Application.Common.Models;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Services.Admin;
using Infrastructure.Services.Admin.Interfaces;
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

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddScoped<JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminJobPostService, AdminJobPostService>();
        services.AddScoped<IAdminReviewService, AdminReviewService>();
        services.AddScoped<IAdminReportService, AdminReportService>();
        services.AddScoped<IAdminDisputeService, AdminDisputeService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<IAdminAuditLogService, AdminAuditLogService>();

        // Options
        services.Configure<CloudinaryOptions>(configuration.GetSection("Cloudinary"));

        // External payment service
        services.AddKeyedSingleton("OrderClient", (sp, key) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new PayOSClient(new PayOSOptions
            {
                ClientId = config["PayOS:ClientId"] ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID"),
                ApiKey = config["PayOS:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY"),
                ChecksumKey = config["PayOS:ChecksumKey"] ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY"),
                LogLevel = LogLevel.Debug,
            });
        });

        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<GigbridgeDbContext>("Database");

        return services;
    }
}
