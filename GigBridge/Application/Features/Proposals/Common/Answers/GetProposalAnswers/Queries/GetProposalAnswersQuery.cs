using Application.Features.Proposals.Common.DTOs;
using MediatR;

namespace Application.Features.Proposals.Common.Answers.GetProposalAnswers.Queries;

public record GetProposalAnswersQuery(
    Guid ProposalsId,
    Guid UserId,
    string Role) : IRequest<IEnumerable<ProposalAnswerDto>>;
