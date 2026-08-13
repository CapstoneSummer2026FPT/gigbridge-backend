using Domain.Enums.Contracts;
namespace Application.Features.Contracts.Completion.Client.DTOs;

public sealed record EndProjectResponse(
    Guid ContractId,
    int ContractStatus,
    decimal ReleasedAmountVnd,
    decimal ReleasedTokens,
    decimal EscrowReleasedAmountVnd,
    DateTime? CompletedAt);

