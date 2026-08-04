namespace Domain.Enums;

/// <summary>
/// Identifies which wallet balance pool funded or absorbed a transaction.
/// Deposited = non-withdrawable spendable tokens, Earned = withdrawable earned
/// tokens. Mixed payments are recorded as Combined with the allocation broken
/// out in WalletTransaction.DepositedAmount / EarnedAmount.
/// </summary>
public enum WalletBalanceSource
{
    /// <summary>AvailableTokens — purchased/deposited, spendable but not withdrawable.</summary>
    Deposited = 0,

    /// <summary>WithdrawableTokens — earned from completed work, spendable and withdrawable.</summary>
    Earned = 1,

    /// <summary>HeldTokens funded from the deposited balance (escrow).</summary>
    HeldDeposited = 2,

    /// <summary>HeldTokens funded from the earned balance (escrow).</summary>
    HeldEarned = 3,

    /// <summary>PendingWithdrawalTokens — earned locked in a withdrawal request.</summary>
    PendingWithdrawal = 4,

    /// <summary>Payment split across deposited and earned balances.</summary>
    Combined = 5
}
