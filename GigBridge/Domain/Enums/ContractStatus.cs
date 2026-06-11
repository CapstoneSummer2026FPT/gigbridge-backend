namespace Domain.Enums;

public enum ContractStatus
{
    Draft = 0,
    PendingFreelancerSelection = 1,
    PendingEscrow = 2,
    PendingSignature = 3,
    Active = 4,
    Completed = 5,
    Cancelled = 6,
    Disputed = 7
}
