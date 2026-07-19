using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Options;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Common.Services;

public sealed class PayoutOutboxWorker : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PayoutOutboxWorker> _logger;
    private readonly WalletWithdrawalOptions _options;

    public PayoutOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PayoutOutboxWorker> logger,
        IOptions<WalletWithdrawalOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await SyncStaleWithdrawalsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Payout outbox batch failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;

        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPayoutProvider>();
        if (!(await provider.CheckAvailabilityAsync(cancellationToken)).IsAvailable) return;

        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = DateTime.UtcNow;
        var timeout = now.AddMinutes(_options.ProcessingTimeoutMinutes);

        await context.Set<PayoutOutbox>()
            .Where(outbox =>
                outbox.Status == (int)PayoutOutboxStatus.Processing &&
                outbox.NextAttemptAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(outbox => outbox.Status, (int)PayoutOutboxStatus.Pending)
                    .SetProperty(outbox => outbox.NextAttemptAt, now),
                cancellationToken);

        var candidateIds = await context.Set<PayoutOutbox>()
            .AsNoTracking()
            .Where(outbox =>
                outbox.Status == (int)PayoutOutboxStatus.Pending &&
                outbox.NextAttemptAt <= now)
            .OrderBy(outbox => outbox.NextAttemptAt)
            .Select(outbox => outbox.PayoutOutboxId)
            .Take(Math.Clamp(_options.OutboxBatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var outboxId in candidateIds)
        {
            var claimed = await context.Set<PayoutOutbox>()
                .Where(outbox =>
                    outbox.PayoutOutboxId == outboxId &&
                    outbox.Status == (int)PayoutOutboxStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(outbox => outbox.Status, (int)PayoutOutboxStatus.Processing)
                        .SetProperty(outbox => outbox.NextAttemptAt, timeout),
                    cancellationToken);

            if (claimed == 0)
            {
                continue;
            }

            await ProcessOutboxAsync(scope.ServiceProvider, outboxId, cancellationToken);
        }
    }

    internal async Task SyncStaleWithdrawalsAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IPayoutProvider>();
        if (!(await provider.CheckAvailabilityAsync(cancellationToken)).IsAvailable) return;

        var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var now = dateTimeService.UtcNow;
        var syncBefore = now.AddMinutes(-Math.Max(1, _options.SyncIntervalMinutes));

        var withdrawals = await context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .Where(withdrawal =>
                (withdrawal.Status == (int)WithdrawalStatus.Processing ||
                    withdrawal.Status == (int)WithdrawalStatus.SyncRequired) &&
                (withdrawal.LastSyncedAt == null || withdrawal.LastSyncedAt <= syncBefore))
            .OrderBy(withdrawal => withdrawal.LastSyncedAt ?? withdrawal.CreatedAt)
            .Take(Math.Clamp(_options.OutboxBatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var withdrawal in withdrawals)
        {
            var delay = GetReconciliationDelay(withdrawal.CreatedAt, now);
            if (withdrawal.LastSyncedAt.HasValue && withdrawal.LastSyncedAt.Value > now.Subtract(delay))
            {
                continue;
            }

            try
            {
                var status = await provider.GetPayoutStatusAsync(
                    new PayoutStatusRequest(
                        withdrawal.WalletWithdrawalId,
                        withdrawal.ProviderOrderCode,
                        withdrawal.ProviderPayoutId),
                    cancellationToken);

                await WithdrawalWorkflow.ApplyProviderResultAsync(
                    context,
                    dateTimeService,
                    withdrawal.WalletWithdrawalId,
                    status,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await WithdrawalWorkflow.ApplyProviderResultAsync(
                    context,
                    dateTimeService,
                    withdrawal.WalletWithdrawalId,
                    new PayoutProviderResult(
                        PayoutProviderOutcome.SyncRequired,
                        withdrawal.ProviderPayoutId,
                        withdrawal.ProviderTransactionCode,
                        withdrawal.ProviderRawStatus,
                        "Provider sync failed."),
                    cancellationToken);
            }
        }
    }

    private TimeSpan GetReconciliationDelay(DateTime createdAt, DateTime now)
    {
        var minimum = TimeSpan.FromMinutes(Math.Max(1, _options.SyncIntervalMinutes));
        var age = now - createdAt;
        if (age < TimeSpan.FromMinutes(10)) return minimum;
        if (age < TimeSpan.FromHours(1)) return Max(minimum, TimeSpan.FromMinutes(5));
        if (age < TimeSpan.FromHours(6)) return Max(minimum, TimeSpan.FromMinutes(15));
        if (age < TimeSpan.FromDays(1)) return Max(minimum, TimeSpan.FromHours(1));
        return Max(minimum, TimeSpan.FromHours(6));
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private async Task ProcessOutboxAsync(
        IServiceProvider serviceProvider,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        var context = serviceProvider.GetRequiredService<IApplicationDbContext>();
        var provider = serviceProvider.GetRequiredService<IPayoutProvider>();
        var protector = serviceProvider.GetRequiredService<IBankAccountProtector>();
        var dateTimeService = serviceProvider.GetRequiredService<IDateTimeService>();

        var outbox = await context.Set<PayoutOutbox>()
            .FirstAsync(outbox => outbox.PayoutOutboxId == outboxId, cancellationToken);

        var withdrawal = await context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                withdrawal => withdrawal.WalletWithdrawalId == outbox.WalletWithdrawalId,
                cancellationToken);

        if (withdrawal is null)
        {
            outbox.Status = (int)PayoutOutboxStatus.DeadLettered;
            outbox.LastError = "Withdrawal does not exist.";
            outbox.ProcessedAt = dateTimeService.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (WithdrawalWorkflow.IsTerminal(withdrawal.Status))
        {
            outbox.Status = (int)PayoutOutboxStatus.Delivered;
            outbox.ProcessedAt = dateTimeService.UtcNow;
            outbox.LastError = null;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            PayoutProviderResult payout;
            if (!string.IsNullOrWhiteSpace(withdrawal.ProviderPayoutId))
            {
                payout = await provider.GetPayoutStatusAsync(
                    new PayoutStatusRequest(
                        withdrawal.WalletWithdrawalId,
                        withdrawal.ProviderOrderCode,
                        withdrawal.ProviderPayoutId),
                    cancellationToken);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(withdrawal.BankBin))
                {
                    throw new InvalidOperationException("Withdrawal bank BIN is missing.");
                }

                var accountNumber = protector.Unprotect(withdrawal.BankAccountNumberEncrypted);
                payout = await provider.CreatePayoutAsync(
                    new PayoutCreateRequest(
                        withdrawal.WalletWithdrawalId,
                        withdrawal.ProviderOrderCode,
                        withdrawal.NetVndAmount,
                        withdrawal.BankBin,
                        accountNumber,
                        withdrawal.BankAccountName,
                        ($"GigBridge WD {withdrawal.WalletWithdrawalId:N}")[..21],
                        withdrawal.WalletWithdrawalId.ToString("D")),
                    cancellationToken);
            }

            await WithdrawalWorkflow.ApplyProviderResultAsync(
                context,
                dateTimeService,
                withdrawal.WalletWithdrawalId,
                payout,
                cancellationToken);

            if (payout.Outcome == PayoutProviderOutcome.SyncRequired)
            {
                ScheduleRetry(outbox, payout.FailureReason ?? payout.RawStatus ?? "Payout requires sync.", dateTimeService.UtcNow);
            }
            else
            {
                outbox.Status = (int)PayoutOutboxStatus.Delivered;
                outbox.ProcessedAt = dateTimeService.UtcNow;
                outbox.LastError = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await WithdrawalWorkflow.ApplyProviderResultAsync(
                context,
                dateTimeService,
                withdrawal.WalletWithdrawalId,
                new PayoutProviderResult(
                    PayoutProviderOutcome.SyncRequired,
                    withdrawal.ProviderPayoutId,
                    withdrawal.ProviderTransactionCode,
                    withdrawal.ProviderRawStatus,
                    "Payout processing failed."),
                cancellationToken);
            ScheduleRetry(outbox, "Payout processing failed.", dateTimeService.UtcNow);
            _logger.LogWarning(ex, "Payout outbox {OutboxId} failed for withdrawal {WithdrawalId}.", outboxId, withdrawal.WalletWithdrawalId);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ScheduleRetry(PayoutOutbox outbox, string error, DateTime now)
    {
        outbox.AttemptCount++;
        outbox.LastError = error;

        if (outbox.AttemptCount > RetryDelays.Length)
        {
            outbox.Status = (int)PayoutOutboxStatus.DeadLettered;
            outbox.ProcessedAt = now;
            return;
        }

        outbox.Status = (int)PayoutOutboxStatus.Pending;
        outbox.NextAttemptAt = now.Add(RetryDelays[outbox.AttemptCount - 1]);
    }
}
