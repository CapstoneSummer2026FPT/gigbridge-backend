namespace Application.Features.Contracts.Milestones.Client.RequestRevision.DTOs;

public sealed record RequestMilestoneRevisionRequest(string Reason, IReadOnlyCollection<Guid> WorkItemIds);
