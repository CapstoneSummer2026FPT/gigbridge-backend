using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;

namespace Application.Features.Wallets.Common;

internal static class ContractEscrowWalletWorkflow
{
    public static EscrowTransferResult Release(
        IApplicationDbContext context,
        UserWallet clientWallet,
        UserWallet freelancerWallet,
        Guid contractId,
        Guid escrowId,
        Guid? milestoneId,
        decimal amountVnd,
        string transactionCode,
        string provider,
        string note,
        DateTime now,
        bool chargeFreelancerFee = true)
    {
        if (amountVnd <= 0m)
        {
            return default;
        }

        var grossTokens = TokenWalletRules.ToTokens(amountVnd);
        var feeVnd = chargeFreelancerFee ? ServiceFeeWorkflow.CalculateVnd(amountVnd) : 0m;
        var feeTokens = TokenWalletRules.ToTokens(feeVnd);
        var netTokens = grossTokens - feeTokens;

        if (clientWallet.HeldTokens < grossTokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this release.");
        }

        clientWallet.HeldTokens -= grossTokens;
        clientWallet.UpdatedAt = now;
        WalletWorkflow.CreditWithdrawable(freelancerWallet, netTokens, now);

        context.Set<WalletTransaction>().AddRange(
            CreateTransaction(
                clientWallet,
                contractId,
                escrowId,
                milestoneId,
                grossTokens,
                amountVnd,
                WalletTransactionType.EscrowRelease,
                transactionCode,
                provider,
                note,
                now),
            CreateTransaction(
                freelancerWallet,
                contractId,
                escrowId,
                milestoneId,
                netTokens,
                amountVnd - feeVnd,
                WalletTransactionType.EscrowRelease,
                transactionCode,
                provider,
                note,
                now));

        if (feeTokens > 0m)
        {
            var feeCode = $"{ServiceFeeWorkflow.FreelancerReleaseFeePrefix}{transactionCode}";
            var feeTransaction = CreateTransaction(
                freelancerWallet,
                contractId,
                escrowId,
                milestoneId,
                feeTokens,
                feeVnd,
                WalletTransactionType.Adjustment,
                feeCode,
                "InternalTokenWallet",
                "1% freelancer service fee withheld from escrow release.",
                now);
            context.Set<WalletTransaction>().Add(feeTransaction);
            context.Set<PlatformRevenueEvent>().Add(new PlatformRevenueEvent
            {
                PlatformRevenueEventId = Guid.NewGuid(),
                Source = PlatformRevenueSource.ContractReleaseFee,
                WalletTransactionId = feeTransaction.WalletTransactionsId,
                PayerUserId = freelancerWallet.UserId,
                ContractId = contractId,
                SourceEntityType = nameof(WalletTransaction),
                SourceEntityId = feeTransaction.WalletTransactionsId,
                SourceReference = feeCode,
                GigCoinAmount = feeTokens,
                VndEquivalent = feeVnd,
                VndPerGigCoin = TokenWalletRules.VndPerToken,
                OccurredAt = now,
                RecordedAt = now,
                Metadata = "{\"rate\":0.01,\"capture\":\"atomic\"}"
            });
        }

        return new EscrowTransferResult(amountVnd, grossTokens, feeVnd, feeTokens, netTokens);
    }

    public static decimal Refund(
        IApplicationDbContext context,
        UserWallet clientWallet,
        Guid contractId,
        Guid escrowId,
        Guid? milestoneId,
        decimal amountVnd,
        string transactionCode,
        string provider,
        string note,
        DateTime now)
    {
        if (amountVnd <= 0m)
        {
            return 0m;
        }

        var tokens = TokenWalletRules.ToTokens(amountVnd);
        if (clientWallet.HeldTokens < tokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this refund.");
        }

        clientWallet.HeldTokens -= tokens;
        clientWallet.AvailableTokens += tokens;
        clientWallet.UpdatedAt = now;
        context.Set<WalletTransaction>().Add(CreateTransaction(
            clientWallet,
            contractId,
            escrowId,
            milestoneId,
            tokens,
            amountVnd,
            WalletTransactionType.EscrowRefund,
            transactionCode,
            provider,
            note,
            now));
        return tokens;
    }

    private static WalletTransaction CreateTransaction(
        UserWallet wallet,
        Guid contractId,
        Guid escrowId,
        Guid? milestoneId,
        decimal tokens,
        decimal amountVnd,
        WalletTransactionType type,
        string transactionCode,
        string provider,
        string note,
        DateTime now) => new()
    {
        WalletTransactionsId = Guid.NewGuid(),
        UserWalletsId = wallet.UserWalletsId,
        UserId = wallet.UserId,
        ContractsId = contractId,
        ContractEscrowId = escrowId,
        MilestonesId = milestoneId,
        TokenAmount = tokens,
        VndAmount = amountVnd,
        Type = (int)type,
        Status = (int)WalletTransactionStatus.Succeeded,
        IdempotencyKey = transactionCode,
        GatewayProvider = provider,
        GatewayTransactionCode = transactionCode,
        Note = note,
        CreatedAt = now,
        CompletedAt = now
    };
}

internal readonly record struct EscrowTransferResult(
    decimal GrossVnd,
    decimal GrossTokens,
    decimal FeeVnd,
    decimal FeeTokens,
    decimal NetTokens);
