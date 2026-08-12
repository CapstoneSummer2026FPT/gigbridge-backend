using System.Reflection;
using Application.Common.Behaviours;
using Application.Common.Interfaces.IService;
using Application.Common.Mappings;
using Application.Common.Options;
using Application.Common.Services;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using Application.Features.Chat.Common.Schedules;
using Application.Features.MarketplaceAnalytics.Common.Services;
using Application.Features.Reviews.Common.Moderation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


namespace Application;

public static class DependencyInjection
{

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<DeliveryOutboxOptions>, DeliveryOutboxOptionsValidator>();
        services
            .AddOptions<DeliveryOutboxOptions>()
            .Bind(
                configuration.GetSection(DeliveryOutboxOptions.SectionName),
                binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();

        services.Configure<WalletWithdrawalOptions>(options =>
        {
            var section = configuration.GetSection(WalletWithdrawalOptions.SectionName);
            options.Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled;
            options.MinTokens = ReadDecimal(section["MinTokens"], options.MinTokens);
            options.MaxTokens = ReadDecimal(section["MaxTokens"], options.MaxTokens);
            options.DailyMaxTokens = ReadDecimal(section["DailyMaxTokens"], options.DailyMaxTokens);
            options.FixedFeeVnd = ReadDecimal(section["FixedFeeVnd"], options.FixedFeeVnd);
            options.Provider = string.IsNullOrWhiteSpace(section["Provider"]) ? options.Provider : section["Provider"]!;
            options.OutboxBatchSize = ReadInt(section["OutboxBatchSize"], options.OutboxBatchSize);
            options.ProcessingTimeoutMinutes = ReadInt(section["ProcessingTimeoutMinutes"], options.ProcessingTimeoutMinutes);
            options.SyncIntervalMinutes = ReadInt(section["SyncIntervalMinutes"], options.SyncIntervalMinutes);

            if (options.MinTokens <= 0) options.MinTokens = 10m;
            if (options.MaxTokens <= 0) options.MaxTokens = 100_000m;
            if (options.DailyMaxTokens <= 0) options.DailyMaxTokens = 500_000m;
            if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "PayOS";
            if (options.OutboxBatchSize <= 0) options.OutboxBatchSize = 20;
            if (options.ProcessingTimeoutMinutes <= 0) options.ProcessingTimeoutMinutes = 10;
            if (options.SyncIntervalMinutes <= 0) options.SyncIntervalMinutes = 5;
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = configuration["MediatR:LicenseKey"];

            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        services.AddAutoMapper(cfg =>
        {
            cfg.LicenseKey = configuration["AutoMapper:LicenseKey"];
        }, typeof(MappingProfile));
        services.AddScoped<ScheduleWorkflowService>();
        services.AddScoped<IUserAccountStatusService, UserAccountStatusService>();
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        services.AddScoped<IUserAuditLogService, UserAuditLogService>();
        services.AddScoped<IUserEloService, UserEloService>();
        services.AddScoped<IReviewModerationService, ReviewModerationService>();
        services.AddScoped<IPremiumAccessService, PremiumAccessService>();
        services.AddScoped<IProposalQuestionTimerService, ProposalQuestionTimerService>();
        services.AddScoped<IProposalInterviewReviewService, ProposalInterviewReviewService>();
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
        services.AddScoped<IMarketplaceAnalyticsRecorder, MarketplaceAnalyticsRecorder>();
        services.AddSingleton<DeliveryOutboxService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DeliveryOutboxService>());

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddSingleton<DeadlineWarningService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DeadlineWarningService>());

            services.AddSingleton<PayoutOutboxWorker>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PayoutOutboxWorker>());

            services.AddSingleton<ContractAutoCompletionWorker>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ContractAutoCompletionWorker>());
        }

        return services;
    }

    private static decimal ReadDecimal(string? value, decimal fallback)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ReadInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
