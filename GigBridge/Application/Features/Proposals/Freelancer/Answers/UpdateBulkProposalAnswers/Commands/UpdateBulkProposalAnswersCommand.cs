using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.DTOs;
using MediatR;

namespace Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.Commands;

public record UpdateBulkProposalAnswersCommand(
    Guid ProposalsId,
    Guid UserId,
    UpdateBulkProposalAnswersRequest Request) : IRequest<IEnumerable<ProposalAnswerDto>>;
