namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record UpdateContractDetailsRequest(
    string DisputeTerms,
    IReadOnlyList<ContractMilestoneRequest> Milestones);
