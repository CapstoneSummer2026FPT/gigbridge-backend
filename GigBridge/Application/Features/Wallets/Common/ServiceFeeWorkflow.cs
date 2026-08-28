using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Wallets;
using Domain.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common;

internal static class ServiceFeeWorkflow
{
    internal const string AcceptJobFeePrefix = "SERVICE-FEE-ACCEPT-";
    internal const string ClientFundingFeePrefix = "SERVICE-FEE-FUND-";
    internal const string FreelancerReleaseFeePrefix = "SERVICE-FEE-RELEASE-";
    internal const decimal ServiceFeeRate = 0.01m;

    public static async Task<decimal> ChargeAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid contractId,
        decimal jobAmount,
        string idempotencyKey,
        string note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Service fees are charged directly on the G-coin job amount (1%). The
        // WalletTransaction.VndAmount field stores the G-coin fee number (a legacy
        // field-name mislabel); the true VND value is derived for revenue events.
        var serviceFeeTokens = decimal.Round(
            jobAmount * ServiceFeeRate,
            4,
            MidpointRounding.AwayFromZero);
        if (serviceFeeTokens <= 0m)
        {
            throw new BadRequestException("The job amount must be greater than zero to calculate the service fee.");
        }
        var serviceFeeVnd = serviceFeeTokens;

        var existingTransaction = await context.Set<WalletTransaction>()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.UserId == userId &&
                    transaction.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existingTransaction is not null)
        {
            return existingTransaction.TokenAmount;
        }

        var wallet = await context.Set<UserWallet>()
            .FirstOrDefaultAsync(existingWallet => existingWallet.UserId == userId, cancellationToken);

        if (wallet is null)
        {
            throw new BadRequestException("Insufficient GigCoin balance to pay the service fee.");
        }

        var walletBefore = WalletBalanceAudit.Snapshot(wallet);
        var usage = WalletWorkflow.DebitAvailable(
            wallet,
            serviceFeeTokens,
            now,
            "Insufficient GigCoin balance to pay the service fee.");
        var balanceSource = WalletBalanceAudit.ResolveSource(usage.DepositedAmount, usage.EarnedAmount);
        var (depositedAmount, earnedAmount) = WalletBalanceAudit.ToSplitAmounts(
            usage.DepositedAmount,
            usage.EarnedAmount);

        var walletTransaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = userId,
            ContractsId = contractId,
            TokenAmount = serviceFeeTokens,
            VndAmount = serviceFeeVnd,
            BalanceSource = (int)balanceSource,
            DepositedAmount = depositedAmount,
            EarnedAmount = earnedAmount,
            Type = (int)WalletTransactionType.Adjustment,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = idempotencyKey,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = idempotencyKey,
            Metadata = WalletBalanceAudit.EnrichMetadata(
                "{\"category\":\"ServiceFee\",\"rate\":0.01}",
                usage.DepositedAmount,
                usage.EarnedAmount,
                walletBefore,
                wallet),
            Note = note,
            CreatedAt = now,
            CompletedAt = now
        };
        context.Set<WalletTransaction>().Add(walletTransaction);
        context.Set<PlatformRevenueEvent>().Add(new PlatformRevenueEvent
        {
            PlatformRevenueEventId = Guid.NewGuid(),
            Source = idempotencyKey.StartsWith(ClientFundingFeePrefix, StringComparison.Ordinal)
                ? PlatformRevenueSource.ContractFundingFee
                : PlatformRevenueSource.ContractReleaseFee,
            WalletTransactionId = walletTransaction.WalletTransactionsId,
            PayerUserId = userId,
            ContractId = contractId,
            SourceEntityType = nameof(WalletTransaction),
            SourceEntityId = walletTransaction.WalletTransactionsId,
            SourceReference = idempotencyKey,
            GigCoinAmount = serviceFeeTokens,
            VndEquivalent = TokenWalletRules.ToVnd(serviceFeeTokens),
            VndPerGigCoin = TokenWalletRules.VndPerToken,
            OccurredAt = now,
            RecordedAt = now,
            Metadata = "{\"rate\":0.01,\"capture\":\"atomic\"}"
        });

        return serviceFeeTokens;
    }

    /// <summary>
    /// Reverses a previously charged service fee (looked up by the original charge's
    /// idempotency key), crediting the same deposited/earned split back to the payer's
    /// wallet and removing the platform revenue event it originally recorded (revenue
    /// rows are constrained to non-negative amounts, so a reversal is expressed by
    /// deleting the original row rather than inserting a negative counter-entry). Used
    /// when a contract is cancelled before it became active, so the fee never should
    /// have been retained. Idempotent: replays return the already-recorded refund amount.
    /// </summary>
    public static async Task<decimal> RefundAsync(
        IApplicationDbContext context,
        Guid userId,
        Guid contractId,
        string originalChargeIdempotencyKey,
        string refundIdempotencyKey,
        string note,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingRefund = await context.Set<WalletTransaction>()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.UserId == userId &&
                    transaction.IdempotencyKey == refundIdempotencyKey,
                cancellationToken);

        if (existingRefund is not null)
        {
            return existingRefund.TokenAmount;
        }

        var originalCharge = await context.Set<WalletTransaction>()
            .FirstOrDefaultAsync(
                transaction =>
                    transaction.UserId == userId &&
                    transaction.IdempotencyKey == originalChargeIdempotencyKey,
                cancellationToken);

        if (originalCharge is null)
        {
            // No fee was ever charged (e.g. it failed or was skipped) — nothing to refund.
            return 0m;
        }

        var wallet = await context.Set<UserWallet>()
            .FirstOrDefaultAsync(existingWallet => existingWallet.UserId == userId, cancellationToken)
            ?? throw new BadRequestException("Wallet does not exist for the service fee refund.");

        var depositedAmount = originalCharge.DepositedAmount ?? originalCharge.TokenAmount;
        var earnedAmount = originalCharge.EarnedAmount ?? 0m;

        var walletBefore = WalletBalanceAudit.Snapshot(wallet);
        wallet.AvailableTokens += depositedAmount;
        wallet.WithdrawableTokens += earnedAmount;
        wallet.UpdatedAt = now;

        var refundTransaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = userId,
            ContractsId = contractId,
            TokenAmount = originalCharge.TokenAmount,
            VndAmount = originalCharge.VndAmount,
            BalanceSource = (int)WalletBalanceSource.Combined,
            DepositedAmount = depositedAmount,
            EarnedAmount = earnedAmount,
            Type = (int)WalletTransactionType.ServiceFeeRefund,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = refundIdempotencyKey,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = refundIdempotencyKey,
            Metadata = WalletBalanceAudit.EnrichMetadata(
                "{\"category\":\"ServiceFeeRefund\"}",
                depositedAmount,
                earnedAmount,
                walletBefore,
                wallet),
            Note = note,
            CreatedAt = now,
            CompletedAt = now
        };
        context.Set<WalletTransaction>().Add(refundTransaction);

        var originalRevenueEvent = await context.Set<PlatformRevenueEvent>()
            .FirstOrDefaultAsync(
                revenueEvent => revenueEvent.WalletTransactionId == originalCharge.WalletTransactionsId,
                cancellationToken);

        if (originalRevenueEvent is not null)
        {
            context.Set<PlatformRevenueEvent>().Remove(originalRevenueEvent);
        }

        return originalCharge.TokenAmount;
    }
}
