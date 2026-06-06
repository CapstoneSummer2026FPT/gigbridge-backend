using Application.Features.Proposals.Common.DTOs;
using MediatR;

namespace Application.Features.Proposals.Common.GetProposalDetail.Queries;

public record GetProposalDetailQuery(
    Guid ProposalId,
    Guid UserId
) : IRequest<ProposalDetailDto>;