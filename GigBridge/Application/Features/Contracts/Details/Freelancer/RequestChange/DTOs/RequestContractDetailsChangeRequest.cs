namespace Application.Features.Contracts.Details.Freelancer.RequestChange.DTOs;

public sealed record RequestContractDetailsChangeRequest(
    string? Reason,
    IReadOnlyCollection<Guid>? AffectedMilestoneIds = null,
    IReadOnlyCollection<Guid>? AffectedWorkItemIds = null);
