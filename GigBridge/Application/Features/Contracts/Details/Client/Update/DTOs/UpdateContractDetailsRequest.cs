namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record UpdateContractDetailsRequest(
    IReadOnlyList<ContractMilestoneRequest> Milestones);
