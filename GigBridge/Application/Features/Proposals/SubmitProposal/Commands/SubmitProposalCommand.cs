using Application.Features.Proposals.SubmitProposal.DTOs;
using MediatR;
using System;

namespace Application.Features.Proposals.SubmitProposal.Commands;

public record SubmitProposalCommand(SubmitProposalRequest Request, Guid FreelancerProfilesId) : IRequest<Guid>;