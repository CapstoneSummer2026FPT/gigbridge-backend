namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record ContractMilestoneRequest(
    Guid? MilestoneId,
    string Title,
    decimal Amount,
    DateOnly? DueDate,
    int? SortOrder);
