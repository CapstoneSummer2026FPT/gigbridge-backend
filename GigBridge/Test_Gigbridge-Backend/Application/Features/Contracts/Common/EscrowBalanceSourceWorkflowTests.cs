using Application.Common.Exceptions;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

/// <summary>
/// Verifies that escrow funding source composition is tracked on the escrow and that
/// releases, refunds and penalties restore or credit each GigCoin pool correctly.
/// </summary>
public sealed class EscrowBalanceSourceWorkflowTests
{
    private static readonly DateTime Now = new(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Release_CreditsEntireNetToFreelancerWithdrawableTokensRegardlessOfClientFundingSource()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = 100m
        };
        var freelancer = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 60m,
            EarnedTokens = 40m
        };

        var result = ContractEscrowWalletWorkflow.Release(
            context, client, freelancer, escrow, contractId, milestoneId,
            100_000m, "release-1", "InternalTokenWallet", "Milestone release", Now);

        Assert.Equal(100m, result.GrossTokens);
        Assert.Equal(1m, result.FeeTokens);
        Assert.Equal(99m, result.NetTokens);
        Assert.Equal(0m, client.HeldTokens);
        Assert.Equal(0m, freelancer.AvailableTokens);
        Assert.Equal(99m, freelancer.WithdrawableTokens);
        Assert.Equal(0m, escrow.DepositedTokens);
        Assert.Equal(0m, escrow.EarnedTokens);

        var clientRelease = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowRelease &&
            transaction.UserId == client.UserId));
        Assert.Equal((int)WalletBalanceSource.Combined, clientRelease.BalanceSource);
        Assert.Equal(60m, clientRelease.DepositedAmount);
        Assert.Equal(40m, clientRelease.EarnedAmount);

        var freelancerRelease = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowRelease &&
            transaction.UserId == freelancer.UserId));
        Assert.Equal((int)WalletBalanceSource.Earned, freelancerRelease.BalanceSource);
        Assert.Null(freelancerRelease.DepositedAmount);
        Assert.Equal(99m, freelancerRelease.EarnedAmount);

        var fee = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Equal((int)WalletBalanceSource.Earned, fee.BalanceSource);
        Assert.Equal(1m, fee.EarnedAmount);
    }

    [Fact]
    public void Refund_RestoresEachSourceToItsMatchingPool()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 100m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 60m,
            EarnedTokens = 40m
        };

        var refundedTokens = ContractEscrowWalletWorkflow.Refund(
            context, client, escrow, contractId, null,
            100_000m, "refund-1", "InternalTokenWallet", "Contract cancelled", Now);

        Assert.Equal(100m, refundedTokens);
        Assert.Equal(0m, client.HeldTokens);
        Assert.Equal(60m, client.AvailableTokens);
        Assert.Equal(40m, client.WithdrawableTokens);
        Assert.Equal(0m, escrow.DepositedTokens);
        Assert.Equal(0m, escrow.EarnedTokens);

        var refund = Assert.Single(transactions.Entities);
        Assert.Equal((int)WalletTransactionType.EscrowRefund, refund.Type);
        Assert.Equal((int)WalletBalanceSource.Combined, refund.BalanceSource);
        Assert.Equal(60m, refund.DepositedAmount);
        Assert.Equal(40m, refund.EarnedAmount);
    }

    [Fact]
    public void Refund_PartialRefundSplitsProportionallyByEscrowComposition()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 100m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contractId,
            DepositedTokens = 60m,
            EarnedTokens = 40m
        };

        var refundedTokens = ContractEscrowWalletWorkflow.Refund(
            context, client, escrow, contractId, null,
            50_000m, "refund-partial", "InternalTokenWallet", "Partial refund", Now);

        Assert.Equal(50m, refundedTokens);
        Assert.Equal(50m, client.HeldTokens);
        Assert.Equal(30m, client.AvailableTokens);
        Assert.Equal(20m, client.WithdrawableTokens);
        Assert.Equal(30m, escrow.DepositedTokens);
        Assert.Equal(20m, escrow.EarnedTokens);

        var refund = Assert.Single(transactions.Entities);
        Assert.Equal(30m, refund.DepositedAmount);
        Assert.Equal(20m, refund.EarnedAmount);
    }

    [Fact]
    public void Refund_LegacyEscrowWithoutSourceTrackingRestoresToDepositedPool()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 100m
        };
        // Pre-source-tracking escrow: composition 0/0 falls back to all-deposited.
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contractId,
            DepositedTokens = 0m,
            EarnedTokens = 0m
        };

        var refundedTokens = ContractEscrowWalletWorkflow.Refund(
            context, client, escrow, contractId, null,
            50_000m, "refund-legacy", "InternalTokenWallet", "Legacy refund", Now);

        Assert.Equal(50m, refundedTokens);
        Assert.Equal(50m, client.HeldTokens);
        Assert.Equal(50m, client.AvailableTokens);
        Assert.Equal(0m, client.WithdrawableTokens);
        Assert.Equal((int)WalletBalanceSource.HeldDeposited,
            Assert.Single(transactions.Entities).BalanceSource);
    }

    [Fact]
    public void Release_TrackedEscrowCompositionCannotBeOverdrawnEvenWhenWalletHeldTotalIsSufficient()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = 100m
        };
        var freelancer = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = Guid.NewGuid(),
            DepositedTokens = 30m,
            EarnedTokens = 20m
        };

        Assert.Throws<BadRequestException>(() =>
            ContractEscrowWalletWorkflow.Release(
                context, client, freelancer, escrow, escrow.ContractsId, null,
                60_000m, "release-overdraw", "InternalTokenWallet", "Invalid release", Now));

        Assert.Equal(100m, client.HeldTokens);
        Assert.Equal(0m, freelancer.WithdrawableTokens);
        Assert.Equal(30m, escrow.DepositedTokens);
        Assert.Equal(20m, escrow.EarnedTokens);
    }

    [Fact]
    public void Release_SecondReleaseAfterHeldExhaustedThrowsAndDoesNotDoubleCredit()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = 100m
        };
        var freelancer = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 100m,
            EarnedTokens = 0m
        };

        ContractEscrowWalletWorkflow.Release(
            context, client, freelancer, escrow, contractId, null,
            100_000m, "release-dup", "InternalTokenWallet", "First release", Now);
        Assert.Equal(99m, freelancer.WithdrawableTokens);

        Assert.Throws<BadRequestException>(() =>
            ContractEscrowWalletWorkflow.Release(
                context, client, freelancer, escrow, contractId, null,
                100_000m, "release-dup-2", "InternalTokenWallet", "Duplicate release", Now));

        Assert.Equal(99m, freelancer.WithdrawableTokens);
        Assert.Equal(0m, client.HeldTokens);
    }

    [Fact]
    public void Refund_SecondRefundAfterHeldExhaustedThrowsAndDoesNotDoubleRestore()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 100m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contractId,
            DepositedTokens = 100m,
            EarnedTokens = 0m
        };

        ContractEscrowWalletWorkflow.Refund(
            context, client, escrow, contractId, null,
            100_000m, "refund-dup", "InternalTokenWallet", "First refund", Now);
        Assert.Equal(100m, client.AvailableTokens);

        Assert.Throws<BadRequestException>(() =>
            ContractEscrowWalletWorkflow.Refund(
                context, client, escrow, contractId, null,
                100_000m, "refund-dup-2", "InternalTokenWallet", "Duplicate refund", Now));

        Assert.Equal(100m, client.AvailableTokens);
        Assert.Equal(0m, client.WithdrawableTokens);
        Assert.Equal(0m, client.HeldTokens);
    }

    [Fact]
    public void Release_MixedEscrowCompositionSplitsProportionallyAndCreditsNetEarned()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = 100m
        };
        var freelancer = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m
        };
        // 75% deposited / 25% earned composition.
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contractId,
            DepositedTokens = 75m,
            EarnedTokens = 25m
        };

        // Release 40 tokens (40,000 VND).
        var result = ContractEscrowWalletWorkflow.Release(
            context, client, freelancer, escrow, contractId, null,
            40_000m, "release-mixed", "InternalTokenWallet", "Milestone release", Now);

        Assert.Equal(40m, result.GrossTokens);
        // 30 deposited + 10 earned leave the client's held escrow proportionally.
        Assert.Equal(60m, client.HeldTokens);
        Assert.Equal(45m, escrow.DepositedTokens);
        Assert.Equal(15m, escrow.EarnedTokens);
        // The freelancer always receives the net as earned GigCoin.
        Assert.Equal(0m, freelancer.AvailableTokens);
        Assert.Equal(39.6m, freelancer.WithdrawableTokens);

        var clientRelease = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowRelease &&
            transaction.UserId == client.UserId));
        Assert.Equal(30m, clientRelease.DepositedAmount);
        Assert.Equal(10m, clientRelease.EarnedAmount);
        Assert.Equal((int)WalletBalanceSource.Combined, clientRelease.BalanceSource);
    }

    [Fact]
    public void Penalty_DebitsHeldEscrowBySourceWithoutCreditingAnyWallet()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 5m,
            WithdrawableTokens = 5m,
            HeldTokens = 100m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 60m,
            EarnedTokens = 40m
        };

        var debit = ContractEscrowWalletWorkflow.Penalty(
            context, client, escrow, contractId, milestoneId, Guid.NewGuid(),
            50_000m, "penalty-1", "Financial misconduct", Now);

        Assert.Equal(50m, client.HeldTokens);
        Assert.Equal(5m, client.AvailableTokens);
        Assert.Equal(5m, client.WithdrawableTokens);
        Assert.Equal(30m, escrow.DepositedTokens);
        Assert.Equal(20m, escrow.EarnedTokens);
        Assert.Same(debit, Assert.Single(transactions.Entities));
        Assert.Equal((int)WalletBalanceSource.Combined, debit.BalanceSource);
        Assert.Equal(30m, debit.DepositedAmount);
        Assert.Equal(20m, debit.EarnedAmount);
    }
}
