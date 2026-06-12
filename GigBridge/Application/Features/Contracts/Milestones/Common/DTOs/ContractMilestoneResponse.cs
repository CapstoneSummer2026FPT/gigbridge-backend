namespace Application.Features.Contracts.Milestones.Common.DTOs;

public sealed record ContractMilestoneResponse(
    Guid MilestoneId,
    Guid ContractId,
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int Status,
    int? SortOrder,
    DateTime? StartedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    decimal ReleasedAmount,
    DateTime? LastReleasedAt);
