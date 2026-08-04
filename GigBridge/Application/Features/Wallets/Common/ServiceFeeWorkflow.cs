using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common;

internal static class ServiceFeeWorkflow
{
    internal const string AcceptJobFeePrefix = "SERVICE-FEE-ACCEPT-";
    internal const string EndProjectFeePrefix = "SERVICE-FEE-END-";
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
}
