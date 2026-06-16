namespace Domain.Enums;

public enum ContractStatus
{
    Draft = 0,
    PendingFreelancerSelection = 1,
    InNegotiation = 2,
    PendingContractDetails = 3,
    PendingContractConfirmation = 4,
    PendingEscrow = 5,
    PendingSignature = 6,
    Active = 7,
    Completed = 8,
    Cancelled = 9,
    Disputed = 10
}
