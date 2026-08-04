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
        ContractEscrow escrow,
        Guid contractId,
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

        // Contract/escrow amounts are G-coin: the gross transfer and its 1% fee are
        // expressed directly in G-coin tokens (no VND -> token division).
        var grossTokens = amountVnd;
        var feeTokens = chargeFreelancerFee
            ? decimal.Round(amountVnd * ServiceFeeWorkflow.ServiceFeeRate, 4, MidpointRounding.AwayFromZero)
            : 0m;
        var feeVnd = feeTokens;
        var netTokens = grossTokens - feeTokens;

        if (clientWallet.HeldTokens < grossTokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this release.");
        }

        var clientBefore = Snapshot(clientWallet);
        var freelancerBefore = Snapshot(freelancerWallet);

        // The released funds leave the client's held escrow; the escrow's source
        // composition shrinks proportionally. The freelancer is credited the net
        // amount as EARNED GigCoin regardless of how the client funded the escrow.
        var heldSplit = SplitByEscrowComposition(escrow, grossTokens);
        clientWallet.HeldTokens -= grossTokens;
        clientWallet.UpdatedAt = now;
        WalletWorkflow.CreditWithdrawable(freelancerWallet, netTokens, now);
        ConsumeEscrowSource(escrow, heldSplit);

        var clientSource = ToHeldSource(heldSplit);
        var (clientSplitDeposited, clientSplitEarned) = WalletBalanceAudit.ToSplitAmounts(
            heldSplit.Deposited,
            heldSplit.Earned);
        context.Set<WalletTransaction>().AddRange(
            CreateTransaction(
                clientWallet,
                contractId,
                escrow.ContractEscrowId,
                milestoneId,
                grossTokens,
                amountVnd,
                WalletTransactionType.EscrowRelease,
                transactionCode,
                provider,
                note,
                now,
                clientSource,
                clientSplitDeposited,
                clientSplitEarned,
                WalletBalanceAudit.EnrichMetadata(
                    null, heldSplit.Deposited, heldSplit.Earned, clientBefore, clientWallet)),
            CreateTransaction(
                freelancerWallet,
                contractId,
                escrow.ContractEscrowId,
                milestoneId,
                netTokens,
                amountVnd - feeVnd,
                WalletTransactionType.EscrowRelease,
                transactionCode,
                provider,
                note,
                now,
                WalletBalanceSource.Earned,
                null,
                netTokens,
                WalletBalanceAudit.EnrichMetadata(
                    null, 0m, netTokens, freelancerBefore, freelancerWallet)));

        if (feeTokens > 0m)
        {
            var feeCode = $"{ServiceFeeWorkflow.FreelancerReleaseFeePrefix}{transactionCode}";
            var feeTransaction = CreateTransaction(
                freelancerWallet,
                contractId,
                escrow.ContractEscrowId,
                milestoneId,
                feeTokens,
                feeVnd,
                WalletTransactionType.Adjustment,
                feeCode,
                "InternalTokenWallet",
                "1% freelancer service fee withheld from escrow release.",
                now,
                WalletBalanceSource.Earned,
                null,
                feeTokens,
                null);
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
                VndEquivalent = TokenWalletRules.ToVnd(feeTokens),
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
        ContractEscrow escrow,
        Guid contractId,
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

        // Contract/escrow amounts are G-coin: the refund restores the G-coin amount directly.
        var tokens = amountVnd;
        if (clientWallet.HeldTokens < tokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this refund.");
        }

        var before = Snapshot(clientWallet);
        var split = SplitByEscrowComposition(escrow, tokens);

        // Refunds restore each source to the client's matching pool: deposited
        // portions back to the deposited balance, earned portions back to earned.
        clientWallet.HeldTokens -= tokens;
        clientWallet.AvailableTokens += split.Deposited;
        clientWallet.WithdrawableTokens += split.Earned;
        clientWallet.UpdatedAt = now;
        ConsumeEscrowSource(escrow, split);

        var (depositedAmount, earnedAmount) = WalletBalanceAudit.ToSplitAmounts(
            split.Deposited,
            split.Earned);
        context.Set<WalletTransaction>().Add(CreateTransaction(
            clientWallet,
            contractId,
            escrow.ContractEscrowId,
            milestoneId,
            tokens,
            amountVnd,
            WalletTransactionType.EscrowRefund,
            transactionCode,
            provider,
            note,
            now,
            ToHeldSource(split),
            depositedAmount,
            earnedAmount,
            WalletBalanceAudit.EnrichMetadata(null, split.Deposited, split.Earned, before, clientWallet)));
        return tokens;
    }

    /// <summary>
    /// Retains a penalty for the platform by debiting only the client's held escrow balance.
    /// No user wallet is credited and the returned transaction is the client debit ledger entry.
    /// </summary>
    public static WalletTransaction Penalty(
        IApplicationDbContext context,
        UserWallet clientWallet,
        ContractEscrow escrow,
        Guid contractId,
        Guid milestoneId,
        Guid disputeId,
        decimal amountVnd,
        string transactionCode,
        string note,
        DateTime now)
    {
        if (amountVnd <= 0m)
            throw new BadRequestException("Penalty amount must be greater than zero.");

        // Contract/escrow amounts are G-coin: the penalty debits the G-coin amount directly
        // from the client's held escrow and is retained by the platform (no wallet credit).
        var tokens = amountVnd;
        if (clientWallet.HeldTokens < tokens)
            throw new BadRequestException("Client held wallet balance is insufficient for this penalty.");

        var before = Snapshot(clientWallet);
        var split = SplitByEscrowComposition(escrow, tokens);
        clientWallet.HeldTokens -= tokens;
        clientWallet.UpdatedAt = now;
        ConsumeEscrowSource(escrow, split);

        var existingMetadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            disputeId,
            sourceUserId = clientWallet.UserId,
            retainedByPlatform = true,
            operation = "DisputePenaltyClientHeldDebit"
        });
        var (depositedAmount, earnedAmount) = WalletBalanceAudit.ToSplitAmounts(
            split.Deposited,
            split.Earned);
        var debit = CreateTransaction(clientWallet, contractId, escrow.ContractEscrowId, milestoneId, tokens,
            amountVnd, WalletTransactionType.DisputePenalty, transactionCode,
            "AdminDisputeResolution", note, now,
            ToHeldSource(split), depositedAmount, earnedAmount, null);
        debit.Metadata = WalletBalanceAudit.EnrichMetadata(
            existingMetadata, split.Deposited, split.Earned, before, clientWallet);
        context.Set<WalletTransaction>().Add(debit);
        return debit;
    }

    /// <summary>
    /// Splits a token amount leaving the escrow by the escrow's held composition.
    /// A legacy escrow created before source tracking (composition 0/0) falls back
    /// to treating the whole amount as deposited, matching prior refund behaviour.
    /// </summary>
    private static (decimal Deposited, decimal Earned) SplitByEscrowComposition(
        ContractEscrow escrow,
        decimal tokenAmount)
    {
        var total = escrow.DepositedTokens + escrow.EarnedTokens;
        if (total <= 0m)
        {
            return (tokenAmount, 0m);
        }

        if (tokenAmount > total)
        {
            throw new BadRequestException("Escrow source composition is insufficient for this transfer.");
        }

        var deposited = decimal.Round(
            tokenAmount * escrow.DepositedTokens / total,
            4,
            MidpointRounding.AwayFromZero);
        deposited = Math.Min(deposited, escrow.DepositedTokens);
        var earned = tokenAmount - deposited;
        if (earned > escrow.EarnedTokens)
        {
            earned = escrow.EarnedTokens;
            deposited = tokenAmount - earned;
        }
        return (deposited, earned);
    }

    private static void ConsumeEscrowSource(
        ContractEscrow escrow,
        (decimal Deposited, decimal Earned) split)
    {
        escrow.DepositedTokens -= split.Deposited;
        escrow.EarnedTokens -= split.Earned;
    }

    private static WalletBalanceSource ToHeldSource((decimal Deposited, decimal Earned) split) =>
        split.Deposited > 0m && split.Earned > 0m
            ? WalletBalanceSource.Combined
            : split.Earned > 0m
                ? WalletBalanceSource.HeldEarned
                : WalletBalanceSource.HeldDeposited;

    private static UserWallet Snapshot(UserWallet wallet) => new()
    {
        AvailableTokens = wallet.AvailableTokens,
        WithdrawableTokens = wallet.WithdrawableTokens,
        HeldTokens = wallet.HeldTokens,
        PendingWithdrawalTokens = wallet.PendingWithdrawalTokens
    };

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
        DateTime now,
        WalletBalanceSource balanceSource = WalletBalanceSource.Deposited,
        decimal? depositedAmount = null,
        decimal? earnedAmount = null,
        string? metadata = null) => new()
    {
        WalletTransactionsId = Guid.NewGuid(),
        UserWalletsId = wallet.UserWalletsId,
        UserId = wallet.UserId,
        ContractsId = contractId,
        ContractEscrowId = escrowId,
        MilestonesId = milestoneId,
        TokenAmount = tokens,
        VndAmount = amountVnd,
        BalanceSource = (int)balanceSource,
        DepositedAmount = depositedAmount,
        EarnedAmount = earnedAmount,
        Type = (int)type,
        Status = (int)WalletTransactionStatus.Succeeded,
        IdempotencyKey = transactionCode,
        GatewayProvider = provider,
        GatewayTransactionCode = transactionCode,
        Metadata = metadata,
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
