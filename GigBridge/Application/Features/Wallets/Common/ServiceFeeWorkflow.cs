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
    private const decimal ServiceFeeRate = 0.01m;

    public static decimal CalculateVnd(decimal amountVnd)
    {
        return decimal.Round(amountVnd * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);
    }

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
        var serviceFeeVnd = CalculateVnd(jobAmount);
        var serviceFeeTokens = TokenWalletRules.ToTokens(serviceFeeVnd);
        if (serviceFeeTokens <= 0)
        {
            throw new BadRequestException("The job amount must be greater than zero to calculate the service fee.");
        }

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

        WalletWorkflow.DebitAvailable(wallet, serviceFeeTokens, now, "Insufficient GigCoin balance to pay the service fee.");

        context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = userId,
            ContractsId = contractId,
            TokenAmount = serviceFeeTokens,
            VndAmount = serviceFeeVnd,
            Type = (int)WalletTransactionType.Adjustment,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = idempotencyKey,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = idempotencyKey,
            Metadata = "{\"category\":\"ServiceFee\",\"rate\":0.01}",
            Note = note,
            CreatedAt = now,
            CompletedAt = now
        });

        return serviceFeeTokens;
    }
}
