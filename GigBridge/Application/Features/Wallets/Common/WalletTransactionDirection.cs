using Domain.Enums.Wallets;

namespace Application.Features.Wallets.Common;

/// <summary>
/// Classifies whether a wallet transaction is a credit (incoming, positive for the owning
/// wallet) or a debit (outgoing, negative). Mirrors the same direction semantics already
/// trusted by <see cref="Application.Features.Admin.Reconciliation.Common.Internal.ReconciliationLedger"/>
/// for pool drift detection, so the two never disagree on what a row means.
///
/// <see cref="WalletTransactionType.EscrowRelease"/> is the one type that is dual-direction:
/// <see cref="ContractEscrowWalletWorkflow.Release"/> writes one row for the client (a debit —
/// funds leaving their held escrow) and one row for the freelancer (a credit — funds arriving
/// as earned/withdrawable balance), both with the same positive TokenAmount. The freelancer's
/// row is always stamped with <see cref="WalletBalanceSource.Earned"/>; the client's row never is.
/// </summary>
public static class WalletTransactionDirection
{
    public static bool IsCredit(int type, int balanceSource) => (WalletTransactionType)type switch
    {
        WalletTransactionType.AdminCredit or WalletTransactionType.TopUp
            or WalletTransactionType.EscrowRefund or WalletTransactionType.WithdrawalRefund
            or WalletTransactionType.ServiceFeeRefund => true,
        WalletTransactionType.EscrowRelease => balanceSource == (int)WalletBalanceSource.Earned,
        _ => false
    };
}
