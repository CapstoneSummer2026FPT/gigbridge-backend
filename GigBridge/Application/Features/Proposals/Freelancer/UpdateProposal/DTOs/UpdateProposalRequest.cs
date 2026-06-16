namespace Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;

public class UpdateProposalRequest
{
    public string? CoverLetter { get; set; }

    public decimal? ProposedBudget { get; set; }

    public string? ProposedDuration { get; set; }
}
