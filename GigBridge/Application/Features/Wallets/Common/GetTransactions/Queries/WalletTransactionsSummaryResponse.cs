namespace Application.Features.Wallets.Common.GetTransactions.Queries;

/// <summary>
/// Lifetime wallet aggregates for the current user. Unlike the paginated
/// transaction list (capped at 100), these sums cover the account's entire
/// history so the /wallet/history stat cards show real totals.
/// </summary>
public sealed record WalletTransactionsSummaryResponse(
    decimal TotalDeposits,
    decimal TotalEscrow,
    decimal TotalRefunds,
    decimal TotalWithdrawn,
    int PendingCount,
    int TotalTransactions);
