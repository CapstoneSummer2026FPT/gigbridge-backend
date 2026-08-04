namespace Application.Features.Proposals.Common.Email;

public sealed record ProposalNegotiationEmailModel(
    string FreelancerName,
    string ClientName,
    string JobTitle,
    string ProposedBudget,
    string ProposedDuration,
    string ActionUrl);
