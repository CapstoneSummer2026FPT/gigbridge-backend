namespace Domain.Enums;

public enum WithdrawalStatus
{
    Pending = 0,
    Processing = 1,
    SyncRequired = 2,
    Success = 3,
    Failed = 4,
    Cancelled = 5
}
