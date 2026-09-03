using Application.Common.InternalServices.Wallets.Models;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.BackgroundJobs;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using Domain.Enums.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.Wallets.BackgroundJobs;
public sealed class PayoutOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MaxIdlePollInterval = TimeSpan.FromMinutes(1);
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

    /// <summary>
    /// Last observed provider availability. The worker polls every 20-60 seconds, so logging every
    /// blocked pass would bury the log. Only transitions are reported at warning level.
    /// </summary>
    private bool? _lastAvailability;

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
        if (!_options.Enabled)
        {
            _logger.LogWarning(
                "Payout outbox worker started but WalletWithdrawals:Enabled is false. " +
                "No withdrawal will ever be sent to {Provider}.",
                _options.Provider);
        }
        else
        {
            _logger.LogInformation(
                "Payout outbox worker started. Provider={Provider} BatchSize={BatchSize} " +
                "SyncIntervalMinutes={SyncIntervalMinutes}",
                _options.Provider,
                _options.OutboxBatchSize,
                _options.SyncIntervalMinutes);
        }

        var idleInterval = PollInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedWork = false;
            try
            {
                var processedOutbox = await ProcessBatchAsync(stoppingToken);
                var syncedWithdrawals = await SyncStaleWithdrawalsAsync(stoppingToken);
                processedWork = processedOutbox || syncedWithdrawals;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Payout outbox batch failed.");
            }

            var delay = processedWork ? PollInterval : idleInterval;
            idleInterval = processedWork
                ? PollInterval
                : NextIdleInterval(idleInterval, MaxIdlePollInterval);
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task<bool> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return false;

        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPayoutProvider>();
        if (!await IsProviderAvailableAsync(provider, cancellationToken)) return false;

        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = DateTime.UtcNow;
        var timeout = now.AddMinutes(_options.ProcessingTimeoutMinutes);

        // A row that hangs past the processing timeout is returned to the queue. AttemptCount is
        // incremented so a permanently-hanging row still walks the retry ladder into dead-letter
        // instead of being retried forever.
        var recovered = await context.Set<PayoutOutbox>()
            .Where(outbox =>
                outbox.Status == (int)PayoutOutboxStatus.Processing &&
                outbox.NextAttemptAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(outbox => outbox.Status, (int)PayoutOutboxStatus.Pending)
                    .SetProperty(outbox => outbox.AttemptCount, outbox => outbox.AttemptCount + 1)
                    .SetProperty(outbox => outbox.NextAttemptAt, now),
                cancellationToken);

        if (recovered > 0)
        {
            _logger.LogWarning(
                "Recovered {RecoveredCount} payout outbox row(s) stuck in Processing past the {TimeoutMinutes}-minute timeout.",
                recovered,
                _options.ProcessingTimeoutMinutes);
        }

        var candidateIds = await context.Set<PayoutOutbox>()
            .AsNoTracking()
            .Where(outbox =>
                outbox.Status == (int)PayoutOutboxStatus.Pending &&
                outbox.NextAttemptAt <= now)
            .OrderBy(outbox => outbox.NextAttemptAt)
            .Select(outbox => outbox.PayoutOutboxId)
            .Take(Math.Clamp(_options.OutboxBatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        var processed = false;
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
            processed = true;
        }

        return recovered > 0 || processed;
    }

    internal async Task<bool> SyncStaleWithdrawalsAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return false;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IPayoutProvider>();
        if (!await IsProviderAvailableAsync(provider, cancellationToken)) return false;

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

        var processed = false;
        foreach (var withdrawal in withdrawals)
        {
            var delay = GetReconciliationDelay(withdrawal.CreatedAt, now);
            if (withdrawal.LastSyncedAt.HasValue && withdrawal.LastSyncedAt.Value > now.Subtract(delay))
            {
                continue;
            }

            processed = true;
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
                _logger.LogError(
                    ex,
                    "Provider sync failed for withdrawal {WithdrawalId} (order {ProviderOrderCode}).",
                    withdrawal.WalletWithdrawalId,
                    withdrawal.ProviderOrderCode);

                await WithdrawalWorkflow.ApplyProviderResultAsync(
                    context,
                    dateTimeService,
                    withdrawal.WalletWithdrawalId,
                    new PayoutProviderResult(
                        PayoutProviderOutcome.SyncRequired,
                        withdrawal.ProviderPayoutId,
                        withdrawal.ProviderTransactionCode,
                        withdrawal.ProviderRawStatus,
                        Describe(ex)),
                    cancellationToken);
            }
        }

        return processed;
    }

    /// <summary>
    /// Gates every provider call on payout availability. A silent early return here is what let a
    /// broken PayOS credential or a non-whitelisted egress IP stall every withdrawal with no trace,
    /// so entering and leaving the unavailable state is logged.
    /// </summary>
    private async Task<bool> IsProviderAvailableAsync(
        IPayoutProvider provider,
        CancellationToken cancellationToken)
    {
        var availability = await provider.CheckAvailabilityAsync(cancellationToken);
        if (availability.IsAvailable)
        {
            if (_lastAvailability == false)
            {
                _logger.LogInformation(
                    "{Provider} payout is available again; resuming payout processing. BalanceVnd={BalanceVnd}",
                    provider.ProviderName,
                    availability.BalanceVnd);
            }

            _lastAvailability = true;
            return true;
        }

        if (_lastAvailability != false)
        {
            _logger.LogWarning(
                "{Provider} payout is unavailable; payout processing is paused. ErrorCode={ErrorCode} Reason={Reason}",
                provider.ProviderName,
                availability.ErrorCode,
                availability.SafeMessage);
        }
        else
        {
            _logger.LogDebug(
                "{Provider} payout still unavailable. ErrorCode={ErrorCode}",
                provider.ProviderName,
                availability.ErrorCode);
        }

        _lastAvailability = false;
        return false;
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

    private static TimeSpan NextIdleInterval(TimeSpan current, TimeSpan maximum) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, maximum.Ticks));

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
            _logger.LogError(
                "Payout outbox {OutboxId} references withdrawal {WithdrawalId}, which does not exist. Dead-lettering.",
                outboxId,
                outbox.WalletWithdrawalId);
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

                _logger.LogInformation(
                    "Sending payout for withdrawal {WithdrawalId} (order {ProviderOrderCode}): " +
                    "{NetVndAmount} VND to BIN {BankBin} account {MaskedAccount}.",
                    withdrawal.WalletWithdrawalId,
                    withdrawal.ProviderOrderCode,
                    withdrawal.NetVndAmount,
                    withdrawal.BankBin,
                    withdrawal.BankAccountNumberMasked);

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

            if (payout.Outcome == PayoutProviderOutcome.SyncRequired)
            {
                await ScheduleRetryAsync(
                    context,
                    dateTimeService,
                    outbox,
                    withdrawal,
                    payout.ProviderPayoutId,
                    payout.RawStatus,
                    payout.ProviderTransactionCode,
                    payout.FailureReason ?? payout.RawStatus ?? "Payout requires sync.",
                    cancellationToken);
            }
            else
            {
                await WithdrawalWorkflow.ApplyProviderResultAsync(
                    context,
                    dateTimeService,
                    withdrawal.WalletWithdrawalId,
                    payout,
                    cancellationToken);

                outbox.Status = (int)PayoutOutboxStatus.Delivered;
                outbox.ProcessedAt = dateTimeService.UtcNow;
                outbox.LastError = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Payout outbox {OutboxId} failed for withdrawal {WithdrawalId} (order {ProviderOrderCode}).",
                outboxId,
                withdrawal.WalletWithdrawalId,
                withdrawal.ProviderOrderCode);

            await ScheduleRetryAsync(
                context,
                dateTimeService,
                outbox,
                withdrawal,
                withdrawal.ProviderPayoutId,
                withdrawal.ProviderRawStatus,
                withdrawal.ProviderTransactionCode,
                Describe(ex),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Advances the retry ladder and records the real reason on both the outbox row and the
    /// withdrawal. Once the ladder is exhausted the outbox is dead-lettered; the withdrawal is only
    /// failed - which refunds the locked tokens - when the provider never accepted a payout. If a
    /// provider payout id exists the money may already be in flight, so the row stays in
    /// SyncRequired for manual reconciliation rather than risk refunding a payout that also pays.
    /// </summary>
    private async Task ScheduleRetryAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        PayoutOutbox outbox,
        WalletWithdrawal withdrawal,
        string? providerPayoutId,
        string? rawStatus,
        string? transactionCode,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = dateTimeService.UtcNow;
        outbox.AttemptCount++;
        outbox.LastError = Truncate(reason);

        var effectivePayoutId = string.IsNullOrWhiteSpace(providerPayoutId)
            ? withdrawal.ProviderPayoutId
            : providerPayoutId;
        var deadLettered = outbox.AttemptCount > RetryDelays.Length;
        var payoutNeverAccepted = string.IsNullOrWhiteSpace(effectivePayoutId);

        if (deadLettered)
        {
            outbox.Status = (int)PayoutOutboxStatus.DeadLettered;
            outbox.ProcessedAt = now;
        }
        else
        {
            outbox.Status = (int)PayoutOutboxStatus.Pending;
            outbox.NextAttemptAt = now.Add(RetryDelays[outbox.AttemptCount - 1]);
        }

        var outcome = deadLettered && payoutNeverAccepted
            ? PayoutProviderOutcome.Failed
            : PayoutProviderOutcome.SyncRequired;

        await WithdrawalWorkflow.ApplyProviderResultAsync(
            context,
            dateTimeService,
            withdrawal.WalletWithdrawalId,
            new PayoutProviderResult(
                outcome,
                effectivePayoutId,
                transactionCode,
                rawStatus,
                reason),
            cancellationToken);

        if (!deadLettered)
        {
            return;
        }

        if (payoutNeverAccepted)
        {
            _logger.LogError(
                "Withdrawal {WithdrawalId} exhausted {AttemptCount} payout attempts and was never " +
                "accepted by the provider. Marked Failed and the locked tokens were refunded. Reason={Reason}",
                withdrawal.WalletWithdrawalId,
                outbox.AttemptCount,
                reason);
        }
        else
        {
            _logger.LogError(
                "Withdrawal {WithdrawalId} exhausted {AttemptCount} payout attempts but the provider " +
                "holds payout {ProviderPayoutId}. Tokens were NOT refunded - reconcile manually before " +
                "resolving. Reason={Reason}",
                withdrawal.WalletWithdrawalId,
                outbox.AttemptCount,
                effectivePayoutId,
                reason);
        }
    }

    private static string Describe(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 1000 ? value : value[..1000];
}
