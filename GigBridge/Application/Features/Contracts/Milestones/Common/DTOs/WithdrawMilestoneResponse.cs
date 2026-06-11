namespace Application.Features.Contracts.Milestones.Common.DTOs;

public sealed record WithdrawMilestoneResponse(
    Guid ContractId,
    Guid MilestoneId,
    Guid EscrowId,
    decimal ReleasedAmountVnd,
    decimal ReleasedTokens,
    decimal MilestoneReleasedAmountVnd,
    decimal EscrowReleasedAmountVnd,
    int EscrowStatus);
