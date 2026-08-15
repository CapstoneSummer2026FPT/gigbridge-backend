namespace Application.Common.InternalServices.Proposals.Models;
public sealed record ProposalNegotiationEmailModel(
    string FreelancerName,
    string ClientName,
    string JobTitle,
    string ProposedBudget,
    string ProposedDuration,
    string ActionUrl);
