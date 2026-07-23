namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record ContractWorkItemRequest(
    Guid? WorkItemId,
    string Title,
    string? Description,
    string? Deliverables,
    string? EstimatedDuration,
    int OrderIndex);

public sealed record ContractMilestoneRequest(
    Guid? MilestoneId,
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int? SortOrder,
    string? Description = null,
    string? EstimatedDuration = null,
    string? Deliverables = null,
    string? AcceptanceCriteria = null,
    IReadOnlyList<ContractWorkItemRequest>? WorkItems = null);
