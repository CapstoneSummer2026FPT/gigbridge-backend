using Domain.Enums.Wallets;

namespace Application.Features.Admin.Reconciliation.Common.Internal;

/// <summary>
/// Reconstructs the expected wallet pools from the succeeded wallet ledger so the admin
/// reconciliation report can detect pool/ledger drift. Pure functions only — no I/O.
///
/// Every producer of a wallet transaction was verified against the pool mutations it makes:
/// </summary>
internal static class ReconciliationLedger
{
    /// <summary>Rounding tolerance for drift detection (G-coin, 4dp ledger precision).</summary>
    public const decimal Tolerance = 0.0001m;

    /// <summary>Adjustment whose token amount is already netted out of the credited pool (ledger-only).</summary>
    private const string FreelancerReleaseFeePrefix = "SERVICE-FEE-RELEASE-";

    public static bool Drifts(decimal actual, decimal expected) =>
        Math.Abs(actual - expected) > Tolerance;

    /// <summary>
    /// Classifies an Adjustment transaction's pool effect. Service-fee adjustments for
    /// funding/accepting/ending (SERVICE-FEE-FUND-/ACCEPT-/END-) and admin wallet updates
    /// spend the deposited pool first; the freelancer release fee (SERVICE-FEE-RELEASE-)
    /// is already withheld from the credited net amount and has no pool effect.
    /// </summary>
    public static AdjustmentKind ClassifyAdjustment(string? idempotencyKey)
    {
        if (idempotencyKey is null)
        {
            return AdjustmentKind.Unclassified;
        }

        if (idempotencyKey.StartsWith(FreelancerReleaseFeePrefix, StringComparison.Ordinal))
        {
            return AdjustmentKind.LedgerOnlyReleaseFee;
        }

        if (idempotencyKey.StartsWith("SERVICE-FEE-", StringComparison.Ordinal))
        {
            return AdjustmentKind.SpendDebit;
        }

        // The only non-service-fee Adjustment producer is the admin wallet update, which
        // spends the deposited pool first. A legacy/migration row would fall here too and
        // is surfaced via the unclassified counter so drift stays attributable.
        return AdjustmentKind.Unclassified;
    }

    /// <summary>
    /// Signed pool effect of one succeeded transaction. Deposit-side spending is applied
    /// through the transaction's recorded split; legacy rows without a split fall back to
    /// the balance source (earned vs. everything else).
    /// </summary>
    public static PoolDelta DeltaFor(WalletTransactionRow row)
    {
        var tokenAmount = row.TokenAmount;
        return (WalletTransactionType)row.Type switch
        {
            WalletTransactionType.AdminCredit or WalletTransactionType.TopUp =>
                new PoolDelta(tokenAmount, 0m, 0m, 0m),
            WalletTransactionType.EscrowHold =>
                new PoolDelta(-Deposited(row), -Earned(row), tokenAmount, 0m),
            WalletTransactionType.EscrowRelease => IsFreelancerCredit(row)
                ? new PoolDelta(0m, tokenAmount, 0m, 0m)
                : new PoolDelta(0m, 0m, -tokenAmount, 0m),
            WalletTransactionType.EscrowRefund =>
                new PoolDelta(Deposited(row), Earned(row), -tokenAmount, 0m),
            WalletTransactionType.Adjustment => ClassifyAdjustment(row.IdempotencyKey) switch
            {
                AdjustmentKind.LedgerOnlyReleaseFee => PoolDelta.Zero,
                _ => new PoolDelta(-Deposited(row), -Earned(row), 0m, 0m)
            },
            WalletTransactionType.WithdrawalLock =>
                new PoolDelta(0m, -tokenAmount, 0m, tokenAmount),
            WalletTransactionType.WithdrawalSuccess =>
                new PoolDelta(0m, 0m, 0m, -tokenAmount),
            WalletTransactionType.WithdrawalRefund =>
                new PoolDelta(0m, tokenAmount, 0m, -tokenAmount),
            // The withdrawal fee is fiat-side only and never creates a wallet transaction.
            WalletTransactionType.WithdrawalFee => PoolDelta.Zero,
            WalletTransactionType.SubscriptionPurchase or WalletTransactionType.PromotionPurchase =>
                new PoolDelta(-Deposited(row), -Earned(row), 0m, 0m),
            WalletTransactionType.DisputePenalty =>
                new PoolDelta(0m, 0m, -tokenAmount, 0m),
            _ => PoolDelta.Zero
        };
    }

    /// <summary>Freelancer escrow credits carry the Earned source; client releases carry a Held source.</summary>
    private static bool IsFreelancerCredit(WalletTransactionRow row) =>
        row.BalanceSource == (int)WalletBalanceSource.Earned;

    private static decimal Deposited(WalletTransactionRow row) =>
        row.DepositedAmount ?? (row.BalanceSource == (int)WalletBalanceSource.Earned ? 0m : row.TokenAmount);

    private static decimal Earned(WalletTransactionRow row) =>
        row.EarnedAmount ?? (row.BalanceSource == (int)WalletBalanceSource.Earned ? row.TokenAmount : 0m);
}

public enum AdjustmentKind
{
    /// <summary>Release-fee adjustment already netted into the credited pool.</summary>
    LedgerOnlyReleaseFee,

    /// <summary>Known service-fee adjustment that spends the deposited pool first.</summary>
    SpendDebit,

    /// <summary>Adjustment with an unrecognized idempotency key (admin update or legacy row).</summary>
    Unclassified
}

public readonly record struct PoolDelta(decimal Available, decimal Withdrawable, decimal Held, decimal Pending)
{
    public static PoolDelta Zero => new(0m, 0m, 0m, 0m);
}

public sealed record WalletTransactionRow(
    Guid UserId,
    int Type,
    decimal TokenAmount,
    int BalanceSource,
    decimal? DepositedAmount,
    decimal? EarnedAmount,
    string? IdempotencyKey);
