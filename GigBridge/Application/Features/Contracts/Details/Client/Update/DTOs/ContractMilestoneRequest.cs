namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record ContractMilestoneRequest(
    Guid? MilestoneId,
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int? SortOrder,
    string? Description = null,
    string? EstimatedDuration = null,
    string? Deliverables = null,
    string? AcceptanceCriteria = null);
