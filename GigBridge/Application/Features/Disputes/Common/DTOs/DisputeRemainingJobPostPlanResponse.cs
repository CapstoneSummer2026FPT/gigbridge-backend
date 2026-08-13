namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeRemainingMilestonePlanResponse(
    string Title,
    string? Description,
    decimal Amount,
    string? EstimatedDuration,
    DateOnly? DueDate,
    string? Deliverables,
    string? AcceptanceCriteria,
    int OrderIndex);

public sealed record DisputeRemainingJobPostPlanResponse(
    Guid ContractId,
    Guid OriginalJobPostId,
    string Title,
    string Description,
    Guid? MajorCategoryId,
    string? Currency,
    int? Visibility,
    string[] CustomSkillNames,
    Guid[] SkillIds,
    decimal TotalRemainingBudget,
    string EstimatedDuration,
    DateTime EndDate,
    DateTime DisputeResolvedAt,
    IReadOnlyList<DisputeRemainingMilestonePlanResponse> RemainingMilestones);
