using Application.Features.Wallets.Common.GetTransactions.Queries;
using Domain.Entities;
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

        var result = await Handle(context);

        Assert.Equal(1_200m, result.TotalDeposits);
        Assert.Equal(120, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_CountsOnlySucceededTransactionsForDepositsEscrowAndWithdrawals()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Succeeded, 100m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Cancelled, 50m),
            Tx(_userId, (int)WalletTransactionType.TopUp, (int)WalletTransactionStatus.Failed, 25m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Succeeded, 200m),
            Tx(_userId, (int)WalletTransactionType.EscrowHold, (int)WalletTransactionStatus.Pending, 40m),
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Succeeded, 30m),
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Cancelled, 70m));

        var result = await Handle(context);

        Assert.Equal(100m, result.TotalDeposits);
        Assert.Equal(200m, result.TotalEscrow);
        Assert.Equal(30m, result.TotalWithdrawn);
    }

    [Fact]
    public async Task Handle_TotalWithdrawnIncludesFreelancerCreditedEscrowReleases()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            // Freelancer's own credited row from a milestone withdrawal / dispute-resolution
            // release: BalanceSource=Earned. Must count toward Total Withdrawn.
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 20m,
                (int)WalletBalanceSource.Earned),
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 15m,
                (int)WalletBalanceSource.Earned),
            // A literal bank/gateway cash-out still counts too.
            Tx(_userId, (int)WalletTransactionType.WithdrawalSuccess, (int)WalletTransactionStatus.Succeeded, 5m));

        var result = await Handle(context);

        Assert.Equal(40m, result.TotalWithdrawn);
    }

    [Fact]
    public async Task Handle_TotalWithdrawnExcludesTheClientsDebitSideOfAnEscrowRelease()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            // The client's own row for the same release event: a Held* source, not Earned.
            // This is money leaving their escrow, not something *they* withdrew.
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Succeeded, 20m,
                (int)WalletBalanceSource.HeldDeposited));

        var result = await Handle(context);

        Assert.Equal(0m, result.TotalWithdrawn);
    }

    [Fact]
    public async Task Handle_TotalWithdrawnIgnoresNonSucceededEscrowReleases()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowRelease, (int)WalletTransactionStatus.Pending, 20m,
                (int)WalletBalanceSource.Earned));

        var result = await Handle(context);

        Assert.Equal(0m, result.TotalWithdrawn);
    }

    [Fact]
    public async Task Handle_RefundsIncludesBothEscrowRefundAndWithdrawalRefund()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>(
            Tx(_userId, (int)WalletTransactionType.EscrowRefund, (int)WalletTransactionStatus.Succeeded, 5m),
            Tx(_userId, (int)WalletTransactionType.WithdrawalRefund, (int)WalletTransactionStatus.Succeeded, 7m),
            Tx(_userId, (int)WalletTransactionType.EscrowRefund, (int)WalletTransactionStatus.Pending, 9m));

        var result = await Handle(context);

        Assert.Equal(12m, result.TotalRefunds);
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

        var result = await Handle(context);

        Assert.Equal(2, result.PendingCount);
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

        var result = await Handle(context);

        Assert.Equal(100m, result.TotalDeposits);
        Assert.Equal(200m, result.TotalEscrow);
        Assert.Equal(0m, result.TotalWithdrawn);
        Assert.Equal(2, result.TotalTransactions);
    }

    [Fact]
    public async Task Handle_EmptyAccountReturnsZeros()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();

        var result = await Handle(context);

        Assert.Equal(0m, result.TotalDeposits);
        Assert.Equal(0m, result.TotalEscrow);
        Assert.Equal(0m, result.TotalRefunds);
        Assert.Equal(0m, result.TotalWithdrawn);
        Assert.Equal(0, result.PendingCount);
        Assert.Equal(0, result.TotalTransactions);
    }

    private async Task<WalletTransactionsSummaryResponse> Handle(InMemoryApplicationDbContext context)
    {
        var handler = new GetWalletTransactionsSummaryQueryHandler(context);
        return await handler.Handle(new GetWalletTransactionsSummaryQuery(_userId), CancellationToken.None);
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
