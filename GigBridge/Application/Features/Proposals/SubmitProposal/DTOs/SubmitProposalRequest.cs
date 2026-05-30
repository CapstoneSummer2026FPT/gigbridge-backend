using System;

namespace Application.Features.Proposals.SubmitProposal.DTOs;

public record SubmitProposalRequest(
    Guid JobPostsId,
    string? CoverLetter,
    decimal? ProposedRate,
    string? ProposedDuration
);