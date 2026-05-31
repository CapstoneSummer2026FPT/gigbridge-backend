using Application.Features.Proposals.Freelancer.SubmitProposal.DTOs;
using MediatR;
using System;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public record SubmitProposalCommand(
    SubmitProposalRequest Request,
    Guid UserId
) : IRequest<Guid>;