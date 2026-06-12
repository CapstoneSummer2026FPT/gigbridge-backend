namespace Application.Features.Contracts.Common.DTOs;

public sealed record ContractWorkflowResponse(
    Guid ContractId,
    int Status,
    Guid? EscrowId,
    Guid? EsignDocumentId);
