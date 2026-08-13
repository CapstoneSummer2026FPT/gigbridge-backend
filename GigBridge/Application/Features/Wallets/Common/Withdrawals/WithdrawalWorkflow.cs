using Application.Features.Wallets.Common.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Wallets.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Wallets;
using Domain.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals;

internal static class WithdrawalWorkflow
{
    public static bool IsTerminal(int status) =>
        status == (int)WithdrawalStatus.Success ||
        status == (int)WithdrawalStatus.Failed ||
        status == (int)WithdrawalStatus.Cancelled;

    public static async Task<WalletWithdrawal> ApplyProviderResultAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        Guid withdrawalId,
        PayoutProviderResult result,
        CancellationToken cancellationToken)
    {
        return result.Outcome switch
        {
            PayoutProviderOutcome.Succeeded => await FinalizeAsync(
                context, dateTimeService, withdrawalId, WithdrawalStatus.Success, result, null, cancellationToken),
            PayoutProviderOutcome.Failed => await FinalizeAsync(
                context,
                dateTimeService,
                withdrawalId,
                WithdrawalStatus.Failed,
                result,
                result.FailureReason ?? result.RawStatus ?? "Payout failed.",
                cancellationToken),
            PayoutProviderOutcome.Accepted or PayoutProviderOutcome.Pending => await MarkNonTerminalAsync(
                context, dateTimeService, withdrawalId, WithdrawalStatus.Processing, result, null, cancellationToken),
            _ => await MarkNonTerminalAsync(
                context,
                dateTimeService,
                withdrawalId,
                WithdrawalStatus.SyncRequired,
                result,
                result.FailureReason ?? "Withdrawal status requires provider sync.",
                cancellationToken)
        };
    }

    private static async Task<WalletWithdrawal> FinalizeAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        Guid withdrawalId,
        WithdrawalStatus target,
        PayoutProviderResult result,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var current = await GetAsync(context, withdrawalId, cancellationToken);
        if (current.Status == (int)target)
        {
            return current;
        }

        if (IsTerminal(current.Status))
        {
            throw new ConflictException("Terminal withdrawal status cannot be changed.");
        }

        var now = dateTimeService.UtcNow;
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var claimed = await TryClaimTerminalAsync(
            context, current, target, result, failureReason, now, cancellationToken);
        if (!claimed)
        {
            var latest = await GetAsync(context, withdrawalId, cancellationToken);
            if (latest.Status == (int)target)
            {
                return latest;
            }

            throw new ConflictException("Terminal withdrawal status cannot be changed.");
        }

        var walletUpdated = await TryFinalizeWalletAsync(
            context, current, target, now, cancellationToken);
        if (!walletUpdated)
        {
            throw new ConflictException("Pending withdrawal balance is insufficient.");
        }

        var transactionType = target == WithdrawalStatus.Success
            ? WalletTransactionType.WithdrawalSuccess
            : WalletTransactionType.WithdrawalRefund;
        var idempotencyKey = $"withdrawal:{withdrawalId:D}:{(target == WithdrawalStatus.Success ? "success" : "refund")}";
        if (!await context.Set<WalletTransaction>().AnyAsync(
            item => item.UserId == current.UserId && item.IdempotencyKey == idempotencyKey,
            cancellationToken))
        {
            context.Set<WalletTransaction>().Add(new WalletTransaction
            {
                WalletTransactionsId = Guid.NewGuid(),
                UserWalletsId = current.UserWalletsId,
                UserId = current.UserId,
                TokenAmount = current.TokenAmount,
                VndAmount = current.VndAmount,
                BalanceSource = (int)(target == WithdrawalStatus.Success
                    ? WalletBalanceSource.PendingWithdrawal
                    : WalletBalanceSource.Earned),
                EarnedAmount = current.TokenAmount,
                Type = (int)transactionType,
                Status = (int)WalletTransactionStatus.Succeeded,
                IdempotencyKey = idempotencyKey,
                GatewayProvider = current.Provider,
                GatewayOrderCode = current.ProviderOrderCode,
                GatewayTransactionCode = $"WITHDRAWAL-{target.ToString().ToUpperInvariant()}-{withdrawalId:N}",
                Metadata = withdrawalId.ToString("D"),
                Note = target == WithdrawalStatus.Success
                    ? "Withdrawal payout completed."
                    : "Withdrawal failed and earned balance was refunded.",
                CreatedAt = now,
                CompletedAt = now
            });
        }

        if (target == WithdrawalStatus.Success && current.FeeVnd > 0m &&
            !await context.Set<PlatformRevenueEvent>().AnyAsync(
                item => item.WalletWithdrawalId == withdrawalId,
                cancellationToken))
        {
            context.Set<PlatformRevenueEvent>().Add(new PlatformRevenueEvent
            {
                PlatformRevenueEventId = Guid.NewGuid(),
                Source = PlatformRevenueSource.WithdrawalFee,
                WalletWithdrawalId = withdrawalId,
                PayerUserId = current.UserId,
                SourceEntityType = nameof(WalletWithdrawal),
                SourceEntityId = withdrawalId,
                SourceReference = current.ProviderTransactionCode ?? current.ProviderOrderCode,
                GigCoinAmount = TokenWalletRules.ToTokens(current.FeeVnd),
                VndEquivalent = current.FeeVnd,
                VndPerGigCoin = TokenWalletRules.VndPerToken,
                OccurredAt = now,
                RecordedAt = now,
                Metadata = "{\"capture\":\"successful-withdrawal\"}"
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(context, withdrawalId, cancellationToken);
    }

    private static async Task<WalletWithdrawal> MarkNonTerminalAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        Guid withdrawalId,
        WithdrawalStatus target,
        PayoutProviderResult result,
        string? syncError,
        CancellationToken cancellationToken)
    {
        var now = dateTimeService.UtcNow;
        try
        {
            await context.Set<WalletWithdrawal>()
                .Where(item => item.WalletWithdrawalId == withdrawalId &&
                    item.Status != (int)WithdrawalStatus.Success &&
                    item.Status != (int)WithdrawalStatus.Failed &&
                    item.Status != (int)WithdrawalStatus.Cancelled)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, (int)target)
                        .SetProperty(item => item.ProviderPayoutId, item => result.ProviderPayoutId ?? item.ProviderPayoutId)
                        .SetProperty(item => item.ProviderTransactionCode, item => result.ProviderTransactionCode ?? item.ProviderTransactionCode)
                        .SetProperty(item => item.ProviderRawStatus, item => result.RawStatus ?? item.ProviderRawStatus)
                        .SetProperty(item => item.ProcessingStartedAt, item =>
                            target == WithdrawalStatus.Processing && item.ProcessingStartedAt == null
                                ? now
                                : item.ProcessingStartedAt)
                        .SetProperty(item => item.LastSyncError, Truncate(syncError))
                        .SetProperty(item => item.LastSyncedAt, now)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
        }
        catch (Exception ex) when (IsExecuteUpdateUnsupported(ex))
        {
            var item = await GetTrackedAsync(context, withdrawalId, cancellationToken);
            if (!IsTerminal(item.Status))
            {
                item.Status = (int)target;
                item.ProviderPayoutId = result.ProviderPayoutId ?? item.ProviderPayoutId;
                item.ProviderTransactionCode = result.ProviderTransactionCode ?? item.ProviderTransactionCode;
                item.ProviderRawStatus = result.RawStatus ?? item.ProviderRawStatus;
                item.ProcessingStartedAt ??= target == WithdrawalStatus.Processing ? now : null;
                item.LastSyncError = Truncate(syncError);
                item.LastSyncedAt = now;
                item.UpdatedAt = now;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return await GetAsync(context, withdrawalId, cancellationToken);
    }

    private static async Task<bool> TryClaimTerminalAsync(
        IApplicationDbContext context,
        WalletWithdrawal current,
        WithdrawalStatus target,
        PayoutProviderResult result,
        string? failureReason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.Set<WalletWithdrawal>()
                .Where(item => item.WalletWithdrawalId == current.WalletWithdrawalId &&
                    item.Status != (int)WithdrawalStatus.Success &&
                    item.Status != (int)WithdrawalStatus.Failed &&
                    item.Status != (int)WithdrawalStatus.Cancelled)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, (int)target)
                        .SetProperty(item => item.ProviderPayoutId, item => result.ProviderPayoutId ?? item.ProviderPayoutId)
                        .SetProperty(item => item.ProviderTransactionCode, item => result.ProviderTransactionCode ?? item.ProviderTransactionCode)
                        .SetProperty(item => item.ProviderRawStatus, item => result.RawStatus ?? item.ProviderRawStatus)
                        .SetProperty(item => item.FailureReason, Truncate(failureReason))
                        .SetProperty(item => item.LastSyncError, (string?)null)
                        .SetProperty(item => item.LastSyncedAt, now)
                        .SetProperty(item => item.CompletedAt, now)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken) == 1;
        }
        catch (Exception ex) when (IsExecuteUpdateUnsupported(ex))
        {
            var item = await GetTrackedAsync(context, current.WalletWithdrawalId, cancellationToken);
            if (IsTerminal(item.Status)) return false;
            item.Status = (int)target;
            item.ProviderPayoutId = result.ProviderPayoutId ?? item.ProviderPayoutId;
            item.ProviderTransactionCode = result.ProviderTransactionCode ?? item.ProviderTransactionCode;
            item.ProviderRawStatus = result.RawStatus ?? item.ProviderRawStatus;
            item.FailureReason = Truncate(failureReason);
            item.LastSyncError = null;
            item.LastSyncedAt = now;
            item.CompletedAt = now;
            item.UpdatedAt = now;
            return true;
        }
    }

    private static async Task<bool> TryFinalizeWalletAsync(
        IApplicationDbContext context,
        WalletWithdrawal withdrawal,
        WithdrawalStatus target,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = context.Set<UserWallet>().Where(wallet =>
                wallet.UserWalletsId == withdrawal.UserWalletsId &&
                wallet.PendingWithdrawalTokens >= withdrawal.TokenAmount);
            return target == WithdrawalStatus.Success
                ? await query.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(wallet => wallet.PendingWithdrawalTokens, wallet => wallet.PendingWithdrawalTokens - withdrawal.TokenAmount)
                        .SetProperty(wallet => wallet.Version, wallet => wallet.Version + 1)
                        .SetProperty(wallet => wallet.UpdatedAt, now),
                    cancellationToken) == 1
                : await query.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(wallet => wallet.PendingWithdrawalTokens, wallet => wallet.PendingWithdrawalTokens - withdrawal.TokenAmount)
                        .SetProperty(wallet => wallet.WithdrawableTokens, wallet => wallet.WithdrawableTokens + withdrawal.TokenAmount)
                        .SetProperty(wallet => wallet.Version, wallet => wallet.Version + 1)
                        .SetProperty(wallet => wallet.UpdatedAt, now),
                    cancellationToken) == 1;
        }
        catch (Exception ex) when (IsExecuteUpdateUnsupported(ex))
        {
            var wallet = await context.Set<UserWallet>().FirstOrDefaultAsync(
                item => item.UserWalletsId == withdrawal.UserWalletsId,
                cancellationToken);
            if (wallet is null || wallet.PendingWithdrawalTokens < withdrawal.TokenAmount) return false;
            wallet.PendingWithdrawalTokens -= withdrawal.TokenAmount;
            if (target == WithdrawalStatus.Failed)
            {
                // A rejected/cancelled/failed withdrawal returns only to the earned pool.
                wallet.WithdrawableTokens += withdrawal.TokenAmount;
            }
            wallet.Version++;
            wallet.UpdatedAt = now;
            return true;
        }
    }

    private static async Task<WalletWithdrawal> GetAsync(
        IApplicationDbContext context,
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        await context.Set<WalletWithdrawal>().AsNoTracking().FirstOrDefaultAsync(
            item => item.WalletWithdrawalId == withdrawalId,
            cancellationToken) ?? throw new NotFoundException("Withdrawal does not exist.");

    private static async Task<WalletWithdrawal> GetTrackedAsync(
        IApplicationDbContext context,
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        await context.Set<WalletWithdrawal>().FirstOrDefaultAsync(
            item => item.WalletWithdrawalId == withdrawalId,
            cancellationToken) ?? throw new NotFoundException("Withdrawal does not exist.");

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 1000 ? value : value[..1000];

    private static bool IsExecuteUpdateUnsupported(Exception exception) =>
        exception is InvalidOperationException or NotSupportedException ||
        exception.InnerException is not null && IsExecuteUpdateUnsupported(exception.InnerException);
}
