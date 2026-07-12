using Application.Features.Proposals.Common.DTOs;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.DTOs;

public record SubmitProposalRequest(
    Guid JobPostsId,
    string? CoverLetter,
    decimal? ProposedBudget,
    string? ProposedDuration,
    string? AnalysisSummary = null,
    string? SolutionApproach = null,
    string? Deliverables = null,
    string? Assumptions = null,
    string? OutOfScope = null,
    IReadOnlyCollection<ProposalWorkBreakdownItemDto>? WorkBreakdownItems = null,
    IReadOnlyCollection<ProposalMilestonePlanDto>? MilestonePlans = null
);
