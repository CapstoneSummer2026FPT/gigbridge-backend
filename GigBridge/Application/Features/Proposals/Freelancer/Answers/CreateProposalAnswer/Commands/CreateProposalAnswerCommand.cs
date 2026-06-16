using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.Commands;

public record CreateProposalAnswerCommand(
    Guid ProposalsId,
    Guid UserId,
    CreateProposalAnswerRequest Request) : IRequest<ProposalAnswerDto>;
