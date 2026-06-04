using Application.Features.Proposals.Common.DTOs;
using MediatR;

namespace Application.Features.Proposals.Common.GetMyProposalByJobPost.Queries;

public record GetMyProposalByJobPostQuery(
    Guid JobPostId,
    Guid UserId
) : IRequest<ProposalDetailDto>;