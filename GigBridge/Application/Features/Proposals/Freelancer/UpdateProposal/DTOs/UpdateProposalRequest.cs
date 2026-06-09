namespace Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;

public class UpdateProposalRequest
{
    public string? CoverLetter { get; set; }

    public decimal? ProposedRate { get; set; }

    public string? ProposedDuration { get; set; }
}