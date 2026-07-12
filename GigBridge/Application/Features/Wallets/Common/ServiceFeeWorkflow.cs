using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common;

internal static class ServiceFeeWorkflow
{
    internal const string AcceptJobFeePrefix = "SERVICE-FEE-ACCEPT-";
    internal const string EndProjectFeePrefix = "SERVICE-FEE-END-";
    private const decimal ServiceFeeRate = 0.01m;

    public static decimal Calculate(decimal jobAmount)
    {
        return decimal.Round(jobAmount * ServiceFeeRate, 4, MidpointRounding.AwayFromZero);
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
        var serviceFee = Calculate(jobAmount);
        if (serviceFee <= 0)
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

        if (wallet is null || wallet.AvailableTokens < serviceFee)
        {
            throw new BadRequestException("Insufficient GigCoin balance to pay the service fee.");
        }

        wallet.AvailableTokens -= serviceFee;
        wallet.UpdatedAt = now;

        context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = userId,
            ContractsId = contractId,
            TokenAmount = serviceFee,
            VndAmount = decimal.Round(jobAmount * ServiceFeeRate, 2, MidpointRounding.AwayFromZero),
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

        return serviceFee;
    }
}
