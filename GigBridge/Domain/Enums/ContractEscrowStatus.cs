namespace Domain.Enums;

public enum ContractEscrowStatus
{
    PendingFunding = 0,
    PartiallyFunded = 1,
    Funded = 2,
    PartiallyReleased = 3,
    Released = 4,
    Refunded = 5,
    Cancelled = 6,
    Disputed = 7
}
