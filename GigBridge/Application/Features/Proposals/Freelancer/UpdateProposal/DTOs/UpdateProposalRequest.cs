using Application.Features.Proposals.Common.DTOs;

namespace Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;

public class UpdateProposalRequest
{
    public string? CoverLetter { get; set; }
    public decimal? ProposedBudget { get; set; }
    public string? ProposedDuration { get; set; }
    public string? AnalysisSummary { get; set; }
    public string? SolutionApproach { get; set; }
    public string? Deliverables { get; set; }
    public string? Assumptions { get; set; }
    public string? OutOfScope { get; set; }
    public IReadOnlyCollection<ProposalWorkBreakdownItemDto>? WorkBreakdownItems { get; set; }
    public IReadOnlyCollection<ProposalMilestonePlanDto>? MilestonePlans { get; set; }
}
