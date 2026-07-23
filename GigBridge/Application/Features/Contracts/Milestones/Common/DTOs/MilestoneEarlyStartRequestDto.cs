namespace Application.Features.Contracts.Milestones.Common.DTOs;

public sealed record MilestoneEarlyStartRequestDto(
    Guid RequestId,
    Guid ContractId,
    Guid MilestoneId,
    string Reason,
    string? ResponseNote,
    int Status,
    DateTime CreatedAt,
    DateTime? RespondedAt);
