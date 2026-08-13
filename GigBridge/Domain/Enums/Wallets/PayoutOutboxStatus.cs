namespace Domain.Enums.Wallets;

public enum PayoutOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Delivered = 2,
    DeadLettered = 3,
    Cancelled = 4
}
