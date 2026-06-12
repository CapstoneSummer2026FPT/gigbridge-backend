namespace Application.Features.Contracts.Details.Client.Update.DTOs;

public sealed record UpdateContractDetailsRequest(
    string ScopeOfWork,
    string PaymentTerms,
    string IntellectualPropertyTerms,
    string ConfidentialityTerms,
    string CancellationTerms,
    string DisputeTerms,
    IReadOnlyList<ContractMilestoneRequest> Milestones);
