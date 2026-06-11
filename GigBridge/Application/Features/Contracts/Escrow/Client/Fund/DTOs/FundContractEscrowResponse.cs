namespace Application.Features.Contracts.Escrow.Client.Fund.DTOs;

public sealed record FundContractEscrowResponse(
    Guid ContractId,
    Guid EscrowId,
    decimal RequiredAmountVnd,
    decimal HeldTokens,
    int ContractStatus,
    int EscrowStatus);
