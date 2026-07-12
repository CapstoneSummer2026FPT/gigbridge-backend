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
        using var scope = _scopeFactory.CreateScope();
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
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IPayoutProvider>();
        var dateTimeService = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var syncBefore = dateTimeService.UtcNow.AddMinutes(-Math.Max(1, _options.SyncIntervalMinutes));

        var withdrawals = await context.Set<WalletWithdrawal>()
            .Where(withdrawal =>
                (withdrawal.Status == (int)WithdrawalStatus.Processing ||
                    withdrawal.Status == (int)WithdrawalStatus.SyncRequired) &&
                (withdrawal.LastSyncedAt == null || withdrawal.LastSyncedAt <= syncBefore))
            .OrderBy(withdrawal => withdrawal.LastSyncedAt ?? withdrawal.CreatedAt)
            .Take(Math.Clamp(_options.OutboxBatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var withdrawal in withdrawals)
        {
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
                    withdrawal,
                    status,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                WithdrawalWorkflow.MarkSyncRequired(
                    dateTimeService,
                    withdrawal,
                    ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

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
            var accountNumber = protector.Unprotect(withdrawal.BankAccountNumberEncrypted);
            var payout = await provider.CreatePayoutAsync(
                new PayoutCreateRequest(
                    withdrawal.WalletWithdrawalId,
                    withdrawal.ProviderOrderCode,
                    withdrawal.NetVndAmount,
                    withdrawal.BankCode,
                    accountNumber,
                    withdrawal.BankAccountName,
                    $"GigBridge withdrawal {withdrawal.ProviderOrderCode}",
                    withdrawal.ProviderOrderCode),
                cancellationToken);

            await WithdrawalWorkflow.ApplyProviderResultAsync(
                context,
                dateTimeService,
                withdrawal,
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
            var message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            WithdrawalWorkflow.MarkSyncRequired(dateTimeService, withdrawal, message);
            ScheduleRetry(outbox, message, dateTimeService.UtcNow);
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
