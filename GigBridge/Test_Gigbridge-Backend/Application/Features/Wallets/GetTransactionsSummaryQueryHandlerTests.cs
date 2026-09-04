using Application.Common.Exceptions;
using Application.Features.Wallets.Common;
using Application.Features.Wallets.Common.GetTransactions.Queries;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Wallets;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Wallets;

public class GetTransactionsSummaryQueryHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Handle_AggregatesLifetimeTotalsAcrossAllTransactions_NotLimitedTo100()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        // More than 100 transactions: the summary must count all of them.
        for (var i = 0; i < 120; i++)
        {
            transactions.Add(Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 10m));
        }

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(1_200m, result.TotalTopUps);
        Assert.Equal(120, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_CountsOnlySucceededTransactionsForClientMetrics()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 100m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Cancelled, 50m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Failed, 25m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Succeeded, 200m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Pending, 40m));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(100m, result.TotalTopUps);
        Assert.Equal(200m, result.Client!.TotalEscrowFunded);
    }

    [Fact]
    public async Task Handle_CountsOnlySucceededTransactionsForFreelancerMetrics()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Succeeded, 30m),
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Cancelled, 70m));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(30m, result.Freelancer!.TotalWithdrawnToBank);
    }

    [Fact]
    public async Task Handle_FreelancerEarningsAndBankWithdrawalsAreSeparateMetrics()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            // The freelancer's credited rows from milestone releases: BalanceSource=Earned.
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 20m,
                (int)WalletBalanceSource.Earned),
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 15m,
                (int)WalletBalanceSource.Earned),
            // A literal bank cash-out is a different stage of the money's life, not more income.
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Succeeded, 5m));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(35m, result.Freelancer!.TotalEarnedFromEscrow);
        Assert.Equal(5m, result.Freelancer!.TotalWithdrawnToBank);
    }

    /// <summary>
    /// Regression guard for the original defect: "Total Withdrawn" summed WithdrawalSuccess
    /// together with the freelancer's credited EscrowRelease rows, so a freelancer who earned
    /// 1,000,000 and then withdrew that same 1,000,000 was reported as 2,000,000. The two
    /// stages must stay separate fields and must never be folded into one number.
    /// </summary>
    [Fact]
    public async Task Handle_EarnThenWithdrawTheSameCoinsIsNeverCountedTwice()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 1_000_000m,
                (int)WalletBalanceSource.Earned),
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Succeeded,
                1_000_000m, (int)WalletBalanceSource.PendingWithdrawal));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(1_000_000m, result.Freelancer!.TotalEarnedFromEscrow);
        Assert.Equal(1_000_000m, result.Freelancer!.TotalWithdrawnToBank);
        Assert.NotEqual(2_000_000m, result.Freelancer!.TotalWithdrawnToBank);
    }

    [Fact]
    public async Task Handle_ClientReleaseLegCountsAsPaidToFreelancersNotAsEarnings()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            // The client's own row for a release: a Held* source, never Earned. This is money
            // leaving their escrow toward a freelancer, not something they earned or withdrew.
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 20m,
                (int)WalletBalanceSource.HeldDeposited));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(20m, result.Client!.TotalReleasedToFreelancers);
        Assert.Null(result.Freelancer);
    }

    [Fact]
    public async Task Handle_IgnoresNonSucceededEscrowReleases()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Pending, 20m,
                (int)WalletBalanceSource.Earned));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(0m, result.Freelancer!.TotalEarnedFromEscrow);
    }

    [Fact]
    public async Task Handle_ClientRefundsCoverEscrowRefundsOnly()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowRefund, (int)WalletTransactionStatus.Succeeded, 5m),
            Tx(_userId, (int)WalletTransactionType.EscrowRefund, (int)WalletTransactionStatus.Pending, 9m));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(5m, result.Client!.TotalEscrowRefunds);
    }

    [Fact]
    public async Task Handle_WithdrawalRefundIsNotCountedAsBankWithdrawal()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.WithdrawalRefund, (int)WalletTransactionStatus.Succeeded, 50m,
                (int)WalletBalanceSource.Earned));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(0m, result.Freelancer!.TotalWithdrawnToBank);
    }

    [Fact]
    public async Task Handle_PendingCountCountsOnlyPendingStatusTransactions()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Pending, 1m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Pending, 2m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 3m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Failed, 4m));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(2, result.PendingTransactionCount);
        Assert.Equal(4, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_CancelledAndFailedRowsAreExcludedFromAmountsButCountedInTotalTransactions()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Cancelled, 11m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Failed, 22m),
            Tx(_userId, (int)WalletTransactionType.EscrowRefund, (int)WalletTransactionStatus.Cancelled, 33m),
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Failed, 44m,
                (int)WalletBalanceSource.HeldDeposited));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(0m, result.TotalTopUps);
        Assert.Equal(0m, result.Client!.TotalEscrowFunded);
        Assert.Equal(0m, result.Client!.TotalEscrowRefunds);
        Assert.Equal(0m, result.Client!.TotalReleasedToFreelancers);
        Assert.Equal(4, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_IsScopedToRequestingUserOnly()
    {
        var otherUser = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 100m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Succeeded, 200m),
            Tx(otherUser, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 999m),
            Tx(otherUser, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Succeeded, 999m));

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(100m, result.TotalTopUps);
        Assert.Equal(200m, result.Client!.TotalEscrowFunded);
        Assert.Equal(2, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_ClientSummaryHasNoFreelancerBranch()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(nameof(UserRole.Client), result.Role);
        Assert.NotNull(result.Client);
        Assert.Null(result.Freelancer);
    }

    [Fact]
    public async Task Handle_FreelancerSummaryHasNoClientBranch()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(nameof(UserRole.Freelancer), result.Role);
        Assert.NotNull(result.Freelancer);
        Assert.Null(result.Client);
    }

    [Fact]
    public async Task Handle_AdminReceivesGenericSummaryWithNoRoleBranch()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 12m));

        var result = await Handle(context, UserRole.Admin);

        Assert.Equal("Generic", result.Role);
        Assert.Null(result.Client);
        Assert.Null(result.Freelancer);
        Assert.Equal(12m, result.TotalTopUps);
    }

    [Fact]
    public async Task Handle_MissingUserThrowsNotFound()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();
        context.AddSet<User>();

        var handler = new GetWalletTransactionsSummaryQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetWalletTransactionsSummaryQuery(_userId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CurrentEscrowHeldComesFromWalletPoolNotCumulativeHolds()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Succeeded, 100m),
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 60m,
                (int)WalletBalanceSource.HeldDeposited));
        context.AddSet(Wallet(_userId, heldTokens: 40m));

        var result = await Handle(context, UserRole.Client, seedWallet: false);

        // Lifetime funding only ever grows; the live pool reflects what is actually in escrow now.
        Assert.Equal(100m, result.Client!.TotalEscrowFunded);
        Assert.Equal(40m, result.Client!.CurrentEscrowHeld);
    }

    [Fact]
    public async Task Handle_CurrentPendingWithdrawalComesFromWalletPoolNotTransactionCount()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Pending, 1m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Pending, 2m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Pending, 3m));
        context.AddSet(Wallet(_userId, pendingWithdrawalTokens: 250m));

        var result = await Handle(context, UserRole.Freelancer, seedWallet: false);

        Assert.Equal(250m, result.Freelancer!.CurrentPendingWithdrawal);
        Assert.Equal(3, result.PendingTransactionCount);
    }

    [Fact]
    public async Task Handle_MissingWalletRowYieldsZeroPools()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();
        context.AddSet<UserWallet>();

        var result = await Handle(context, UserRole.Freelancer, seedWallet: false);

        Assert.Equal(0m, result.Freelancer!.CurrentPendingWithdrawal);
    }

    [Fact]
    public async Task Handle_ServiceFeesPaidIsNetOfServiceFeeRefunds()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Fee(_userId, ServiceFeeWorkflow.AcceptJobFeePrefix + "abc", 10m),
            Tx(_userId, (int)WalletTransactionType.ServiceFeeRefund, (int)WalletTransactionStatus.Succeeded, 10m));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(0m, result.Freelancer!.TotalServiceFeesPaid);
    }

    [Fact]
    public async Task Handle_ServiceFeesPaidCountsReleaseAndAcceptanceFees()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Fee(_userId, ServiceFeeWorkflow.AcceptJobFeePrefix + "abc", 10m),
            Fee(_userId, ServiceFeeWorkflow.FreelancerReleaseFeePrefix + "def", 7m));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(17m, result.Freelancer!.TotalServiceFeesPaid);
    }

    [Fact]
    public async Task Handle_ServiceFeesPaidIgnoresAdjustmentRowsWithoutAServiceFeePrefix()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Fee(_userId, "MANUAL-ADJUSTMENT-xyz", 99m),
            // The client-side funding fee belongs to the client, never to a freelancer.
            Fee(_userId, ServiceFeeWorkflow.ClientFundingFeePrefix + "ghi", 5m));

        var result = await Handle(context, UserRole.Freelancer);

        Assert.Equal(0m, result.Freelancer!.TotalServiceFeesPaid);
    }

    [Fact]
    public async Task Handle_EmptyAccountReturnsZeros()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();

        var result = await Handle(context, UserRole.Client);

        Assert.Equal(0m, result.TotalTopUps);
        Assert.Equal(0, result.PendingTransactionCount);
        Assert.Equal(0, result.TotalTransactions);
        Assert.Equal(0m, result.Client!.TotalEscrowFunded);
        Assert.Equal(0m, result.Client!.CurrentEscrowHeld);
        Assert.Equal(0m, result.Client!.TotalReleasedToFreelancers);
        Assert.Equal(0m, result.Client!.TotalEscrowRefunds);
    }

    private async Task<WalletTransactionsSummaryResponse> Handle(
        InMemoryApplicationDbContext context,
        UserRole role,
        bool seedWallet = true)
    {
        context.AddSet(TestUser(_userId, role));
        if (seedWallet)
        {
            context.AddSet(Wallet(_userId));
        }

        var handler = new GetWalletTransactionsSummaryQueryHandler(context);
        return await handler.Handle(new GetWalletTransactionsSummaryQuery(_userId), CancellationToken.None);
    }

    private static User TestUser(Guid userId, UserRole role) => new()
    {
        UserId = userId,
        FullName = "Test User",
        Email = $"{userId:N}@example.test",
        Role = (int)role,
        IsActive = true
    };

    private static UserWallet Wallet(
        Guid userId,
        decimal heldTokens = 0m,
        decimal pendingWithdrawalTokens = 0m) => new()
    {
        UserWalletsId = Guid.NewGuid(),
        UserId = userId,
        HeldTokens = heldTokens,
        PendingWithdrawalTokens = pendingWithdrawalTokens
    };

    private static WalletTransaction Fee(Guid userId, string idempotencyKey, decimal amount)
    {
        var transaction = Tx(
            userId,
            (int)WalletTransactionType.Adjustment,
            (int)WalletTransactionStatus.Succeeded,
            amount);
        transaction.IdempotencyKey = idempotencyKey;
        return transaction;
    }

    private static WalletTransaction Tx(Guid userId, int type, int status, decimal amount, int balanceSource = 0) => new()
    {
        WalletTransactionsId = Guid.NewGuid(),
        UserWalletsId = Guid.NewGuid(),
        UserId = userId,
        TokenAmount = amount,
        VndAmount = 0m,
        Type = type,
        Status = status,
        BalanceSource = balanceSource,
        CreatedAt = new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc)
    };
}
