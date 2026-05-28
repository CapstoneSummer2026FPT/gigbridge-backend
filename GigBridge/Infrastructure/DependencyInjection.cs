using Application.Common.Interfaces;
using Application.Common.Interfaces.IRepository;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services.Auth;
using Infrastructure.Services.BackgroundJobs;
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

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();


        // Services
        services.AddScoped<IPasswordHasher, Infrastructure.Services.Auth.BCryptPasswordHasher>();
        services.AddScoped<IJwtService, Infrastructure.Services.Auth.JwtService>();
        services.AddScoped<IAuthService, Infrastructure.Services.Auth.AuthService>();
        services.AddScoped<IGoogleAuthService, Infrastructure.Services.Auth.GoogleAuthService>();
        services.AddScoped<IEmailService, Infrastructure.Services.Email.EmailService>();
        services.AddScoped<IMediaService, Infrastructure.Services.Media.MediaService>();
        services.AddScoped<INotificationService, Infrastructure.Services.Notification.NotificationService>();
        services.AddTransient<IDateTimeService, Infrastructure.Services.Common.DateTimeService>();
        services.AddScoped<IBackgroundJobService, Infrastructure.Services.BackgroundJobs.HangfireJobService>();

        

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
