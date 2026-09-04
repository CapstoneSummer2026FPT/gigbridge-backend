using Domain.Enums.Wallets;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

/// <summary>
/// Wallet stat-card figures for the current user, shaped by their role.
///
/// A Client and a Freelancer sit on opposite sides of every escrow movement, so a
/// single flat set of numbers cannot describe both: only a Client ever funds escrow
/// (<see cref="WalletTransactionType.EscrowHold"/>), and only a Freelancer may cash
/// out to a bank (every withdrawal endpoint is Freelancer-only). Exactly one of
/// <see cref="Client"/> / <see cref="Freelancer"/> is non-null, selected by
/// <see cref="Role"/>; an Admin gets "Generic" with neither branch.
///
/// Amounts are GigCoin (TokenAmount), never VND.
/// </summary>
/// <param name="Role">"Client", "Freelancer" or "Generic" — the branch discriminator.</param>
/// <param name="TotalTopUps">Lifetime sum of succeeded gateway top-ups.</param>
/// <param name="PendingTransactionCount">Count (not an amount) of transactions still Pending.</param>
/// <param name="TotalTransactions">Count of every ledger row, Failed and Cancelled included,
/// so it matches the transaction list rendered beneath the stat cards.</param>
public sealed record WalletTransactionsSummaryResponse(
    string Role,
    decimal TotalTopUps,
    int PendingTransactionCount,
    int TotalTransactions,
    ClientWalletSummary? Client,
    FreelancerWalletSummary? Freelancer);

/// <summary>
/// Client-side money flow. A client funds escrow and gets refunds; they can never withdraw.
/// </summary>
/// <param name="TotalEscrowFunded">Lifetime gross funded into escrow. Only ever grows.</param>
/// <param name="CurrentEscrowHeld">Live <c>UserWallet.HeldTokens</c> — what is in escrow
/// right now. Falls as milestones are released or refunded, unlike
/// <paramref name="TotalEscrowFunded"/>.</param>
/// <param name="TotalReleasedToFreelancers">Lifetime sum of the client's debit leg of escrow
/// releases — money that left their escrow and reached freelancers.</param>
/// <param name="TotalEscrowRefunds">Lifetime escrow refunds returned to this client.</param>
public sealed record ClientWalletSummary(
    decimal TotalEscrowFunded,
    decimal CurrentEscrowHeld,
    decimal TotalReleasedToFreelancers,
    decimal TotalEscrowRefunds);

/// <summary>
/// Freelancer-side money flow.
///
/// IMPORTANT: <paramref name="TotalEarnedFromEscrow"/> and <paramref name="TotalWithdrawnToBank"/>
/// describe the SAME coins at two different stages of their life — earned into the wallet, then
/// cashed out of it. They must never be summed or folded into one "total withdrawn" figure;
/// doing so reports a freelancer who earned and withdrew 1,000,000 as having moved 2,000,000.
/// </summary>
/// <param name="TotalEarnedFromEscrow">Lifetime income: the freelancer's credit leg of escrow
/// releases.</param>
/// <param name="TotalWithdrawnToBank">Lifetime outflow: succeeded bank/gateway payouts only.</param>
/// <param name="CurrentPendingWithdrawal">Live <c>UserWallet.PendingWithdrawalTokens</c> — an
/// amount locked in an in-flight payout, not a transaction count.</param>
/// <param name="TotalServiceFeesPaid">Lifetime platform service fees paid, net of service-fee
/// refunds issued when a contract is cancelled. Floored at zero.</param>
public sealed record FreelancerWalletSummary(
    decimal TotalEarnedFromEscrow,
    decimal TotalWithdrawnToBank,
    decimal CurrentPendingWithdrawal,
    decimal TotalServiceFeesPaid);
