namespace Application.Features.Contracts.Completion.Freelancer.DTOs;

public sealed record ClaimFinalPayoutResponse(
    Guid ContractId,
    decimal ReleasedAmountVnd,
    decimal ReleasedTokens,
    decimal EscrowReleasedAmountVnd,
    int EscrowStatus,
    bool AlreadyClaimed,
    DateTime? ClaimedAt);
