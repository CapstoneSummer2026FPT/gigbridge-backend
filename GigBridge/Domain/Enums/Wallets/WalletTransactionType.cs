namespace Domain.Enums.Wallets;

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
    WithdrawalFee = 9,
    SubscriptionPurchase = 10,
    PromotionPurchase = 11,
    DisputePenalty = 12
}
