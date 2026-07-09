using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals;

internal static class WithdrawalWorkflow
{
    public static bool IsTerminal(int status)
    {
        return status == (int)WithdrawalStatus.Success ||
            status == (int)WithdrawalStatus.Failed ||
            status == (int)WithdrawalStatus.Cancelled;
    }

    public static async Task ApplyProviderResultAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        PayoutProviderResult providerResult,
        CancellationToken cancellationToken)
    {
        UpdateProviderFields(withdrawal, providerResult);

        switch (providerResult.Outcome)
        {
            case PayoutProviderOutcome.Succeeded:
                await FinalizeSuccessAsync(context, dateTimeService, withdrawal, providerResult, cancellationToken);
                break;

            case PayoutProviderOutcome.Failed:
                await FinalizeFailedAsync(
                    context,
                    dateTimeService,
                    withdrawal,
                    providerResult.FailureReason ?? providerResult.RawStatus ?? "Payout failed.",
                    providerResult,
                    cancellationToken);
                break;

            case PayoutProviderOutcome.Accepted:
            case PayoutProviderOutcome.Pending:
                MarkProcessing(dateTimeService, withdrawal, providerResult);
                break;

            case PayoutProviderOutcome.SyncRequired:
                MarkSyncRequired(dateTimeService, withdrawal, providerResult.FailureReason, providerResult);
                break;
        }
    }

    public static async Task ApplyWebhookResultAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        PayoutWebhookVerificationResult webhook,
        CancellationToken cancellationToken)
    {
        var result = new PayoutProviderResult(
            webhook.Outcome,
            webhook.ProviderPayoutId,
            webhook.ProviderTransactionCode,
            webhook.RawStatus,
            webhook.FailureReason,
            webhook.RawPayload);

        await ApplyProviderResultAsync(context, dateTimeService, withdrawal, result, cancellationToken);
    }

    public static async Task FinalizeSuccessAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        PayoutProviderResult providerResult,
        CancellationToken cancellationToken)
    {
        if (withdrawal.Status == (int)WithdrawalStatus.Success)
        {
            return;
        }

        if (withdrawal.Status == (int)WithdrawalStatus.Failed ||
            withdrawal.Status == (int)WithdrawalStatus.Cancelled)
        {
            throw new ConflictException("Terminal withdrawal status cannot be changed.");
        }

        var wallet = await GetWalletAsync(context, withdrawal.UserWalletsId, cancellationToken);
        if (wallet.PendingWithdrawalTokens < withdrawal.TokenAmount)
        {
            throw new ConflictException("Pending withdrawal balance is insufficient.");
        }

        var now = dateTimeService.UtcNow;
        wallet.PendingWithdrawalTokens -= withdrawal.TokenAmount;
        wallet.UpdatedAt = now;

        withdrawal.Status = (int)WithdrawalStatus.Success;
        withdrawal.ProviderPayoutId = providerResult.ProviderPayoutId ?? withdrawal.ProviderPayoutId;
        withdrawal.ProviderTransactionCode = providerResult.ProviderTransactionCode ?? withdrawal.ProviderTransactionCode;
        withdrawal.ProviderRawStatus = providerResult.RawStatus ?? withdrawal.ProviderRawStatus;
        withdrawal.LastSyncError = null;
        withdrawal.LastSyncedAt = now;
        withdrawal.CompletedAt = now;
        withdrawal.UpdatedAt = now;

        await AddWalletTransactionIfMissingAsync(
            context,
            withdrawal,
            WalletTransactionType.WithdrawalSuccess,
            WalletTransactionStatus.Succeeded,
            $"WITHDRAWAL-SUCCESS-{withdrawal.WalletWithdrawalId:N}",
            "Withdrawal payout completed.",
            now,
            cancellationToken);
    }

    public static async Task FinalizeFailedAsync(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        string reason,
        PayoutProviderResult? providerResult,
        CancellationToken cancellationToken)
    {
        if (withdrawal.Status == (int)WithdrawalStatus.Failed)
        {
            return;
        }

        if (withdrawal.Status == (int)WithdrawalStatus.Success ||
            withdrawal.Status == (int)WithdrawalStatus.Cancelled)
        {
            throw new ConflictException("Terminal withdrawal status cannot be changed.");
        }

        var wallet = await GetWalletAsync(context, withdrawal.UserWalletsId, cancellationToken);
        if (wallet.PendingWithdrawalTokens < withdrawal.TokenAmount)
        {
            throw new ConflictException("Pending withdrawal balance is insufficient.");
        }

        var now = dateTimeService.UtcNow;
        wallet.PendingWithdrawalTokens -= withdrawal.TokenAmount;
        wallet.AvailableTokens += withdrawal.TokenAmount;
        wallet.UpdatedAt = now;

        if (providerResult is not null)
        {
            UpdateProviderFields(withdrawal, providerResult);
        }

        withdrawal.Status = (int)WithdrawalStatus.Failed;
        withdrawal.FailureReason = reason;
        withdrawal.LastSyncError = null;
        withdrawal.LastSyncedAt = now;
        withdrawal.CompletedAt = now;
        withdrawal.UpdatedAt = now;

        await AddWalletTransactionIfMissingAsync(
            context,
            withdrawal,
            WalletTransactionType.WithdrawalRefund,
            WalletTransactionStatus.Succeeded,
            $"WITHDRAWAL-REFUND-{withdrawal.WalletWithdrawalId:N}",
            $"Withdrawal failed and balance was refunded. Reason: {reason}",
            now,
            cancellationToken);
    }

    public static void MarkProcessing(
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        PayoutProviderResult providerResult)
    {
        if (IsTerminal(withdrawal.Status))
        {
            return;
        }

        var now = dateTimeService.UtcNow;
        UpdateProviderFields(withdrawal, providerResult);
        withdrawal.Status = (int)WithdrawalStatus.Processing;
        withdrawal.ProcessingStartedAt ??= now;
        withdrawal.LastSyncedAt = now;
        withdrawal.LastSyncError = null;
        withdrawal.UpdatedAt = now;
    }

    public static void MarkSyncRequired(
        IDateTimeService dateTimeService,
        WalletWithdrawal withdrawal,
        string? reason,
        PayoutProviderResult? providerResult = null)
    {
        if (IsTerminal(withdrawal.Status))
        {
            return;
        }

        if (providerResult is not null)
        {
            UpdateProviderFields(withdrawal, providerResult);
        }

        var now = dateTimeService.UtcNow;
        withdrawal.Status = (int)WithdrawalStatus.SyncRequired;
        withdrawal.LastSyncError = reason ?? "Withdrawal status requires provider sync.";
        withdrawal.LastSyncedAt = now;
        withdrawal.UpdatedAt = now;
    }

    private static void UpdateProviderFields(
        WalletWithdrawal withdrawal,
        PayoutProviderResult providerResult)
    {
        withdrawal.ProviderPayoutId = providerResult.ProviderPayoutId ?? withdrawal.ProviderPayoutId;
        withdrawal.ProviderTransactionCode = providerResult.ProviderTransactionCode ?? withdrawal.ProviderTransactionCode;
        withdrawal.ProviderRawStatus = providerResult.RawStatus ?? withdrawal.ProviderRawStatus;
        withdrawal.Metadata = providerResult.RawPayload ?? withdrawal.Metadata;
    }

    private static async Task<UserWallet> GetWalletAsync(
        IApplicationDbContext context,
        Guid walletId,
        CancellationToken cancellationToken)
    {
        return await context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserWalletsId == walletId, cancellationToken)
            ?? throw new NotFoundException("Wallet does not exist.");
    }

    private static async Task AddWalletTransactionIfMissingAsync(
        IApplicationDbContext context,
        WalletWithdrawal withdrawal,
        WalletTransactionType type,
        WalletTransactionStatus status,
        string transactionCode,
        string note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await context.Set<WalletTransaction>()
            .AnyAsync(
                transaction =>
                    transaction.UserId == withdrawal.UserId &&
                    transaction.Type == (int)type &&
                    transaction.GatewayTransactionCode == transactionCode,
                cancellationToken);

        if (exists)
        {
            return;
        }

        context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = withdrawal.UserWalletsId,
            UserId = withdrawal.UserId,
            TokenAmount = withdrawal.TokenAmount,
            VndAmount = withdrawal.VndAmount,
            Type = (int)type,
            Status = (int)status,
            GatewayProvider = withdrawal.Provider,
            GatewayOrderCode = withdrawal.ProviderOrderCode,
            GatewayTransactionCode = transactionCode,
            Metadata = withdrawal.WalletWithdrawalId.ToString("D"),
            Note = note.Length > 1000 ? note[..1000] : note,
            CreatedAt = now,
            CompletedAt = now
        });
    }
}
