using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

/// <summary>
/// Builds the /wallet/history stat cards. The figures are role-shaped because a Client
/// and a Freelancer stand on opposite sides of the same escrow rows: see
/// <see cref="WalletTransactionsSummaryResponse"/> for why the two branches exist and why
/// earnings and bank withdrawals are reported as separate numbers.
/// </summary>
public sealed class GetWalletTransactionsSummaryQueryHandler :
    IRequestHandler<GetWalletTransactionsSummaryQuery, WalletTransactionsSummaryResponse>
{
    private const int Succeeded = (int)WalletTransactionStatus.Succeeded;
    private const int Pending = (int)WalletTransactionStatus.Pending;
    private const string GenericRole = "Generic";

    private readonly IApplicationDbContext _context;

    public GetWalletTransactionsSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WalletTransactionsSummaryResponse> Handle(
        GetWalletTransactionsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var role = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => user.UserId == request.UserId)
            .Select(user => (int?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (!role.HasValue)
        {
            throw new NotFoundException("User does not exist.");
        }

        // One grouped read instead of a sum-per-metric round trip. The grouping is bounded
        // by the enum cardinality (types x statuses x balance sources), so the result set
        // stays tiny no matter how long the account's history is.
        var buckets = await _context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.UserId == request.UserId)
            .GroupBy(transaction => new
            {
                transaction.Type,
                transaction.Status,
                transaction.BalanceSource
            })
            .Select(group => new SummaryBucket(
                group.Key.Type,
                group.Key.Status,
                group.Key.BalanceSource,
                group.Sum(transaction => transaction.TokenAmount),
                group.Count()))
            .ToListAsync(cancellationToken);

        var totalTopUps = SucceededSum(buckets, bucket => bucket.Type == (int)WalletTransactionType.TopUp);
        var pendingTransactionCount = buckets.Where(bucket => bucket.Status == Pending).Sum(bucket => bucket.Count);
        var totalTransactions = buckets.Sum(bucket => bucket.Count);

        return role.Value switch
        {
            (int)UserRole.Client => new WalletTransactionsSummaryResponse(
                nameof(UserRole.Client),
                totalTopUps,
                pendingTransactionCount,
                totalTransactions,
                await BuildClientSummaryAsync(request.UserId, buckets, cancellationToken),
                null),
            (int)UserRole.Freelancer => new WalletTransactionsSummaryResponse(
                nameof(UserRole.Freelancer),
                totalTopUps,
                pendingTransactionCount,
                totalTransactions,
                null,
                await BuildFreelancerSummaryAsync(request.UserId, buckets, cancellationToken)),
            _ => new WalletTransactionsSummaryResponse(
                GenericRole,
                totalTopUps,
                pendingTransactionCount,
                totalTransactions,
                null,
                null)
        };
    }

    private async Task<ClientWalletSummary> BuildClientSummaryAsync(
        Guid userId,
        IReadOnlyList<SummaryBucket> buckets,
        CancellationToken cancellationToken)
    {
        var pools = await LoadWalletPoolsAsync(userId, cancellationToken);

        return new ClientWalletSummary(
            SucceededSum(buckets, bucket => bucket.Type == (int)WalletTransactionType.EscrowHold),
            pools.HeldTokens,
            // The client's debit leg of a release. Both legs share Type/Status/Amount and are
            // told apart only by BalanceSource, so direction must come from the canonical rule.
            SucceededSum(buckets, bucket =>
                bucket.Type == (int)WalletTransactionType.EscrowRelease && !IsCredit(bucket)),
            SucceededSum(buckets, bucket => bucket.Type == (int)WalletTransactionType.EscrowRefund));
    }

    private async Task<FreelancerWalletSummary> BuildFreelancerSummaryAsync(
        Guid userId,
        IReadOnlyList<SummaryBucket> buckets,
        CancellationToken cancellationToken)
    {
        var pools = await LoadWalletPoolsAsync(userId, cancellationToken);
        var serviceFeesPaid = await LoadNetServiceFeesPaidAsync(userId, buckets, cancellationToken);

        return new FreelancerWalletSummary(
            // Income: the freelancer's credit leg of a release.
            SucceededSum(buckets, bucket =>
                bucket.Type == (int)WalletTransactionType.EscrowRelease && IsCredit(bucket)),
            // Outflow: bank payouts only. Escrow releases are deliberately NOT folded in here -
            // that conflation double-counted the same coins as they moved through the wallet.
            SucceededSum(buckets, bucket => bucket.Type == (int)WalletTransactionType.WithdrawalSuccess),
            pools.PendingWithdrawalTokens,
            serviceFeesPaid);
    }

    private async Task<WalletPools> LoadWalletPoolsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // A user may not have a wallet row yet; treat the live pools as empty rather than failing.
        var pools = await _context.Set<UserWallet>()
            .AsNoTracking()
            .Where(wallet => wallet.UserId == userId)
            .Select(wallet => new WalletPools(wallet.HeldTokens, wallet.PendingWithdrawalTokens))
            .FirstOrDefaultAsync(cancellationToken);

        return pools ?? new WalletPools(0m, 0m);
    }

    /// <summary>
    /// Service fees are Adjustment rows identified by an idempotency-key prefix, so they cannot
    /// be read from the type/status/source grouping. Cancelling a contract refunds the acceptance
    /// fee, so the figure is reported net and floored at zero.
    /// </summary>
    private async Task<decimal> LoadNetServiceFeesPaidAsync(
        Guid userId,
        IReadOnlyList<SummaryBucket> buckets,
        CancellationToken cancellationToken)
    {
        var releaseFeePrefix = ServiceFeeWorkflow.FreelancerReleaseFeePrefix;
        var acceptFeePrefix = ServiceFeeWorkflow.AcceptJobFeePrefix;

        var feesCharged = await _context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.Status == Succeeded &&
                transaction.Type == (int)WalletTransactionType.Adjustment &&
                transaction.IdempotencyKey != null &&
                (transaction.IdempotencyKey.StartsWith(releaseFeePrefix) ||
                 transaction.IdempotencyKey.StartsWith(acceptFeePrefix)))
            .SumAsync(transaction => transaction.TokenAmount, cancellationToken);

        var feesRefunded = SucceededSum(
            buckets,
            bucket => bucket.Type == (int)WalletTransactionType.ServiceFeeRefund);

        return Math.Max(0m, feesCharged - feesRefunded);
    }

    private static bool IsCredit(SummaryBucket bucket) =>
        WalletTransactionDirection.IsCredit(bucket.Type, bucket.BalanceSource);

    private static decimal SucceededSum(
        IReadOnlyList<SummaryBucket> buckets,
        Func<SummaryBucket, bool> predicate) =>
        buckets
            .Where(bucket => bucket.Status == Succeeded && predicate(bucket))
            .Sum(bucket => bucket.Amount);

    private sealed record SummaryBucket(int Type, int Status, int BalanceSource, decimal Amount, int Count);

    private sealed record WalletPools(decimal HeldTokens, decimal PendingWithdrawalTokens);
}
