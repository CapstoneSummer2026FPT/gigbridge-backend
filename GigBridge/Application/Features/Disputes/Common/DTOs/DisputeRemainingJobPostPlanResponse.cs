namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeRemainingWorkItemPlanResponse(
    string Title,
    string? Description,
    string? EstimatedDuration,
    DateOnly? DueDate,
    int OrderIndex);

public sealed record DisputeRemainingMilestonePlanResponse(
    string Title,
    string? Description,
    decimal Amount,
    string? EstimatedDuration,
    DateOnly? DueDate,
    string? Deliverables,
    string? AcceptanceCriteria,
    int OrderIndex,
    /// <summary>
    /// Carried over so the recreated job post keeps its work breakdown. Dropping it would leave the
    /// client re-authoring the whole WBS by hand for work that was already planned and priced.
    /// </summary>
    IReadOnlyList<DisputeRemainingWorkItemPlanResponse> WorkItems);

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
