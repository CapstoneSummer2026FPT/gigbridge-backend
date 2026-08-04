namespace Application.Features.Admin.Reconciliation.Common.DTOs;

/// <summary>
/// Read-only financial reconciliation report over the contract economy. Never mutates
/// wallet, ledger, escrow, or contract state. Surfaces existing deflated escrows from the
/// legacy G-coin/VND unit bug so admins can inspect them without a destructive migration.
/// </summary>
public sealed record EscrowReconciliationReport(
    IReadOnlyList<EscrowFundingDriftItem> EscrowFundingDrift,
    IReadOnlyList<EscrowCompositionDriftItem> EscrowCompositionDrift,
    IReadOnlyList<MilestonePlanDriftItem> MilestonePlanDrift,
    IReadOnlyList<WalletPoolDriftItem> WalletPoolDrift,
    ReconciliationSummary Summary);

/// <summary>
/// Escrow where the funded amount does not match the required (budget) amount.
/// A <see cref="LikelyDeflatedFunding"/> flag marks the exact legacy signature
/// FundedAmount * 1000 == RequiredAmount (e.g. 0.2 funded against a 200 G-coin budget).
/// </summary>
public sealed record EscrowFundingDriftItem(
    Guid ContractEscrowId,
    Guid ContractsId,
    string ContractTitle,
    int Status,
    decimal RequiredAmount,
    decimal FundedAmount,
    decimal FundingDelta,
    bool LikelyDeflatedFunding);

/// <summary>
/// Escrow where the escrow composition invariant breaks:
/// DepositedTokens + EarnedTokens + ReleasedAmount != FundedAmount.
/// The invariant holds after fund, release, refund, amendment, and dispute flows.
/// </summary>
public sealed record EscrowCompositionDriftItem(
    Guid ContractEscrowId,
    Guid ContractsId,
    string ContractTitle,
    decimal DepositedTokens,
    decimal EarnedTokens,
    decimal ReleasedAmount,
    decimal FundedAmount,
    decimal CompositionDelta,
    bool LikelyDeflated);

/// <summary>
/// Contract whose total budget does not match the sum of its milestone amounts.
/// A valid contract plan keeps TotalBudget == sum(Milestone.Amount).
/// </summary>
public sealed record MilestonePlanDriftItem(
    Guid ContractsId,
    string ContractTitle,
    decimal TotalBudget,
    decimal MilestoneTotal,
    int MilestoneCount,
    decimal Delta);

/// <summary>
/// Per-user drift between the wallet pools and the expected pools reconstructed from the
/// succeeded ledger. A zero-drift row is healthy; nonzero drift on a user with unclassified
/// adjustments should be verified against <see cref="UnclassifiedAdjustmentCount"/>.
/// </summary>
public sealed record WalletPoolDriftItem(
    Guid UserId,
    decimal AvailableTokens,
    decimal ExpectedAvailable,
    decimal WithdrawableTokens,
    decimal ExpectedWithdrawable,
    decimal HeldTokens,
    decimal ExpectedHeld,
    decimal PendingWithdrawalTokens,
    decimal ExpectedPending,
    int UnclassifiedAdjustmentCount);

public sealed record ReconciliationSummary(
    int EscrowCount,
    int ContractCount,
    int WalletCount,
    int FundingDriftCount,
    int CompositionDriftCount,
    int MilestonePlanDriftCount,
    int WalletDriftCount,
    int UnclassifiedAdjustmentCount);
