namespace Domain.Enums;

public enum WalletTransactionType
{
    AdminCredit = 0,
    TopUp = 1,
    EscrowHold = 2,
    EscrowRelease = 3,
    EscrowRefund = 4,
    Adjustment = 5,
    WithdrawalLock = 6,
    WithdrawalSuccess = 7,
    WithdrawalRefund = 8,
    WithdrawalFee = 9
}
