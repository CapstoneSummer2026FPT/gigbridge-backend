using Application.Common.Options;
using Application.Features.Wallets.Common.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application.Features.Wallets.Common;

internal static class DependencyInjection
{
    internal static IServiceCollection AddWalletServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        if (BackgroundWorkerOptions.IsEnabled(configuration))
        {
            services.AddSingleton<PayoutOutboxWorker>();
            services.AddSingleton<IHostedService>(provider =>
                provider.GetRequiredService<PayoutOutboxWorker>());
        }

        return services;
    }

    private static decimal ReadDecimal(string? value, decimal fallback) =>
        decimal.TryParse(value, out var parsed) ? parsed : fallback;

    private static int ReadInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;
}
