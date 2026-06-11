using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateProposalAnswer.Commands;

public record UpdateProposalAnswerCommand(
    Guid ProposalsId,
    Guid ProposalAnswersId,
    Guid UserId,
    UpdateProposalAnswerRequest Request) : IRequest<ProposalAnswerDto>;
